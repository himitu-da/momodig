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
    [SerializeField, Min(1)] private int maxDroppedItemScansPerFrame = 64;
    [SerializeField, Min(0f)] private float droppedItemScanInterval = 0.05f;
    [SerializeField, Min(1)] private int maxActiveVisualItems = 96;

    [Header("Slot Lane")]
    [SerializeField, Min(1)] private int conveyorWidthBlocks = 3;
    [SerializeField, Min(1)] private int voxelsPerBlock = 3;

    [Header("Facility Upgrade")]
    [SerializeField] private Stat unlock = new Stat { BaseValue = 0f };

    public bool IsUnlocked => isUnlocked;

    private sealed class ConveyorSlotReservation
    {
        public int LaneIndex;
        public int SlotCount;
        public bool IsReleased;
    }

    private readonly Queue<GameObject> activeVisualItems = new Queue<GameObject>();
    private readonly List<ConveyorSlotReservation> activeSlotReservations = new List<ConveyorSlotReservation>();
    private bool isUnlocked;
    private bool isTransferringPlayerItems;
    private float nextDroppedItemScanTime;
    private int nextDroppedItemScanIndex;
    private bool missingPersistenceLogged;
    private float[] nextSlotEntryTimes = Array.Empty<float>();

    private void Awake()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        EnsureSlotLaneState();
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

            EnsureSlotLaneState();
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

        Vector3 referencePosition = GetExternalReferencePosition(sourcePosition, routeViaDepositPoint);
        return TryAcceptItemNow(itemData, referencePosition);
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

        var reservations = new List<ConveyorSlotReservation>(items.Count);
        var startPositions = new List<Vector3>(items.Count);
        var targetPositions = new List<Vector3>(items.Count);
        var visualItems = new List<GameObject>(items.Count);
        Vector3 referencePosition = GetExternalReferencePosition(sourcePosition, routeViaDepositPoint);
        int batchReservedSlots = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (!TryReserveConveyorSlot(
                    items[i],
                    referencePosition,
                    out Vector3 startPosition,
                    out Vector3 targetPosition,
                    out ConveyorSlotReservation reservation,
                    i == 0,
                    batchReservedSlots))
            {
                ReleaseConveyorSlots(reservations);
                return false;
            }

            reservations.Add(reservation);
            startPositions.Add(startPosition);
            targetPositions.Add(targetPosition);
            batchReservedSlots += reservation.SlotCount;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (!CreateVisualItem(items[i], startPositions[i], out GameObject visualItem))
            {
                DestroyVisualItems(visualItems);
                ReleaseConveyorSlots(reservations);
                return false;
            }

            visualItems.Add(visualItem);
        }

        for (int i = 0; i < items.Count; i++)
        {
            storageManager.AddResource(items[i].resourceType, 1);
            AnimateVisualItem(
                visualItems[i],
                startPositions[i],
                targetPositions[i],
                reservations[i]).Forget();
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
                if (!TryReserveConveyorSlot(
                        itemData,
                        sourcePosition,
                        out Vector3 startPosition,
                        out Vector3 targetPosition,
                        out ConveyorSlotReservation reservation))
                {
                    break;
                }

                if (!CreateVisualItem(itemData, startPosition, out GameObject visualItem))
                {
                    ReleaseConveyorSlot(reservation);
                    break;
                }

                if (!playerController.Inventory.TryRemoveNextItem(out VoxelItemData removedItem))
                {
                    Debug.LogError("ConveyorExportSystem: failed to remove player inventory item.", this);
                    Destroy(visualItem);
                    ReleaseConveyorSlot(reservation);
                    break;
                }

                storageManager.AddResource(removedItem.resourceType, 1);
                AnimateVisualItem(visualItem, startPosition, targetPosition, reservation).Forget();
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
        if (Time.time < nextDroppedItemScanTime || droppedItemManager == null || !HasDroppedItemInputArea())
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
        if (!TryReserveConveyorSlot(
                itemData,
                sourcePosition,
                out Vector3 startPosition,
                out Vector3 targetPosition,
                out ConveyorSlotReservation reservation))
        {
            return;
        }

        storageManager.AddResource(itemData.resourceType, 1);
        droppedItemManager.ReturnItem(item.gameObject);

        if (!CreateVisualItem(itemData, startPosition, out GameObject visualItem))
        {
            ReleaseConveyorSlot(reservation);
            return;
        }

        AnimateVisualItem(visualItem, startPosition, targetPosition, reservation).Forget();
    }

    private bool TryAcceptItemNow(VoxelItemData itemData, Vector3 referencePosition)
    {
        if (!TryReserveConveyorSlot(
                itemData,
                referencePosition,
                out Vector3 startPosition,
                out Vector3 targetPosition,
                out ConveyorSlotReservation reservation))
        {
            return false;
        }

        if (!CreateVisualItem(itemData, startPosition, out GameObject visualItem))
        {
            ReleaseConveyorSlot(reservation);
            return false;
        }

        storageManager.AddResource(itemData.resourceType, 1);
        AnimateVisualItem(visualItem, startPosition, targetPosition, reservation).Forget();
        return true;
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

    private async UniTask AnimateVisualItem(
        GameObject visualItem,
        Vector3 sourcePosition,
        Vector3 targetPosition,
        ConveyorSlotReservation reservation)
    {
        try
        {
            await MoveVisualItemAsync(visualItem, sourcePosition, targetPosition);
        }
        finally
        {
            ReleaseConveyorSlot(reservation);
        }

        if (visualItem != null)
        {
            Destroy(visualItem);
        }
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
            visualItem.transform.position = Vector3.Lerp(sourcePosition, targetPosition, progress);
            await UniTask.Yield();
        }

        if (visualItem != null)
        {
            visualItem.transform.position = targetPosition;
        }
    }

    private bool TryReserveConveyorSlot(
        VoxelItemData itemData,
        Vector3 referencePosition,
        out Vector3 startPosition,
        out Vector3 targetPosition,
        out ConveyorSlotReservation reservation,
        bool requireEntrySlot = true,
        int entryOffsetSlots = 0)
    {
        startPosition = Vector3.zero;
        targetPosition = Vector3.zero;
        reservation = null;

        EnsureSlotLaneState();
        int laneIndex = GetLaneIndexForSource(referencePosition);
        Vector3 center = GetVisualAreaCenterByLaneIndex(laneIndex);
        startPosition = GetClosestVisualAreaPosition(referencePosition, center);
        targetPosition = GetInwardVisualPosition(center, startPosition);

        int slotCapacity = GetSlotCapacity();
        float slotSpacing = GetSlotSpacing(slotCapacity);
        int requiredSlots = GetRequiredSlotCount(itemData, slotSpacing);
        if (GetReservedSlotCount(laneIndex) + requiredSlots > slotCapacity)
        {
            return false;
        }

        if (requireEntrySlot &&
            laneIndex >= 0 &&
            laneIndex < nextSlotEntryTimes.Length &&
            Time.time < nextSlotEntryTimes[laneIndex])
        {
            return false;
        }

        OffsetFlowPositionsBySlots(ref startPosition, ref targetPosition, slotSpacing, entryOffsetSlots);

        reservation = new ConveyorSlotReservation
        {
            LaneIndex = laneIndex,
            SlotCount = requiredSlots,
        };
        activeSlotReservations.Add(reservation);

        if (laneIndex >= 0 && laneIndex < nextSlotEntryTimes.Length)
        {
            float entryBaseTime = Mathf.Max(Time.time, nextSlotEntryTimes[laneIndex]);
            nextSlotEntryTimes[laneIndex] = entryBaseTime + (slotSpacing * requiredSlots / visualMoveSpeed);
        }

        return true;
    }

    private void OffsetFlowPositionsBySlots(
        ref Vector3 startPosition,
        ref Vector3 targetPosition,
        float slotSpacing,
        int entryOffsetSlots)
    {
        if (entryOffsetSlots <= 0)
        {
            return;
        }

        Vector3 movement = targetPosition - startPosition;
        float distance = movement.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 offset = movement.normalized * Mathf.Min(distance, slotSpacing * entryOffsetSlots);
        startPosition += offset;
    }

    private void ReleaseConveyorSlot(ConveyorSlotReservation reservation)
    {
        if (reservation == null || reservation.IsReleased)
        {
            return;
        }

        reservation.IsReleased = true;
        activeSlotReservations.Remove(reservation);
    }

    private void ReleaseConveyorSlots(List<ConveyorSlotReservation> reservations)
    {
        if (reservations == null)
        {
            return;
        }

        for (int i = 0; i < reservations.Count; i++)
        {
            ReleaseConveyorSlot(reservations[i]);
        }
    }

    private void DestroyVisualItems(List<GameObject> visualItems)
    {
        if (visualItems == null)
        {
            return;
        }

        for (int i = 0; i < visualItems.Count; i++)
        {
            if (visualItems[i] != null)
            {
                Destroy(visualItems[i]);
            }
        }
    }

    private int GetReservedSlotCount(int laneIndex)
    {
        int count = 0;
        for (int i = activeSlotReservations.Count - 1; i >= 0; i--)
        {
            ConveyorSlotReservation active = activeSlotReservations[i];
            if (active == null || active.IsReleased)
            {
                activeSlotReservations.RemoveAt(i);
                continue;
            }

            if (active.LaneIndex == laneIndex)
            {
                count += active.SlotCount;
            }
        }

        return count;
    }

    private int GetSlotCapacity()
    {
        return Mathf.Max(1, conveyorWidthBlocks) * Mathf.Max(1, voxelsPerBlock);
    }

    private float GetSlotSpacing(int slotCapacity)
    {
        return Mathf.Max(0.001f, visualAreaSize.x / Mathf.Max(1, slotCapacity));
    }

    private int GetRequiredSlotCount(VoxelItemData itemData, float slotSpacing)
    {
        if (itemData == null)
        {
            return 1;
        }

        float itemWidth = GetItemWidthAlongInwardAxis(itemData);
        return Mathf.Max(1, Mathf.CeilToInt(itemWidth / Mathf.Max(0.001f, slotSpacing)));
    }

    private float GetItemWidthAlongInwardAxis(VoxelItemData itemData)
    {
        Vector3 axis = IsAxisConfigured(inwardAxis) ? inwardAxis.normalized : Vector3.right;
        Vector3 scale = itemData.scale;
        return Mathf.Abs(axis.x) * scale.x +
            Mathf.Abs(axis.y) * scale.y +
            Mathf.Abs(axis.z) * scale.z;
    }

    private void EnsureSlotLaneState()
    {
        int laneCount = GetLaneCount();
        if (nextSlotEntryTimes.Length == laneCount)
        {
            return;
        }

        nextSlotEntryTimes = new float[laneCount];
        activeSlotReservations.Clear();
    }

    private int GetLaneCount()
    {
        return visualAreaCenters != null && visualAreaCenters.Length > 0
            ? visualAreaCenters.Length
            : 1;
    }

    private int GetLaneIndexForSource(Vector3 sourcePosition)
    {
        if (visualAreaCenters == null || visualAreaCenters.Length == 0)
        {
            return 0;
        }

        int nearestIndex = 0;
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
                nearestIndex = i;
                nearestSqrDistance = sqrDistance;
            }
        }

        return nearestIndex;
    }

    private Vector3 GetVisualAreaCenterByLaneIndex(int laneIndex)
    {
        if (visualAreaCenters != null &&
            laneIndex >= 0 &&
            laneIndex < visualAreaCenters.Length &&
            visualAreaCenters[laneIndex] != null)
        {
            return visualAreaCenters[laneIndex].position;
        }

        if (visualAreaCenter != null)
        {
            return visualAreaCenter.position;
        }

        return transform.position;
    }

    private Vector3 GetExternalReferencePosition(Vector3 sourcePosition, bool routeViaDepositPoint)
    {
        return routeViaDepositPoint && externalDepositPoint != null
            ? externalDepositPoint.position
            : sourcePosition;
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
        if (!unlocked)
        {
            ClearSlotLane();
        }

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

        if (conveyorWidthBlocks <= 0)
        {
            Debug.LogError($"ConveyorExportSystem: conveyorWidthBlocks must be positive. conveyorWidthBlocks={conveyorWidthBlocks}", this);
            isValid = false;
        }

        if (voxelsPerBlock <= 0)
        {
            Debug.LogError($"ConveyorExportSystem: voxelsPerBlock must be positive. voxelsPerBlock={voxelsPerBlock}", this);
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

    private bool HasDroppedItemInputArea()
    {
        return droppedItemInputArea != null || HasAnyConfiguredCollider(droppedItemInputAreas);
    }

    private void ClearSlotLane()
    {
        activeSlotReservations.Clear();
        for (int i = 0; i < nextSlotEntryTimes.Length; i++)
        {
            nextSlotEntryTimes[i] = 0f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        playerItemTransferSpeed = Mathf.Max(0.01f, playerItemTransferSpeed);
        visualMoveSpeed = Mathf.Max(0.01f, visualMoveSpeed);
        maxDroppedItemScansPerFrame = Mathf.Max(1, maxDroppedItemScansPerFrame);
        droppedItemScanInterval = Mathf.Max(0f, droppedItemScanInterval);
        maxActiveVisualItems = Mathf.Max(1, maxActiveVisualItems);
        visualAreaSize = new Vector3(
            Mathf.Max(0.01f, visualAreaSize.x),
            Mathf.Max(0f, visualAreaSize.y),
            Mathf.Max(0.01f, visualAreaSize.z));
        inwardTargetInset = Mathf.Max(0f, inwardTargetInset);
        conveyorWidthBlocks = Mathf.Max(1, conveyorWidthBlocks);
        voxelsPerBlock = Mathf.Max(1, voxelsPerBlock);
    }
#endif
}
