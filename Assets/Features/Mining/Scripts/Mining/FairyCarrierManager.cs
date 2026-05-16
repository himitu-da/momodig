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
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private TerrainDataManager terrainDataManager;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float pickupDistance = 0.25f;
    [SerializeField] private float homeArrivalDistance = 0.25f;
    [SerializeField] private float searchInterval = 0.25f;
    [SerializeField] private Vector3 carriedItemLocalOffset = new Vector3(0f, 0.45f, 0f);

    [Header("Pathfinding")]
    [SerializeField] private MiningPassagePathOptions pathOptions = new MiningPassagePathOptions();
    [SerializeField] private float waypointArrivalDistance = 0.08f;
    [SerializeField] private float destinationRepathDistance = 0.25f;

    private readonly HashSet<DroppedItem> reservedItems = new HashSet<DroppedItem>();
    private readonly List<Vector3> activePath = new List<Vector3>();
    private readonly List<Vector3> candidatePath = new List<Vector3>();
    private GameObject fairyInstance;
    private GameObject carriedItemVisual;
    private DroppedItem targetItem;
    private VoxelItemData carriedItem;
    private FairyState state = FairyState.IdleAtHome;
    private bool isUnlocked;
    private float nextSearchTime;
    private MiningPassagePathfinder pathfinder;
    private int activePathIndex;
    private Vector3 activePathDestination;
    private bool hasActivePath;
    private bool pathDirty = true;

    private void Awake()
    {
        RebuildPathfinder();
        ValidateConfiguration();
    }

    private void OnEnable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged += RefreshUnlockState;
        SubscribeTerrainChanges();
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged -= RefreshUnlockState;
        UnsubscribeTerrainChanges();
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

        EnsurePathfinder();

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
            ClearActivePath();
        }
        else
        {
            ClearTargetReservation();
            ClearCarriedItem();
            ClearActivePath();
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

        if (terrainManager == null)
        {
            Debug.LogError("FairyCarrierManager: terrainManager is not configured. Assign it in the Inspector.", this);
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
        if (TryMoveFairyAlongPassage(homePoint.position, homeArrivalDistance, out bool arrived) && arrived && Time.time >= nextSearchTime)
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

        if (!TryMoveFairyAlongPassage(targetItem.transform.position, pickupDistance, out bool arrived))
        {
            ClearTargetReservation();
            SearchFromCurrentPosition();
            return;
        }

        if (!arrived)
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
        if (TryMoveFairyAlongPassage(homePoint.position, homeArrivalDistance, out bool arrived) && arrived)
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

    private bool TryMoveFairyAlongPassage(Vector3 destination, float arrivalDistance, out bool arrived)
    {
        arrived = false;
        Vector3 current = fairyInstance.transform.position;
        float distance = Vector3.Distance(current, destination);
        if (distance <= arrivalDistance)
        {
            fairyInstance.transform.position = destination;
            ClearActivePath();
            arrived = true;
            return true;
        }

        if (!EnsurePathTo(destination))
        {
            return false;
        }

        while (activePathIndex < activePath.Count &&
               Vector3.Distance(fairyInstance.transform.position, activePath[activePathIndex]) <= waypointArrivalDistance)
        {
            activePathIndex++;
        }

        Vector3 moveTarget = activePathIndex < activePath.Count ? activePath[activePathIndex] : destination;
        fairyInstance.transform.position = Vector3.MoveTowards(current, moveTarget, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(fairyInstance.transform.position, destination) <= arrivalDistance)
        {
            fairyInstance.transform.position = destination;
            ClearActivePath();
            arrived = true;
        }

        return true;
    }

    private bool EnsurePathTo(Vector3 destination)
    {
        if (!EnsurePathfinder())
        {
            return false;
        }

        if (hasActivePath &&
            !pathDirty &&
            (activePathDestination - destination).sqrMagnitude <= destinationRepathDistance * destinationRepathDistance &&
            activePathIndex < activePath.Count)
        {
            return true;
        }

        activePath.Clear();
        if (!pathfinder.TryFindPath(fairyInstance.transform.position, destination, activePath))
        {
            ClearActivePath();
            return false;
        }

        activePathDestination = destination;
        activePathIndex = 0;
        hasActivePath = activePath.Count > 0;
        pathDirty = false;
        return hasActivePath;
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
        float nearestPathLength = float.MaxValue;

        for (int i = 0; i < activeItems.Count; i++)
        {
            DroppedItem item = activeItems[i];
            if (!IsTargetStillAvailable(item) || reservedItems.Contains(item))
            {
                continue;
            }

            if (!TryFindPassagePathLength(origin, item.transform.position, out float pathLength))
            {
                continue;
            }

            if (pathLength < nearestPathLength)
            {
                nearestPathLength = pathLength;
                nearest = item;
            }
        }

        if (nearest == null)
        {
            return false;
        }

        targetItem = nearest;
        reservedItems.Add(targetItem);
        ClearActivePath();
        return true;
    }

    private bool TryFindPassagePathLength(Vector3 origin, Vector3 destination, out float pathLength)
    {
        pathLength = 0f;
        if (!EnsurePathfinder())
        {
            return false;
        }

        return pathfinder.TryFindPath(origin, destination, candidatePath, out pathLength);
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

        ClearActivePath();
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

    private bool EnsurePathfinder()
    {
        if (pathfinder != null)
        {
            return true;
        }

        RebuildPathfinder();
        if (pathfinder != null && isActiveAndEnabled)
        {
            SubscribeTerrainChanges();
        }

        return pathfinder != null;
    }

    private void RebuildPathfinder()
    {
        if (terrainManager == null || terrainManager.VoxelManager == null)
        {
            pathfinder = null;
            return;
        }

        pathfinder = new MiningPassagePathfinder(terrainManager, pathOptions);
        pathDirty = true;
    }

    private void SubscribeTerrainChanges()
    {
        if (terrainManager == null || terrainManager.VoxelManager == null)
        {
            return;
        }

        terrainManager.VoxelManager.TerrainCellsChanged -= OnTerrainCellsChanged;
        terrainManager.VoxelManager.TerrainCellsChanged += OnTerrainCellsChanged;
    }

    private void UnsubscribeTerrainChanges()
    {
        if (terrainManager == null || terrainManager.VoxelManager == null)
        {
            return;
        }

        terrainManager.VoxelManager.TerrainCellsChanged -= OnTerrainCellsChanged;
    }

    private void OnTerrainCellsChanged(TerrainChangeBatch change)
    {
        if (change == null || !change.HasChanges)
        {
            return;
        }

        pathDirty = true;
    }

    private void ClearActivePath()
    {
        activePath.Clear();
        activePathIndex = 0;
        hasActivePath = false;
        pathDirty = true;
    }
}
