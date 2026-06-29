using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

public class ConveyorExportSystem : MonoBehaviour
{
    private static readonly ProfilerMarker UpdateMarker =
        new ProfilerMarker("ConveyorExportSystem.Update");
    private static readonly ProfilerMarker ProcessDroppedItemsMarker =
        new ProfilerMarker("ConveyorExportSystem.ProcessDroppedItems");

    [Header("References")]
    [SerializeField] private FacilityUpgradeCatalog facilityUpgradeCatalog;
    [SerializeField] private StorageManager storageManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private DroppedItemManager droppedItemManager;
    [SerializeField] private TerrainDataManager terrainDataManager;

    [Header("Scene Points")]
    [SerializeField] private GameObject conveyorRoot;
    [SerializeField] private Collider playerInputArea;
    [SerializeField] private Collider[] playerInputAreas = new Collider[0];
    [SerializeField] private Collider droppedItemInputArea;
    [SerializeField] private Collider[] droppedItemInputAreas = new Collider[0];
    [SerializeField] private Transform externalDepositPoint;
    [SerializeField] private Transform visualAreaCenter;
    [SerializeField] private Transform[] visualAreaCenters = new Transform[0];
    [SerializeField] private Vector3 visualAreaSize = new Vector3(4f, 0.25f, 1f);
    [SerializeField] private bool moveVisualItemsInward = true;
    [SerializeField] private Vector3 inwardAxis = Vector3.right;
    [SerializeField, Min(0f)] private float inwardTargetInset = 0.1f;

    [Header("Transfer")]
    [SerializeField, Min(0.01f)] private float playerItemTransferSpeed = 50f;
    [SerializeField, Min(0.01f)] private float visualMoveSpeed = 5f;
    [SerializeField, Min(0f)] private float visualLifetimeSeconds = 1.25f;
    [SerializeField, Min(1)] private int maxDroppedItemScansPerFrame = 64;
    [SerializeField, Min(0f)] private float droppedItemScanInterval = 0.05f;
    [SerializeField, Min(1)] private int maxActiveVisualItems = 96;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Facility Upgrade")]
    [SerializeField] private Stat unlock = new Stat { BaseValue = 0f };

    public bool IsUnlocked => isUnlocked;

    private readonly Queue<GameObject> activeVisualItems = new Queue<GameObject>();
    private bool isUnlocked;
    private bool isTransferringPlayerItems;
    private float nextDroppedItemScanTime;
    private int nextDroppedItemScanIndex;
    private bool missingPersistenceLogged;

