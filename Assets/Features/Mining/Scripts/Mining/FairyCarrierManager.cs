using System.Collections.Generic;
using UnityEngine;

public class FairyCarrierManager : MonoBehaviour
{
    private const string CarrierUpgradeId = "garage.fairy.carrier";

    private enum FairyState
    {
        IdleAtHome,
        WaitingForSearchSlot,
        Searching,
        MovingToItem,
        CarryingToHome,
        Depositing
    }

    private sealed class FairyCarrier
    {
        public GameObject Instance;
        public GameObject CarriedItemVisual;
        public DroppedItem TargetItem;
        public VoxelItemData CarriedItem;
        public FairyState State = FairyState.IdleAtHome;
        public float NextSearchTime;
        public float SearchWaitStartedAt = -1f;
        public readonly List<Vector3> ActivePath = new List<Vector3>();
        public readonly List<DroppedItem> SearchTargets = new List<DroppedItem>();
        public readonly List<Vector3> SearchTargetPositions = new List<Vector3>();
        public MiningPassagePathfinder.NearestTargetSearch TargetSearch;
        public int ActivePathIndex;
        public Vector3 ActivePathDestination;
        public int PathVariationSeed;
        public bool HasActivePath;
        public bool PathDirty = true;
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
    [SerializeField, Min(0f)] private float offHomeSearchWaitReturnSeconds = 5f;
    [SerializeField] private Vector3 carriedItemLocalOffset = new Vector3(0f, 0.45f, 0f);

    [Header("Pathfinding")]
    [SerializeField] private MiningPassagePathOptions pathOptions = new MiningPassagePathOptions();
    [SerializeField, Min(1)] private int maxConcurrentTargetSearches = 10;
    [SerializeField, Min(1)] private int targetSearchCellsPerFrame = 256;
    [SerializeField, Min(1)] private int maxSearchTargetCandidates = 128;
    [SerializeField, Min(0f)] private float targetSearchRadius = 0f;
    [SerializeField] private float waypointArrivalDistance = 0.08f;
    [SerializeField] private float destinationRepathDistance = 0.25f;

    private readonly HashSet<DroppedItem> reservedItems = new HashSet<DroppedItem>();
    private readonly List<FairyCarrier> fairies = new List<FairyCarrier>();
    private bool isUnlocked;
    private MiningPassagePathfinder pathfinder;
    private int targetFairyCount;
    private int targetSearchSlotsRemaining;

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

        EnsureFairyInstances();
        targetSearchSlotsRemaining = Mathf.Max(1, maxConcurrentTargetSearches) - CountActiveTargetSearches();
        for (int i = 0; i < fairies.Count; i++)
        {
            FairyCarrier fairy = fairies[i];
            EnsureFairyInstance(fairy, i);
            if (fairy.Instance == null)
            {
                continue;
            }

            switch (fairy.State)
            {
                case FairyState.IdleAtHome:
                    UpdateIdleAtHome(fairy);
                    break;
                case FairyState.WaitingForSearchSlot:
                    UpdateWaitingForSearchSlot(fairy);
                    break;
                case FairyState.Searching:
                    SearchFromCurrentPosition(fairy);
                    break;
                case FairyState.MovingToItem:
                    UpdateMovingToItem(fairy);
                    break;
                case FairyState.CarryingToHome:
                    UpdateCarryingToHome(fairy);
                    break;
                case FairyState.Depositing:
                    DepositCarriedItem(fairy);
                    break;
            }
        }
    }

