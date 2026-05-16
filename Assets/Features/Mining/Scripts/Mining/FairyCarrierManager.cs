using System.Collections.Generic;
using UnityEngine;

public class FairyCarrierManager : MonoBehaviour
{
    private const string CarrierUpgradeId = "garage.fairy.carrier";

    private enum FairyState
    {
        IdleAtHome,
        Searching,
        MovingToItem,
        CarryingToHome,
        Depositing
    }

    [Header("References")]
    [SerializeField] private Transform homePoint;
    [SerializeField] private GameObject fairyPrefab;
    [SerializeField] private TerrainDataManager terrainDataManager;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float pickupDistance = 0.25f;
    [SerializeField] private float homeArrivalDistance = 0.25f;
    [SerializeField] private float searchInterval = 0.25f;
    [SerializeField] private Vector3 carriedItemLocalOffset = new Vector3(0f, 0.45f, 0f);

    private readonly HashSet<DroppedItem> reservedItems = new HashSet<DroppedItem>();
    private GameObject fairyInstance;
    private GameObject carriedItemVisual;
    private DroppedItem targetItem;
    private VoxelItemData carriedItem;
    private FairyState state = FairyState.IdleAtHome;
    private bool isUnlocked;
    private float nextSearchTime;

    private void Awake()
    {
        ValidateConfiguration();
    }