    private void Awake()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        ApplyEnhancements();
    }

    private void OnEnable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged += ApplyEnhancements;
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged -= ApplyEnhancements;
    }

    private void Update()
    {
        using (UpdateMarker.Auto())
        {
            if (!isUnlocked)
            {
                return;
            }

            StartPlayerTransferIfNeeded();
            ProcessDroppedItemsInArea();
        }
    }

    public bool TryExportExternalItem(VoxelItemData itemData, Vector3 sourcePosition, bool routeViaDepositPoint)
    {
        if (!CanExportItem(itemData, "ConveyorExportSystem.TryExportExternalItem"))
        {
            return false;
        }

        ExportItemNow(itemData, sourcePosition, routeViaDepositPoint);
        return true;
    }

    public bool TryExportExternalItems(IList<VoxelItemData> items, Vector3 sourcePosition, bool routeViaDepositPoint)
    {
        if (!isUnlocked)
        {
            return false;
        }

        if (items == null)
        {
            Debug.LogError("ConveyorExportSystem: item list is null.", this);
            return false;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (!CanExportItem(items[i], "ConveyorExportSystem.TryExportExternalItems"))
            {
                return false;
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            ExportItemNow(items[i], sourcePosition, routeViaDepositPoint);
        }

        return true;
    }

    private void StartPlayerTransferIfNeeded()
    {
        if (isTransferringPlayerItems ||
            playerController == null ||
            playerController.Inventory == null ||
            playerController.Inventory.IsEmpty() ||
            !IsPlayerInInputArea())
        {
            return;
        }

        TransferPlayerItemsAsync().Forget();
    }

    private async UniTask TransferPlayerItemsAsync()
    {
        isTransferringPlayerItems = true;
        try
        {
            while (isUnlocked &&
                   playerController != null &&
                   playerController.Inventory != null &&
                   !playerController.Inventory.IsEmpty() &&
                   IsPlayerInInputArea())
            {
                if (!playerController.Inventory.TryPeekNextItem(out VoxelItemData itemData))
                {
                    Debug.LogError("ConveyorExportSystem: failed to read next player inventory item.", this);
                    break;
                }

                if (!CanExportItem(itemData, "ConveyorExportSystem.TransferPlayerItemsAsync"))
                {
                    break;
                }

                Vector3 sourcePosition = playerController.transform.position;
                GetVisualFlowPositions(sourcePosition, out Vector3 startPosition, out Vector3 targetPosition);
                if (!CreateVisualItem(itemData, startPosition, out GameObject visualItem))
                {
                    break;
                }

                if (!playerController.Inventory.TryRemoveNextItem(out VoxelItemData removedItem))
                {
                    Debug.LogError("ConveyorExportSystem: failed to remove player inventory item.", this);
                    Destroy(visualItem);
                    break;
                }

                storageManager.AddResource(removedItem.resourceType, 1);
                AnimateVisualItem(visualItem, startPosition, targetPosition).Forget();
                await UniTask.Delay(TimeSpan.FromSeconds(1f / playerItemTransferSpeed));
            }
        }
        finally
        {
            isTransferringPlayerItems = false;
        }
    }

    private void ProcessDroppedItemsInArea()
    {
        if (Time.time < nextDroppedItemScanTime || droppedItemManager == null || droppedItemInputArea == null)
        {
            return;
        }

        nextDroppedItemScanTime = Time.time + droppedItemScanInterval;
        using (ProcessDroppedItemsMarker.Auto())
        {
            List<DroppedItem> activeItems = droppedItemManager.GetActiveItems();
            if (activeItems.Count == 0)
            {
                nextDroppedItemScanIndex = 0;
                return;
            }

            if (nextDroppedItemScanIndex >= activeItems.Count)
            {
                nextDroppedItemScanIndex = 0;
            }

            int scanned = 0;
            int scanBudget = Mathf.Min(maxDroppedItemScansPerFrame, activeItems.Count);
            while (scanned < scanBudget)
            {
                if (nextDroppedItemScanIndex >= activeItems.Count)
                {
                    nextDroppedItemScanIndex = 0;
                }

                DroppedItem item = activeItems[nextDroppedItemScanIndex];
                nextDroppedItemScanIndex++;
                scanned++;

                if (!IsDroppedItemInInputArea(item))
                {
                    continue;
                }

                ExportDroppedItem(item);
            }
        }
    }

    private void ExportDroppedItem(DroppedItem item)
    {
        if (item == null || !VoxelItemData.TryCreateFromDroppedItem(item, out VoxelItemData itemData))
        {
            return;
        }

        if (!CanExportItem(itemData, "ConveyorExportSystem.ExportDroppedItem"))
        {
            return;
        }

        Vector3 sourcePosition = item.transform.position;
        storageManager.AddResource(itemData.resourceType, 1);
        droppedItemManager.ReturnItem(item.gameObject);

        if (CreateVisualItem(itemData, sourcePosition, out GameObject visualItem))
        {
            GetVisualFlowPositions(sourcePosition, out Vector3 startPosition, out Vector3 targetPosition);
            visualItem.transform.position = startPosition;
            AnimateVisualItem(visualItem, startPosition, targetPosition).Forget();
        }
    }

    private void ExportItemNow(
        VoxelItemData itemData,
        Vector3 sourcePosition,
        bool routeViaDepositPoint)
    {
        storageManager.AddResource(itemData.resourceType, 1);
        Vector3 referencePosition = routeViaDepositPoint && externalDepositPoint != null
            ? externalDepositPoint.position
            : sourcePosition;
        GetVisualFlowPositions(referencePosition, out Vector3 startPosition, out Vector3 targetPosition);

        if (!CreateVisualItem(itemData, startPosition, out GameObject visualItem))
        {
            return;
        }

        AnimateVisualItem(visualItem, startPosition, targetPosition).Forget();
    }

    private bool CanExportItem(VoxelItemData itemData, string context)
    {
        if (!isUnlocked)
        {
            return false;
        }

        if (storageManager == null)
        {
            Debug.LogError("ConveyorExportSystem: StorageManager is not configured.", this);
            return false;
        }

        return itemData != null && itemData.IsValid(context);
    }

    private bool CreateVisualItem(VoxelItemData itemData, Vector3 position, out GameObject visualItem)
    {
        bool created = VoxelItemVisualUtility.TryCreateAnimationItem(
            position,
            transform,
            itemData,
            terrainDataManager,
            "ConveyorExportSystem",
            out visualItem);

        if (created)
        {
            RegisterVisualItem(visualItem);
        }

        return created;
    }

    private void RegisterVisualItem(GameObject visualItem)
    {
        while (activeVisualItems.Count > 0 && activeVisualItems.Peek() == null)
        {
            activeVisualItems.Dequeue();
        }

        activeVisualItems.Enqueue(visualItem);
        while (activeVisualItems.Count > maxActiveVisualItems)
        {
            GameObject oldest = activeVisualItems.Dequeue();
            if (oldest != null)
            {
                Destroy(oldest);
            }
        }
    }

    private async UniTask AnimateVisualItemViaDeposit(
        GameObject visualItem,
        Vector3 sourcePosition,
        Vector3 depositPosition,
        Vector3 targetPosition)
    {
        await MoveVisualItemAsync(visualItem, sourcePosition, depositPosition);
        await MoveVisualItemAsync(visualItem, depositPosition, targetPosition);
        await HoldAndDestroyVisualItem(visualItem);
    }

    private async UniTask AnimateVisualItem(GameObject visualItem, Vector3 sourcePosition, Vector3 targetPosition)
    {
        await MoveVisualItemAsync(visualItem, sourcePosition, targetPosition);
        await HoldAndDestroyVisualItem(visualItem);
    }

    private async UniTask MoveVisualItemAsync(GameObject visualItem, Vector3 sourcePosition, Vector3 targetPosition)
    {
        if (visualItem == null)
        {
            return;
        }

        float distance = Vector3.Distance(sourcePosition, targetPosition);
        float duration = distance > 0.001f ? distance / visualMoveSpeed : 0f;
        if (duration <= 0f)
        {
            visualItem.transform.position = targetPosition;
            return;
        }

        float elapsed = 0f;
        while (visualItem != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float curvedProgress = movementCurve != null ? movementCurve.Evaluate(progress) : progress;
            visualItem.transform.position = Vector3.Lerp(sourcePosition, targetPosition, curvedProgress);
            visualItem.transform.Rotate(0f, 360f * Time.deltaTime, 0f);
            await UniTask.Yield();
        }

        if (visualItem != null)
        {
            visualItem.transform.position = targetPosition;
        }
    }

    private async UniTask HoldAndDestroyVisualItem(GameObject visualItem)
    {
        if (visualLifetimeSeconds > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(visualLifetimeSeconds));
        }

        if (visualItem != null)
        {
            Destroy(visualItem);
        }
    }

    private void GetVisualFlowPositions(Vector3 sourcePosition, out Vector3 startPosition, out Vector3 targetPosition)
    {
        Vector3 center = GetVisualAreaCenterForSource(sourcePosition);
        startPosition = GetClosestVisualAreaPosition(sourcePosition, center);
        targetPosition = GetInwardVisualPosition(center, startPosition);
    }

    private Vector3 GetInwardVisualPosition(Vector3 center, Vector3 startPosition)
    {
        Vector3 halfSize = visualAreaSize * 0.5f;
        if (moveVisualItemsInward && IsAxisConfigured(inwardAxis))
        {
            Vector3 targetPosition = startPosition;
            targetPosition.x = center.x + GetInwardXOffset(center, halfSize.x);
            return targetPosition;
        }

        return center + new Vector3(
            UnityEngine.Random.Range(-halfSize.x, halfSize.x),
            UnityEngine.Random.Range(-halfSize.y, halfSize.y),
            UnityEngine.Random.Range(-halfSize.z, halfSize.z));
    }

    private Vector3 GetClosestVisualAreaPosition(Vector3 sourcePosition, Vector3 center)
    {
        Vector3 halfSize = visualAreaSize * 0.5f;
        Vector3 offset = sourcePosition - center;
        return center + new Vector3(
            Mathf.Clamp(offset.x, -halfSize.x, halfSize.x),
            Mathf.Clamp(offset.y, -halfSize.y, halfSize.y),
            Mathf.Clamp(offset.z, -halfSize.z, halfSize.z));
    }

    private Vector3 GetVisualAreaCenterForSource(Vector3 sourcePosition)
    {
        Transform nearest = GetNearestVisualAreaCenter(sourcePosition);
        if (nearest != null)
        {
            return nearest.position;
        }

        return GetRandomVisualAreaCenter();
    }

    private Transform GetNearestVisualAreaCenter(Vector3 sourcePosition)
    {
        Transform nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < visualAreaCenters.Length; i++)
        {
            Transform candidate = visualAreaCenters[i];
            if (candidate == null)
            {
                continue;
            }

            Vector3 closestPosition = GetClosestVisualAreaPosition(sourcePosition, candidate.position);
            float sqrDistance = (closestPosition - sourcePosition).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearest = candidate;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearest;
    }

    private float GetInwardXOffset(Vector3 center, float halfWidth)
    {
        float usableHalfWidth = Mathf.Max(0f, halfWidth - inwardTargetInset);
        if (usableHalfWidth <= 0f)
        {
            return 0f;
        }

        Vector3 referencePosition = externalDepositPoint != null ? externalDepositPoint.position : transform.position;
        float side = Vector3.Dot(center - referencePosition, inwardAxis.normalized);
        if (Mathf.Abs(side) <= 0.001f)
        {
            return 0f;
        }

        return side < 0f ? usableHalfWidth : -usableHalfWidth;
    }

    private Vector3 GetRandomVisualAreaCenter()
    {
        int validCenterCount = 0;
        for (int i = 0; i < visualAreaCenters.Length; i++)
        {
            if (visualAreaCenters[i] != null)
            {
                validCenterCount++;
            }
        }

        if (validCenterCount > 0)
        {
            int selectedIndex = UnityEngine.Random.Range(0, validCenterCount);
            for (int i = 0; i < visualAreaCenters.Length; i++)
            {
                Transform candidate = visualAreaCenters[i];
                if (candidate == null)
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return candidate.position;
                }

                selectedIndex--;
            }
        }

        if (visualAreaCenter != null)
        {
            return visualAreaCenter.position;
        }

        return transform.position;
    }

    private bool IsAxisConfigured(Vector3 axis)
    {
        return axis.sqrMagnitude > 0.0001f;
    }

    private bool IsPlayerInInputArea()
    {
        return playerController != null && IsPointInAnyInputArea(playerController.transform.position);
    }

    private bool IsDroppedItemInInputArea(DroppedItem item)
    {
        if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
        {
            return false;
        }

        Bounds itemBounds = item.ItemBounds;
        if (IsBoundsInInputArea(droppedItemInputArea, itemBounds, item.transform.position))
        {
            return true;
        }

        for (int i = 0; i < droppedItemInputAreas.Length; i++)
        {
            if (IsBoundsInInputArea(droppedItemInputAreas[i], itemBounds, item.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointInAnyInputArea(Vector3 position)
    {
        if (IsPointInInputArea(playerInputArea, position))
        {
            return true;
        }

        for (int i = 0; i < playerInputAreas.Length; i++)
        {
            if (IsPointInInputArea(playerInputAreas[i], position))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointInInputArea(Collider inputArea, Vector3 position)
    {
        return inputArea != null && inputArea.bounds.Contains(position);
    }

    private bool IsBoundsInInputArea(Collider inputArea, Bounds itemBounds, Vector3 itemPosition)
    {
        if (inputArea == null)
        {
            return false;
        }

        Bounds inputBounds = inputArea.bounds;
        return inputBounds.Intersects(itemBounds) || inputBounds.Contains(itemPosition);
    }

    private void ApplyEnhancements()
    {
        if (facilityUpgradeCatalog == null)
        {
            return;
        }

        unlock.RemoveAllModifiers();
        GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
        if (persistence == null)
        {
            if (!missingPersistenceLogged)
            {
                missingPersistenceLogged = true;
                Debug.LogError("ConveyorExportSystem: GameDataPersistenceManager is not initialized.", this);
            }

            SetUnlocked(false);
            return;
        }

        missingPersistenceLogged = false;
        IReadOnlyList<FacilityUpgradeDefinition> upgrades = facilityUpgradeCatalog.Upgrades;
        for (int upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
        {
            FacilityUpgradeDefinition upgrade = upgrades[upgradeIndex];
            int level = persistence.GetFacilityUpgradeLevel(upgrade.UpgradeId, upgrade.InitialLevel);
            int effectLevel = upgrade.GetEffectLevel(level);
            if (effectLevel == 0)
            {
                continue;
            }

            IReadOnlyList<Enhancement> enhancements = upgrade.Enhancements;
            for (int enhancementIndex = 0; enhancementIndex < enhancements.Count; enhancementIndex++)
            {
                Enhancement enhancement = enhancements[enhancementIndex];
                if (enhancement.TargetCategory != "Conveyor")
                {
                    continue;
                }

                if (enhancement.TargetStatName != "Unlock")
                {
                    Debug.LogError(
                        $"ConveyorExportSystem: enhancement '{enhancement.name}' targets unsupported stat '{enhancement.TargetStatName}'.",
                        this);
                    continue;
                }

                ApplyModifier(unlock, enhancement, effectLevel);
            }
        }

        SetUnlocked(unlock.IntValue > 0);
    }

    private void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        if (conveyorRoot != null && conveyorRoot != gameObject)
        {
            conveyorRoot.SetActive(unlocked);
        }
    }

    private void ApplyModifier(Stat stat, Enhancement enhancement, int level)
    {
        if (enhancement.Type == EnhancementType.Additive)
        {
            stat.AddAdditiveModifier(level * enhancement.Value);
        }
        else if (enhancement.Type == EnhancementType.Multiplicative)
        {
            stat.AddMultiplicativeModifier(Mathf.Pow(enhancement.Value, level));
        }
    }

    private bool ValidateConfiguration()
    {
        bool isValid = true;
        playerInputAreas ??= new Collider[0];
        droppedItemInputAreas ??= new Collider[0];
        visualAreaCenters ??= new Transform[0];

        if (facilityUpgradeCatalog == null)
        {
            Debug.LogError("ConveyorExportSystem: facilityUpgradeCatalog is not configured.", this);
            isValid = false;
        }
        else
        {
            isValid &= facilityUpgradeCatalog.ValidateConfiguration(this);
        }

        if (storageManager == null)
        {
            Debug.LogError("ConveyorExportSystem: storageManager is not configured.", this);
            isValid = false;
        }

        if (playerController == null)
        {
            Debug.LogError("ConveyorExportSystem: playerController is not configured.", this);
            isValid = false;
        }

        if (droppedItemManager == null)
        {
            Debug.LogError("ConveyorExportSystem: droppedItemManager is not configured.", this);
            isValid = false;
        }

        if (terrainDataManager == null)
        {
            Debug.LogError("ConveyorExportSystem: terrainDataManager is not configured.", this);
            isValid = false;
        }

        if (playerInputArea == null && !HasAnyConfiguredCollider(playerInputAreas))
        {
            Debug.LogError("ConveyorExportSystem: player input areas are not configured.", this);
            isValid = false;
        }

        if (droppedItemInputArea == null && !HasAnyConfiguredCollider(droppedItemInputAreas))
        {
            Debug.LogError("ConveyorExportSystem: dropped item input areas are not configured.", this);
            isValid = false;
        }

        if (visualAreaCenter == null && !HasAnyConfiguredTransform(visualAreaCenters))
        {
            Debug.LogError("ConveyorExportSystem: visual area centers are not configured.", this);
            isValid = false;
        }

        if (visualAreaSize.x <= 0f || visualAreaSize.y < 0f || visualAreaSize.z <= 0f)
        {
            Debug.LogError($"ConveyorExportSystem: visualAreaSize is invalid. visualAreaSize={visualAreaSize}", this);
            isValid = false;
        }

        if (moveVisualItemsInward && !IsAxisConfigured(inwardAxis))
        {
            Debug.LogError("ConveyorExportSystem: inwardAxis is not configured.", this);
            isValid = false;
        }

        return isValid;
    }

    private bool HasAnyConfiguredCollider(Collider[] colliders)
    {
        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAnyConfiguredTransform(Transform[] transforms)
    {
        if (transforms == null)
        {
            return false;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        playerItemTransferSpeed = Mathf.Max(0.01f, playerItemTransferSpeed);
        visualMoveSpeed = Mathf.Max(0.01f, visualMoveSpeed);
        visualLifetimeSeconds = Mathf.Max(0f, visualLifetimeSeconds);
        maxDroppedItemScansPerFrame = Mathf.Max(1, maxDroppedItemScansPerFrame);
        droppedItemScanInterval = Mathf.Max(0f, droppedItemScanInterval);
        maxActiveVisualItems = Mathf.Max(1, maxActiveVisualItems);
        visualAreaSize = new Vector3(
            Mathf.Max(0.01f, visualAreaSize.x),
            Mathf.Max(0f, visualAreaSize.y),
            Mathf.Max(0.01f, visualAreaSize.z));
        inwardTargetInset = Mathf.Max(0f, inwardTargetInset);
    }
#endif
}