    private void RefreshUnlockState()
    {
        int level = GameDataPersistenceManager.Instance.GetFacilityUpgradeLevel(CarrierUpgradeId, 0);
        targetFairyCount = Mathf.Max(0, level);
        bool shouldBeUnlocked = targetFairyCount > 0;

        if (!shouldBeUnlocked)
        {
            isUnlocked = false;
            ClearAllFairies();
            reservedItems.Clear();
            return;
        }

        isUnlocked = true;
        EnsureFairyInstances();
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

    private void EnsureFairyInstances()
    {
        if (homePoint == null)
        {
            return;
        }

        while (fairies.Count > targetFairyCount)
        {
            int removeIndex = fairies.Count - 1;
            DestroyFairy(fairies[removeIndex]);
            fairies.RemoveAt(removeIndex);
        }

        while (fairies.Count < targetFairyCount)
        {
            FairyCarrier fairy = new FairyCarrier
            {
                State = FairyState.WaitingForSearchSlot,
                PathVariationSeed = CreatePathVariationSeed(fairies.Count)
            };
            fairies.Add(fairy);
            EnsureFairyInstance(fairy, fairies.Count - 1);
        }
    }

    private void EnsureFairyInstance(FairyCarrier fairy, int index)
    {
        if (fairy.Instance != null || homePoint == null)
        {
            return;
        }

        if (fairyPrefab == null)
        {
            return;
        }

        fairy.Instance = Instantiate(fairyPrefab, homePoint.position, Quaternion.identity, transform);
        fairy.Instance.name = $"FairyCarrier_{index + 1}";
        DisableFairyColliders(fairy);
    }

    private void DisableFairyColliders(FairyCarrier fairy)
    {
        Collider[] colliders = fairy.Instance.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void UpdateIdleAtHome(FairyCarrier fairy)
    {
        if (TryMoveFairyAlongPassage(fairy, homePoint.position, homeArrivalDistance, out bool arrived) &&
            arrived &&
            Time.time >= fairy.NextSearchTime)
        {
            QueueSearchFromCurrentPosition(fairy, 0f);
        }
    }

    private void UpdateWaitingForSearchSlot(FairyCarrier fairy)
    {
        EnsureSearchWaitTimerStarted(fairy);
        if (ShouldReturnHomeAfterSearchWait(fairy))
        {
            ReturnHomeAfterSearchWait(fairy);
            return;
        }

        if (Time.time < fairy.NextSearchTime)
        {
            return;
        }

        if (!TryStartTargetSearch(fairy, fairy.Instance.transform.position))
        {
            return;
        }
    }

    private void SearchFromCurrentPosition(FairyCarrier fairy)
    {
        if (fairy.TargetSearch == null)
        {
            QueueSearchFromCurrentPosition(fairy, 0f);
            return;
        }

        MiningPassageNearestTargetSearchStatus status = fairy.TargetSearch.Step(targetSearchCellsPerFrame);
        if (status == MiningPassageNearestTargetSearchStatus.Running)
        {
            return;
        }

        targetSearchSlotsRemaining++;
        if (status == MiningPassageNearestTargetSearchStatus.Found && TryAssignFoundTarget(fairy))
        {
            fairy.State = FairyState.MovingToItem;
            return;
        }

        QueueSearchFromCurrentPosition(fairy, searchInterval);
    }

    private void UpdateMovingToItem(FairyCarrier fairy)
    {
        if (!IsTargetStillAvailable(fairy.TargetItem))
        {
            ClearTargetReservation(fairy);
            QueueSearchFromCurrentPosition(fairy, 0f);
            return;
        }

        if (!TryMoveFairyAlongPassage(fairy, fairy.TargetItem.transform.position, pickupDistance, out bool arrived))
        {
            ClearTargetReservation(fairy);
            QueueSearchFromCurrentPosition(fairy, 0f);
            return;
        }

        if (!arrived)
        {
            return;
        }

        if (!TryPickupTarget(fairy))
        {
            ClearTargetReservation(fairy);
            QueueSearchFromCurrentPosition(fairy, 0f);
            return;
        }

        fairy.State = FairyState.CarryingToHome;
    }

    private void UpdateCarryingToHome(FairyCarrier fairy)
    {
        UpdateCarriedItemVisual(fairy);
        if (TryMoveFairyAlongPassage(fairy, homePoint.position, homeArrivalDistance, out bool arrived) && arrived)
        {
            fairy.State = FairyState.Depositing;
        }
    }

    private void DepositCarriedItem(FairyCarrier fairy)
    {
        if (fairy.CarriedItem != null && StorageManager.Instance != null)
        {
            StorageManager.Instance.AddResource(fairy.CarriedItem.resourceType, 1);
        }

        ClearCarriedItem(fairy);
        QueueSearchFromCurrentPosition(fairy, 0f);
    }

    private bool TryMoveFairyAlongPassage(FairyCarrier fairy, Vector3 destination, float arrivalDistance, out bool arrived)
    {
        arrived = false;
        Vector3 current = fairy.Instance.transform.position;
        float distance = Vector3.Distance(current, destination);
        if (distance <= arrivalDistance)
        {
            fairy.Instance.transform.position = destination;
            ClearActivePath(fairy);
            arrived = true;
            return true;
        }

        if (!EnsurePathTo(fairy, destination))
        {
            return false;
        }

        while (fairy.ActivePathIndex < fairy.ActivePath.Count &&
               Vector3.Distance(fairy.Instance.transform.position, fairy.ActivePath[fairy.ActivePathIndex]) <= waypointArrivalDistance)
        {
            fairy.ActivePathIndex++;
        }

        Vector3 moveTarget = fairy.ActivePathIndex < fairy.ActivePath.Count ? fairy.ActivePath[fairy.ActivePathIndex] : destination;
        fairy.Instance.transform.position = Vector3.MoveTowards(current, moveTarget, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(fairy.Instance.transform.position, destination) <= arrivalDistance)
        {
            fairy.Instance.transform.position = destination;
            ClearActivePath(fairy);
            arrived = true;
        }

        return true;
    }

    private bool EnsurePathTo(FairyCarrier fairy, Vector3 destination)
    {
        if (!EnsurePathfinder())
        {
            return false;
        }

        if (fairy.HasActivePath &&
            !fairy.PathDirty &&
            (fairy.ActivePathDestination - destination).sqrMagnitude <= destinationRepathDistance * destinationRepathDistance &&
            fairy.ActivePathIndex < fairy.ActivePath.Count)
        {
            return true;
        }

        fairy.ActivePath.Clear();
        if (!pathfinder.TryFindPath(fairy.Instance.transform.position, destination, fairy.ActivePath, out _, fairy.PathVariationSeed))
        {
            ClearActivePath(fairy);
            return false;
        }

        fairy.ActivePathDestination = destination;
        fairy.ActivePathIndex = 0;
        fairy.HasActivePath = fairy.ActivePath.Count > 0;
        fairy.PathDirty = false;
        return fairy.HasActivePath;
    }

    private void QueueSearchFromCurrentPosition(FairyCarrier fairy, float delaySeconds)
    {
        ClearTargetSearch(fairy);
        fairy.NextSearchTime = Time.time + Mathf.Max(0f, delaySeconds);
        if (fairy.State != FairyState.WaitingForSearchSlot || fairy.SearchWaitStartedAt < 0f)
        {
            fairy.SearchWaitStartedAt = Time.time;
        }

        fairy.State = FairyState.WaitingForSearchSlot;
    }

    private void EnsureSearchWaitTimerStarted(FairyCarrier fairy)
    {
        if (fairy.SearchWaitStartedAt < 0f)
        {
            fairy.SearchWaitStartedAt = Time.time;
        }
    }

    private bool ShouldReturnHomeAfterSearchWait(FairyCarrier fairy)
    {
        if (homePoint == null || fairy.Instance == null || offHomeSearchWaitReturnSeconds <= 0f)
        {
            return false;
        }

        if (IsFairyAtHome(fairy))
        {
            return false;
        }

        return Time.time - fairy.SearchWaitStartedAt >= offHomeSearchWaitReturnSeconds;
    }

    private bool IsFairyAtHome(FairyCarrier fairy)
    {
        float threshold = Mathf.Max(0f, homeArrivalDistance);
        return (fairy.Instance.transform.position - homePoint.position).sqrMagnitude <= threshold * threshold;
    }

    private void ReturnHomeAfterSearchWait(FairyCarrier fairy)
    {
        ClearTargetSearch(fairy);
        ClearActivePath(fairy);
        fairy.SearchWaitStartedAt = -1f;
        fairy.NextSearchTime = Time.time + searchInterval;
        fairy.State = FairyState.IdleAtHome;
    }

    private bool TryStartTargetSearch(FairyCarrier fairy, Vector3 origin)
    {
        if (!EnsurePathfinder())
        {
            fairy.NextSearchTime = Time.time + searchInterval;
            return false;
        }

        if (targetSearchSlotsRemaining <= 0)
        {
            return false;
        }

        BeginTargetSearch(fairy, origin);
        if (fairy.TargetSearch == null)
        {
            fairy.NextSearchTime = Time.time + searchInterval;
            return false;
        }

        targetSearchSlotsRemaining--;
        fairy.SearchWaitStartedAt = -1f;
        fairy.State = FairyState.Searching;
        return true;
    }

    private int CountActiveTargetSearches()
    {
        int count = 0;
        for (int i = 0; i < fairies.Count; i++)
        {
            if (fairies[i].State == FairyState.Searching && fairies[i].TargetSearch != null)
            {
                count++;
            }
        }

        return count;
    }

    private void BeginTargetSearch(FairyCarrier fairy, Vector3 origin)
    {
        ClearTargetSearch(fairy);

        DroppedItemManager itemManager = DroppedItemManager.Instance;
        if (itemManager == null)
        {
            return;
        }

        reservedItems.RemoveWhere(IsReservedItemInvalid);
        itemManager.CollectActiveItemsNear(
            origin,
            targetSearchRadius,
            fairy.SearchTargets,
            maxSearchTargetCandidates,
            reservedItems);

        for (int i = fairy.SearchTargets.Count - 1; i >= 0; i--)
        {
            DroppedItem item = fairy.SearchTargets[i];
            if (!IsTargetCandidateAvailable(item))
            {
                fairy.SearchTargets.RemoveAt(i);
            }
        }

        for (int i = 0; i < fairy.SearchTargets.Count; i++)
        {
            DroppedItem item = fairy.SearchTargets[i];
            fairy.SearchTargetPositions.Add(item.transform.position);
        }

        if (fairy.SearchTargetPositions.Count == 0)
        {
            return;
        }

        fairy.TargetSearch = pathfinder.BeginNearestTargetSearch(origin, fairy.SearchTargetPositions, fairy.PathVariationSeed);
    }

    private bool TryAssignFoundTarget(FairyCarrier fairy)
    {
        if (fairy.TargetSearch == null)
        {
            return false;
        }

        int foundIndex = fairy.TargetSearch.FoundTargetIndex;
        if (foundIndex < 0 || foundIndex >= fairy.SearchTargets.Count)
        {
            ClearTargetSearch(fairy);
            return false;
        }

        DroppedItem foundTarget = fairy.SearchTargets[foundIndex];
        if (!IsTargetStillAvailable(foundTarget) || reservedItems.Contains(foundTarget))
        {
            ClearTargetSearch(fairy);
            return false;
        }

        fairy.ActivePath.Clear();
        if (!fairy.TargetSearch.TryBuildPath(fairy.ActivePath))
        {
            ClearTargetSearch(fairy);
            return false;
        }

        fairy.TargetItem = foundTarget;
        reservedItems.Add(fairy.TargetItem);
        fairy.ActivePathDestination = foundTarget.transform.position;
        fairy.ActivePathIndex = 0;
        fairy.HasActivePath = fairy.ActivePath.Count > 0;
        fairy.PathDirty = false;
        ClearTargetSearch(fairy);
        return fairy.HasActivePath;
    }

    private bool TryPickupTarget(FairyCarrier fairy)
    {
        if (!IsTargetStillAvailable(fairy.TargetItem))
        {
            return false;
        }

        if (!VoxelItemData.TryCreateFromDroppedItem(fairy.TargetItem, out VoxelItemData itemData))
        {
            return false;
        }

        fairy.CarriedItem = itemData;
        DroppedItem pickedItem = fairy.TargetItem;
        ClearTargetReservation(fairy);
        DroppedItemManager.Instance.ReturnItem(pickedItem.gameObject);
        CreateCarriedItemVisual(fairy);
        return true;
    }

    private void CreateCarriedItemVisual(FairyCarrier fairy)
    {
        ClearCarriedItemVisual(fairy);
        if (fairy.CarriedItem == null)
        {
            return;
        }

        if (VoxelItemVisualUtility.TryCreateAnimationItem(
                fairy.Instance.transform.position,
                fairy.Instance.transform,
                fairy.CarriedItem,
                terrainDataManager,
                "FairyCarrierManager",
                out GameObject visual))
        {
            fairy.CarriedItemVisual = visual;
            UpdateCarriedItemVisual(fairy);
        }
    }

    private void UpdateCarriedItemVisual(FairyCarrier fairy)
    {
        if (fairy.CarriedItemVisual != null)
        {
            fairy.CarriedItemVisual.transform.localPosition = carriedItemLocalOffset;
            fairy.CarriedItemVisual.transform.Rotate(0f, 180f * Time.deltaTime, 0f);
        }
    }

    private void ClearTargetReservation(FairyCarrier fairy)
    {
        if (fairy.TargetItem != null)
        {
            reservedItems.Remove(fairy.TargetItem);
            fairy.TargetItem = null;
        }

        ClearActivePath(fairy);
    }

    private void ClearCarriedItem(FairyCarrier fairy)
    {
        fairy.CarriedItem = null;
        ClearCarriedItemVisual(fairy);
    }

    private void ClearCarriedItemVisual(FairyCarrier fairy)
    {
        if (fairy.CarriedItemVisual != null)
        {
            Destroy(fairy.CarriedItemVisual);
            fairy.CarriedItemVisual = null;
        }
    }

    private bool IsTargetStillAvailable(DroppedItem item)
    {
        if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
        {
            return false;
        }

        DroppedItemManager itemManager = DroppedItemManager.Instance;
        return itemManager != null && itemManager.ContainsActiveItem(item);
    }

    private bool IsTargetCandidateAvailable(DroppedItem item)
    {
        return item != null && item.gameObject != null && item.gameObject.activeInHierarchy;
    }

    private bool IsReservedItemInvalid(DroppedItem item)
    {
        return item == null || item.gameObject == null || !item.gameObject.activeInHierarchy;
    }

    private int CreatePathVariationSeed(int fairyIndex)
    {
        unchecked
        {
            int seed = 7919;
            seed = seed * 31 + fairyIndex;
            seed = seed * 31 + (homePoint != null ? homePoint.GetInstanceID() : 0);
            seed = seed * 31 + GetInstanceID();
            return seed == 0 ? 1 : seed;
        }
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
        MarkAllPathsDirty();
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

        MarkAllPathsDirty();
    }

    private void MarkAllPathsDirty()
    {
        for (int i = 0; i < fairies.Count; i++)
        {
            fairies[i].PathDirty = true;
            ClearTargetSearch(fairies[i]);
            if (fairies[i].State == FairyState.Searching)
            {
                fairies[i].State = FairyState.WaitingForSearchSlot;
            }
        }
    }

    private void ClearActivePath(FairyCarrier fairy)
    {
        fairy.ActivePath.Clear();
        fairy.ActivePathIndex = 0;
        fairy.HasActivePath = false;
        fairy.PathDirty = true;
    }

    private void ClearTargetSearch(FairyCarrier fairy)
    {
        fairy.TargetSearch = null;
        fairy.SearchTargets.Clear();
        fairy.SearchTargetPositions.Clear();
    }

    private void DestroyFairy(FairyCarrier fairy)
    {
        ClearTargetReservation(fairy);
        ClearCarriedItem(fairy);
        ClearActivePath(fairy);
        ClearTargetSearch(fairy);
        if (fairy.Instance != null)
        {
            Destroy(fairy.Instance);
            fairy.Instance = null;
        }

        fairy.State = FairyState.IdleAtHome;
    }

    private void ClearAllFairies()
    {
        for (int i = 0; i < fairies.Count; i++)
        {
            DestroyFairy(fairies[i]);
        }

        fairies.Clear();
    }
}