    private void OnEnable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged += RefreshUnlockState;
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged -= RefreshUnlockState;
    }

    private void Start()
    {
        RefreshUnlockState();
    }

    private void Update()
    {
        if (!isUnlocked)
        {
            return;
        }

        if (homePoint == null)
        {
            return;
        }

        EnsureFairyInstance();
        if (fairyInstance == null)
        {
            return;
        }

        switch (state)
        {
            case FairyState.IdleAtHome:
                UpdateIdleAtHome();
                break;
            case FairyState.Searching:
                SearchFromCurrentPosition();
                break;
            case FairyState.MovingToItem:
                UpdateMovingToItem();
                break;
            case FairyState.CarryingToHome:
                UpdateCarryingToHome();
                break;
            case FairyState.Depositing:
                DepositCarriedItem();
                break;
        }
    }

    private void RefreshUnlockState()
    {
        int level = GameDataPersistenceManager.Instance.GetFacilityUpgradeLevel(CarrierUpgradeId, 0);
        bool shouldBeUnlocked = level > 0;
        if (isUnlocked == shouldBeUnlocked)
        {
            return;
        }

        isUnlocked = shouldBeUnlocked;
        if (isUnlocked)
        {
            EnsureFairyInstance();
            state = FairyState.Searching;
        }
        else
        {
            ClearTargetReservation();
            ClearCarriedItem();
            if (fairyInstance != null)
            {
                Destroy(fairyInstance);
                fairyInstance = null;
            }

            state = FairyState.IdleAtHome;
        }
    }

    private bool ValidateConfiguration()
    {
        bool isValid = true;
        if (homePoint == null)
        {
            Debug.LogError("FairyCarrierManager: homePoint is not configured.", this);
            isValid = false;
        }

        if (fairyPrefab == null)
        {
            Debug.LogError("FairyCarrierManager: fairyPrefab is not configured.", this);
            isValid = false;
        }

        if (terrainDataManager == null)
        {
            Debug.LogError("FairyCarrierManager: terrainDataManager is not configured.", this);
            isValid = false;
        }

        return isValid;
    }

    private void EnsureFairyInstance()
    {
        if (fairyInstance != null || homePoint == null)
        {
            return;
        }

        if (fairyPrefab == null)
        {
            return;
        }

        fairyInstance = Instantiate(fairyPrefab, homePoint.position, Quaternion.identity, transform);
        fairyInstance.name = "FairyCarrier";
        DisableFairyColliders();
    }

    private void DisableFairyColliders()
    {
        Collider[] colliders = fairyInstance.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void UpdateIdleAtHome()
    {
        if (MoveFairyTowards(homePoint.position, homeArrivalDistance) && Time.time >= nextSearchTime)
        {
            state = FairyState.Searching;
        }
    }

    private void SearchFromCurrentPosition()
    {
        if (TryAssignNearestTarget(fairyInstance.transform.position))
        {
            state = FairyState.MovingToItem;
            return;
        }

        nextSearchTime = Time.time + searchInterval;
        state = FairyState.IdleAtHome;
    }

    private void UpdateMovingToItem()
    {
        if (!IsTargetStillAvailable(targetItem))
        {
            ClearTargetReservation();
            SearchFromCurrentPosition();
            return;
        }

        if (!MoveFairyTowards(targetItem.transform.position, pickupDistance))
        {
            return;
        }

        if (!TryPickupTarget())
        {
            ClearTargetReservation();
            SearchFromCurrentPosition();
            return;
        }

        state = FairyState.CarryingToHome;
    }

    private void UpdateCarryingToHome()
    {
        UpdateCarriedItemVisual();
        if (MoveFairyTowards(homePoint.position, homeArrivalDistance))
        {
            state = FairyState.Depositing;
        }
    }

    private void DepositCarriedItem()
    {
        if (carriedItem != null && StorageManager.Instance != null)
        {
            StorageManager.Instance.AddResource(carriedItem.resourceType, 1);
        }

        ClearCarriedItem();
        state = FairyState.Searching;
    }

    private bool MoveFairyTowards(Vector3 destination, float arrivalDistance)
    {
        Vector3 current = fairyInstance.transform.position;
        float distance = Vector3.Distance(current, destination);
        if (distance <= arrivalDistance)
        {
            fairyInstance.transform.position = destination;
            return true;
        }

        fairyInstance.transform.position = Vector3.MoveTowards(current, destination, moveSpeed * Time.deltaTime);
        return false;
    }

    private bool TryAssignNearestTarget(Vector3 origin)
    {
        DroppedItemManager itemManager = DroppedItemManager.Instance;
        if (itemManager == null)
        {
            return false;
        }

        reservedItems.RemoveWhere(IsReservedItemInvalid);
        List<DroppedItem> activeItems = itemManager.GetActiveItems();
        DroppedItem nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < activeItems.Count; i++)
        {
            DroppedItem item = activeItems[i];
            if (!IsTargetStillAvailable(item) || reservedItems.Contains(item))
            {
                continue;
            }

            float sqrDistance = (item.transform.position - origin).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = item;
            }
        }

        if (nearest == null)
        {
            return false;
        }

        targetItem = nearest;
        reservedItems.Add(targetItem);
        return true;
    }

    private bool TryPickupTarget()
    {
        if (!IsTargetStillAvailable(targetItem))
        {
            return false;
        }

        if (!VoxelItemData.TryCreateFromDroppedItem(targetItem, out VoxelItemData itemData))
        {
            return false;
        }

        carriedItem = itemData;
        DroppedItem pickedItem = targetItem;
        ClearTargetReservation();
        DroppedItemManager.Instance.ReturnItem(pickedItem.gameObject);
        CreateCarriedItemVisual();
        return true;
    }

    private void CreateCarriedItemVisual()
    {
        ClearCarriedItemVisual();
        if (carriedItem == null)
        {
            return;
        }

        if (VoxelItemVisualUtility.TryCreateAnimationItem(
                fairyInstance.transform.position,
                fairyInstance.transform,
                carriedItem,
                terrainDataManager,
                "FairyCarrierManager",
                out GameObject visual))
        {
            carriedItemVisual = visual;
            UpdateCarriedItemVisual();
        }
    }

    private void UpdateCarriedItemVisual()
    {
        if (carriedItemVisual != null)
        {
            carriedItemVisual.transform.localPosition = carriedItemLocalOffset;
            carriedItemVisual.transform.Rotate(0f, 180f * Time.deltaTime, 0f);
        }
    }

    private void ClearTargetReservation()
    {
        if (targetItem != null)
        {
            reservedItems.Remove(targetItem);
            targetItem = null;
        }
    }

    private void ClearCarriedItem()
    {
        carriedItem = null;
        ClearCarriedItemVisual();
    }

    private void ClearCarriedItemVisual()
    {
        if (carriedItemVisual != null)
        {
            Destroy(carriedItemVisual);
            carriedItemVisual = null;
        }
    }

    private bool IsTargetStillAvailable(DroppedItem item)
    {
        if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
        {
            return false;
        }

        DroppedItemManager itemManager = DroppedItemManager.Instance;
        return itemManager != null && itemManager.GetActiveItems().Contains(item);
    }

    private bool IsReservedItemInvalid(DroppedItem item)
    {
        return item == null || item.gameObject == null || !item.gameObject.activeInHierarchy;
    }
}
