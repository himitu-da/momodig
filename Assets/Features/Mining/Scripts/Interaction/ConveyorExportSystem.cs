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

    private sealed class ConveyorVisualItem
    {
        public GameObject VisualItem;
        public ConveyorSlotLane.Reservation Reservation;
        public int LaneIndex;
        public Vector3 LateralOffset;
    }

    private readonly ConveyorSlotLane slotLane = new ConveyorSlotLane();
    private readonly List<ConveyorVisualItem> activeVisualItems = new List<ConveyorVisualItem>();
    private readonly Dictionary<int, Vector3> previousDroppedItemPositions = new Dictionary<int, Vector3>();
    private bool isUnlocked;
    private bool isTransferringPlayerItems;
    private float nextDroppedItemScanTime;
    private int nextDroppedItemScanIndex;
    private bool missingPersistenceLogged;
    private Vector3 previousPlayerPosition;
    private bool hasPreviousPlayerPosition;

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
                CapturePlayerPosition();
                return;
            }

            EnsureSlotLaneState();
            slotLane.Advance(Time.deltaTime, CanAdvanceSlotLane());
            UpdateVisualItems();
            StartPlayerTransferIfNeeded();
            ProcessDroppedItemsInArea();
            CapturePlayerPosition();
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

        var pendingItems = new List<ConveyorVisualItem>(items.Count);
        Vector3 referencePosition = GetExternalReferencePosition(sourcePosition, routeViaDepositPoint);
        EnsureSlotLaneState();

        for (int i = 0; i < items.Count; i++)
        {
            ConveyorVisualItem pendingItem;
            bool prepared;
            if (items.Count > 1)
            {
                prepared = TryPrepareConveyorItemInLaneRange(
                    items[i],
                    referencePosition,
                    0f,
                    slotLane.LaneLength,
                    out pendingItem);
            }
            else
            {
                prepared = TryPrepareConveyorItem(
                    items[i],
                    referencePosition,
                    referencePosition,
                    out pendingItem);
            }

            if (!prepared)
            {
                ReleaseConveyorVisualItems(pendingItems, true);
                return false;
            }

            pendingItems.Add(pendingItem);
        }

        for (int i = 0; i < items.Count; i++)
        {
            storageManager.AddResource(items[i].resourceType, 1);
            CommitConveyorVisualItem(pendingItems[i]);
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
                GetPlayerSweepPositions(out Vector3 previousPosition, out Vector3 currentPosition);
                int acceptedThisPass = 0;
                int maxAcceptedThisPass = Mathf.Max(1, slotLane.SlotCapacity);
                while (acceptedThisPass < maxAcceptedThisPass &&
                       isUnlocked &&
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

                    if (!TryPrepareConveyorItem(
                            itemData,
                            previousPosition,
                            currentPosition,
                            out ConveyorVisualItem pendingItem))
                    {
                        break;
                    }

                    if (!playerController.Inventory.TryRemoveNextItem(out VoxelItemData removedItem))
                    {
                        Debug.LogError("ConveyorExportSystem: failed to remove player inventory item.", this);
                        ReleaseConveyorVisualItem(pendingItem, true);
                        break;
                    }

                    storageManager.AddResource(removedItem.resourceType, 1);
                    CommitConveyorVisualItem(pendingItem);
                    acceptedThisPass++;
                }

                if (acceptedThisPass == 0)
                {
                    break;
                }

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

                if (item == null)
                {
                    continue;
                }

                Vector3 currentPosition = item.transform.position;
                Vector3 previousPosition = GetPreviousDroppedItemPosition(item, currentPosition);
                if (!IsDroppedItemInInputArea(item))
                {
                    TrackDroppedItemPosition(item, currentPosition);
                    continue;
                }

                if (!ExportDroppedItem(item, previousPosition, currentPosition))
                {
                    TrackDroppedItemPosition(item, currentPosition);
                }
            }
        }
    }

    private bool ExportDroppedItem(DroppedItem item, Vector3 previousPosition, Vector3 currentPosition)
    {
        if (item == null || !VoxelItemData.TryCreateFromDroppedItem(item, out VoxelItemData itemData))
        {
            return false;
        }

        if (!CanExportItem(itemData, "ConveyorExportSystem.ExportDroppedItem"))
        {
            return false;
        }

        if (!TryPrepareConveyorItem(
                itemData,
                previousPosition,
                currentPosition,
                out ConveyorVisualItem pendingItem))
        {
            return false;
        }

        storageManager.AddResource(itemData.resourceType, 1);
        droppedItemManager.ReturnItem(item.gameObject);
        ClearDroppedItemPosition(item);
        CommitConveyorVisualItem(pendingItem);
        return true;
    }

    private bool TryAcceptItemNow(VoxelItemData itemData, Vector3 referencePosition)
    {
        if (!TryPrepareConveyorItem(
                itemData,
                referencePosition,
                referencePosition,
                out ConveyorVisualItem pendingItem))
        {
            return false;
        }

        storageManager.AddResource(itemData.resourceType, 1);
        CommitConveyorVisualItem(pendingItem);
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

    private bool TryPrepareConveyorItem(
        VoxelItemData itemData,
        Vector3 previousReferencePosition,
        Vector3 currentReferencePosition,
        out ConveyorVisualItem pendingItem)
    {
        pendingItem = null;
        if (!TryReserveConveyorSlot(
                itemData,
                previousReferencePosition,
                currentReferencePosition,
                out ConveyorSlotLane.Reservation reservation,
                out int laneIndex,
                out Vector3 lateralOffset,
                out Vector3 visualPosition))
        {
            return false;
        }

        if (!CreateVisualItem(itemData, visualPosition, out GameObject visualItem))
        {
            slotLane.Release(reservation);
            return false;
        }

        pendingItem = new ConveyorVisualItem
        {
            VisualItem = visualItem,
            Reservation = reservation,
            LaneIndex = laneIndex,
            LateralOffset = lateralOffset,
        };
        return true;
    }

    private bool TryPrepareConveyorItemInLaneRange(
        VoxelItemData itemData,
        Vector3 referencePosition,
        float sweptFromDistance,
        float sweptToDistance,
        out ConveyorVisualItem pendingItem)
    {
        pendingItem = null;
        if (!TryReserveConveyorSlotInLaneRange(
                itemData,
                referencePosition,
                sweptFromDistance,
                sweptToDistance,
                out ConveyorSlotLane.Reservation reservation,
                out int laneIndex,
                out Vector3 lateralOffset,
                out Vector3 visualPosition))
        {
            return false;
        }

        if (!CreateVisualItem(itemData, visualPosition, out GameObject visualItem))
        {
            slotLane.Release(reservation);
            return false;
        }

        pendingItem = new ConveyorVisualItem
        {
            VisualItem = visualItem,
            Reservation = reservation,
            LaneIndex = laneIndex,
            LateralOffset = lateralOffset,
        };
        return true;
    }

    private bool CreateVisualItem(VoxelItemData itemData, Vector3 position, out GameObject visualItem)
    {
        return VoxelItemVisualUtility.TryCreateAnimationItem(
            position,
            transform,
            itemData,
            terrainDataManager,
            "ConveyorExportSystem",
            out visualItem);
    }

    private void CommitConveyorVisualItem(ConveyorVisualItem item)
    {
        if (item == null)
        {
            return;
        }

        if (item.VisualItem != null && item.Reservation != null && !item.Reservation.IsReleased)
        {
            item.VisualItem.transform.position = GetReservationWorldPosition(item.Reservation, item.LateralOffset);
        }

        activeVisualItems.Add(item);
        TrimActiveVisualItems();
    }

    private void UpdateVisualItems()
    {
        for (int i = activeVisualItems.Count - 1; i >= 0; i--)
        {
            ConveyorVisualItem active = activeVisualItems[i];
            if (active == null ||
                active.Reservation == null ||
                active.Reservation.IsReleased ||
                active.VisualItem == null)
            {
                ReleaseConveyorVisualItem(active, true);
                activeVisualItems.RemoveAt(i);
                continue;
            }

            active.VisualItem.transform.position =
                GetReservationWorldPosition(active.Reservation, active.LateralOffset);
        }
    }

    private void TrimActiveVisualItems()
    {
        for (int i = activeVisualItems.Count - 1; i >= 0; i--)
        {
            if (activeVisualItems[i] == null || activeVisualItems[i].VisualItem == null)
            {
                ReleaseConveyorVisualItem(activeVisualItems[i], true);
                activeVisualItems.RemoveAt(i);
            }
        }

        while (activeVisualItems.Count > maxActiveVisualItems)
        {
            ConveyorVisualItem oldest = activeVisualItems[0];
            activeVisualItems.RemoveAt(0);
            ReleaseConveyorVisualItem(oldest, true);
        }
    }

    private void ReleaseConveyorVisualItem(ConveyorVisualItem item, bool destroyVisual)
    {
        if (item == null)
        {
            return;
        }

        if (destroyVisual && item.VisualItem != null)
        {
            Destroy(item.VisualItem);
        }

        slotLane.Release(item.Reservation);
    }

    private void ReleaseConveyorVisualItems(List<ConveyorVisualItem> items, bool destroyVisuals)
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ReleaseConveyorVisualItem(items[i], destroyVisuals);
        }
    }

    private void DestroyAllActiveVisualItems(bool releaseReservations)
    {
        for (int i = 0; i < activeVisualItems.Count; i++)
        {
            ConveyorVisualItem active = activeVisualItems[i];
            if (active == null)
            {
                continue;
            }

            if (active.VisualItem != null)
            {
                Destroy(active.VisualItem);
            }

            if (releaseReservations)
            {
                slotLane.Release(active.Reservation);
            }
        }

        activeVisualItems.Clear();
    }

    private bool TryReserveConveyorSlot(
        VoxelItemData itemData,
        Vector3 previousReferencePosition,
        Vector3 currentReferencePosition,
        out ConveyorSlotLane.Reservation reservation,
        out int laneIndex,
        out Vector3 lateralOffset,
        out Vector3 visualPosition)
    {
        reservation = null;
        laneIndex = 0;
        lateralOffset = Vector3.zero;
        visualPosition = Vector3.zero;

        EnsureSlotLaneState();
        laneIndex = GetLaneIndexForSource(currentReferencePosition);
        Vector3 center = GetVisualAreaCenterByLaneIndex(laneIndex);
        Vector3 flowDirection = GetFlowDirectionForLane(center);
        float previousDistance = GetLaneDistance(previousReferencePosition, center, flowDirection);
        float currentDistance = GetLaneDistance(currentReferencePosition, center, flowDirection);
        int requiredSlots = GetRequiredSlotCount(itemData, slotLane.SlotSpacing);
        if (!slotLane.TryReserve(laneIndex, previousDistance, currentDistance, requiredSlots, out reservation))
        {
            return false;
        }

        lateralOffset = GetLateralOffset(currentReferencePosition, center, flowDirection);
        visualPosition = GetReservationWorldPosition(reservation, lateralOffset);
        return true;
    }

    private bool TryReserveConveyorSlotInLaneRange(
        VoxelItemData itemData,
        Vector3 referencePosition,
        float sweptFromDistance,
        float sweptToDistance,
        out ConveyorSlotLane.Reservation reservation,
        out int laneIndex,
        out Vector3 lateralOffset,
        out Vector3 visualPosition)
    {
        reservation = null;
        laneIndex = 0;
        lateralOffset = Vector3.zero;
        visualPosition = Vector3.zero;

        EnsureSlotLaneState();
        laneIndex = GetLaneIndexForSource(referencePosition);
        Vector3 center = GetVisualAreaCenterByLaneIndex(laneIndex);
        Vector3 flowDirection = GetFlowDirectionForLane(center);
        int requiredSlots = GetRequiredSlotCount(itemData, slotLane.SlotSpacing);
        if (!slotLane.TryReserve(laneIndex, sweptFromDistance, sweptToDistance, requiredSlots, out reservation))
        {
            return false;
        }

        lateralOffset = GetLateralOffset(referencePosition, center, flowDirection);
        visualPosition = GetReservationWorldPosition(reservation, lateralOffset);
        return true;
    }

    private int GetSlotCapacity()
    {
        return Mathf.Max(1, conveyorWidthBlocks) * Mathf.Max(1, voxelsPerBlock);
    }

    private float GetLaneLength()
    {
        return Mathf.Max(0.001f, GetVisualAxisLength());
    }

    private int GetRequiredSlotCount(VoxelItemData itemData, float slotSpacing)
    {
        if (itemData == null)
        {
            return 1;
        }

        float itemWidth = GetItemWidthAlongInwardAxis(itemData);
        return Mathf.Max(1, Mathf.CeilToInt((itemWidth - 0.0001f) / Mathf.Max(0.001f, slotSpacing)));
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
        if (slotLane.Configure(laneCount, GetLaneLength(), GetSlotCapacity(), visualMoveSpeed))
        {
            DestroyAllActiveVisualItems(false);
        }
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

    private bool CanAdvanceSlotLane()
    {
        return true;
    }

    private Vector3 GetReservationWorldPosition(
        ConveyorSlotLane.Reservation reservation,
        Vector3 lateralOffset)
    {
        if (reservation == null)
        {
            return transform.position;
        }

        Vector3 center = GetVisualAreaCenterByLaneIndex(reservation.LaneIndex);
        Vector3 flowDirection = GetFlowDirectionForLane(center);
        float distance = slotLane.GetReservationCenterDistance(reservation);
        return GetLaneWorldPosition(center, flowDirection, distance, lateralOffset);
    }

    private Vector3 GetLaneWorldPosition(
        Vector3 center,
        Vector3 flowDirection,
        float laneDistance,
        Vector3 lateralOffset)
    {
        float laneLength = Mathf.Max(0.001f, slotLane.LaneLength);
        float visualLength = Mathf.Max(0.001f, GetVisualAxisLength() - inwardTargetInset);
        float normalizedDistance = Mathf.Clamp01(laneDistance / laneLength);
        Vector3 startPosition = center - flowDirection * (GetVisualAxisLength() * 0.5f);
        return startPosition + flowDirection * (normalizedDistance * visualLength) + lateralOffset;
    }

    private Vector3 GetFlowDirectionForLane(Vector3 center)
    {
        Vector3 axis = IsAxisConfigured(inwardAxis) ? inwardAxis.normalized : Vector3.right;
        if (!moveVisualItemsInward)
        {
            return axis;
        }

        Vector3 referencePosition = externalDepositPoint != null ? externalDepositPoint.position : transform.position;
        float side = Vector3.Dot(center - referencePosition, axis);
        if (Mathf.Abs(side) <= 0.001f)
        {
            return axis;
        }

        return side < 0f ? axis : -axis;
    }

    private float GetLaneDistance(Vector3 worldPosition, Vector3 center, Vector3 flowDirection)
    {
        Vector3 startPosition = center - flowDirection * (GetVisualAxisLength() * 0.5f);
        float distance = Vector3.Dot(worldPosition - startPosition, flowDirection);
        return Mathf.Clamp(distance, 0f, slotLane.LaneLength);
    }

    private Vector3 GetLateralOffset(Vector3 sourcePosition, Vector3 center, Vector3 flowDirection)
    {
        Vector3 closestPosition = GetClosestVisualAreaPosition(sourcePosition, center);
        Vector3 centerOffset = closestPosition - center;
        return centerOffset - flowDirection * Vector3.Dot(centerOffset, flowDirection);
    }

    private float GetVisualAxisLength()
    {
        Vector3 axis = IsAxisConfigured(inwardAxis) ? inwardAxis.normalized : Vector3.right;
        return Mathf.Max(
            0.001f,
            Mathf.Abs(axis.x) * visualAreaSize.x +
            Mathf.Abs(axis.y) * visualAreaSize.y +
            Mathf.Abs(axis.z) * visualAreaSize.z);
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

    private bool IsAxisConfigured(Vector3 axis)
    {
        return axis.sqrMagnitude > 0.0001f;
    }

    private void CapturePlayerPosition()
    {
        if (playerController == null)
        {
            hasPreviousPlayerPosition = false;
            return;
        }

        previousPlayerPosition = playerController.transform.position;
        hasPreviousPlayerPosition = true;
    }

    private void GetPlayerSweepPositions(out Vector3 previousPosition, out Vector3 currentPosition)
    {
        currentPosition = playerController != null ? playerController.transform.position : transform.position;
        previousPosition = hasPreviousPlayerPosition ? previousPlayerPosition : currentPosition;
    }

    private Vector3 GetPreviousDroppedItemPosition(DroppedItem item, Vector3 currentPosition)
    {
        if (item == null || item.gameObject == null)
        {
            return currentPosition;
        }

        int instanceId = item.gameObject.GetInstanceID();
        return previousDroppedItemPositions.TryGetValue(instanceId, out Vector3 previousPosition)
            ? previousPosition
            : currentPosition;
    }

    private void TrackDroppedItemPosition(DroppedItem item, Vector3 position)
    {
        if (item == null || item.gameObject == null)
        {
            return;
        }

        previousDroppedItemPositions[item.gameObject.GetInstanceID()] = position;
    }

    private void ClearDroppedItemPosition(DroppedItem item)
    {
        if (item == null || item.gameObject == null)
        {
            return;
        }

        previousDroppedItemPositions.Remove(item.gameObject.GetInstanceID());
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
        DestroyAllActiveVisualItems(true);
        slotLane.Clear();
        previousDroppedItemPositions.Clear();
        hasPreviousPlayerPosition = false;
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
