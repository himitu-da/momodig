using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;

public class DroppedItemManager : MonoBehaviour, IItemManager, IGameSceneTransitionHandler
{
    private static readonly ProfilerMarker ProcessQueuedDropSpawnsMarker =
        new ProfilerMarker("DroppedItemManager.ProcessQueuedDropSpawns");
    private static readonly ProfilerMarker SpawnQueuedDropMarker =
        new ProfilerMarker("DroppedItemManager.SpawnQueuedDrop");

    [Header("アイテム管理設定")]
    [SerializeField] private float _wakeUpRadiusMultiplier = 3f; // アイテムの半径に対する起床範囲の倍率

    [Header("Drop Spawn Queue")]
    [SerializeField] private int maxQueuedDropSpawnsPerFrame = 32;
    [SerializeField] private float maxQueuedDropSpawnMilliseconds = 2f;
    [SerializeField] private int dropQueueWarningThreshold = 512;
    [SerializeField] private bool showDropQueueDebugInfo = false;
    
    // インターフェース実装用プロパティ
    [Header("Voxel Solidification")]
    [SerializeField] private bool enableSolidification = true;
    [SerializeField] private float solidifyAfterSleepingSeconds = 8f;
    [SerializeField] private int maxSolidificationsPerCheck = 4;
    [SerializeField] private int candidateLookupCount = 8;
    [SerializeField] private int candidateMaxBlockRadius = 8;

    [Header("Anchored Drop Physics")]
    [SerializeField] private float angularSleepThreshold = 0.25f;
    [SerializeField] private float minSettlingSeconds = 0.4f;
    [SerializeField] private float anchoredCandidateLinearSpeed = 0.35f;
    [SerializeField] private float anchoredCandidateAngularSpeed = 1.0f;
    [SerializeField] private float settlingAbortLinearSpeed = 1.25f;
    [SerializeField] private float settlingAbortAngularSpeed = 4.0f;
    [SerializeField] private float itemSupportMinOverlapRatio = 0.08f;
    [SerializeField] private float itemSupportProbeDepthRatio = 0.75f;
    [SerializeField] private float itemSupportMaxPenetrationRatio = 0.65f;
    [SerializeField] private float itemSupportHorizontalPaddingRatio = 0.25f;
    [SerializeField] private float upwardWakeHeightRatio = 3f;
    [SerializeField] private float upwardWakePaddingRatio = 0.5f;
    [SerializeField] private int maxUpwardWakePerEvent = 64;
    [SerializeField] private float supportProbeRatio = 0.15f;
    [SerializeField] private float supportMaxGapRatio = 0.20f;
    [SerializeField] private float supportMaxPenetrationRatio = 0.05f;
    [SerializeField] private int maxSupportCells = 8;
    [SerializeField] private float spawnSleepCooldownSeconds = 0.75f;
    [SerializeField] private float miningPreWakeCooldownSeconds = 0.75f;
    [SerializeField] private float forceWakeCooldownSeconds = 1.5f;
    [SerializeField] private float supportLostWakeCooldownSeconds = 0.75f;
    [SerializeField] private float pickupWakeCooldownSeconds = 0.25f;
    [SerializeField] private string dynamicDropLayerName = "DroppedItem";
    [SerializeField] private string anchoredDropLayerName = "AnchoredDrop";
    [SerializeField] private string toolLayerName = "Tool";
    [SerializeField] private bool debugTintAnchoredItems = true;
    [SerializeField] private bool showAnchoredDebugInfo = false;
    [SerializeField] private float debugStateLogInterval = 2f;

    public float WakeUpRadiusMultiplier => _wakeUpRadiusMultiplier;
    
    private static DroppedItemManager _instance;
    public static DroppedItemManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DroppedItemManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    // オブジェクトプーリング関連
    private Dictionary<GameObject, Queue<GameObject>> itemPools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> instancePrefabMap = new Dictionary<GameObject, GameObject>();

    // アクティブなアイテムの管理リスト
    private List<DroppedItem> activeItems = new List<DroppedItem>();
    private readonly List<DroppedItem> fluidTickCandidates = new List<DroppedItem>();
    private readonly HashSet<DroppedItem> fluidTickCandidateSet = new HashSet<DroppedItem>();

    private enum DropState
    {
        Dynamic,
        Settling,
        Anchored,
        Invalidated,
        Solidified
    }

    private enum WakeReason
    {
        Manual,
        MiningPreWake,
        Force,
        SupportLost,
        Pickup,
        Pool,
        Load
    }

    // 各アイテムの状態を管理
    private struct SolidificationCandidate
    {
        public VoxelCellKey key;
        public Vector3 worldPosition;
        public float distanceSqr;
    }

    private class SolidificationReservation
    {
        public DroppedItem item;
        public float startedAt;
    }

    private struct DropSpawnRequest
    {
        public Vector3 position;
        public BlockData blockData;
        public bool useTexture1;
        public int voxelX;
        public int voxelY;
        public int voxelZ;
        public int voxelsPerBlock;
        public float voxelWorldSize;
        public VoxelTextureExtractor textureExtractor;
        public MiningInfo miningInfo;
        public bool applyInitialForce;
    }

    private class ItemState
    {
        public DropState state = DropState.Dynamic;
        public Queue<float> velocityHistory = new Queue<float>();
        public float sleepCooldownTimer = 0f;
        public float settlingSince = -1f;
        public float solidificationStartedAt = -1f;
        public bool hasSolidificationReservation = false;
        public VoxelCellKey reservedCell;
        public readonly List<VoxelCellKey> supportCells = new List<VoxelCellKey>();
        public int lastInvalidationVersion = -1;
    }
    private Dictionary<DroppedItem, ItemState> itemStates = new Dictionary<DroppedItem, ItemState>();
    private readonly Queue<DropSpawnRequest> queuedDropSpawns = new Queue<DropSpawnRequest>();
    private Dictionary<VoxelCellKey, SolidificationReservation> solidificationReservations =
        new Dictionary<VoxelCellKey, SolidificationReservation>();
    private readonly Dictionary<VoxelCellKey, HashSet<DroppedItem>> anchoredBySupportCell =
        new Dictionary<VoxelCellKey, HashSet<DroppedItem>>();
    private readonly Queue<DroppedItem> invalidatedQueue = new Queue<DroppedItem>();
    private readonly HashSet<DroppedItem> invalidatedItems = new HashSet<DroppedItem>();
    private readonly HashSet<DroppedItem> reusableWakeSet = new HashSet<DroppedItem>();
    private readonly Dictionary<DroppedItem, float> queuedInitialForceSkipUntil = new Dictionary<DroppedItem, float>();
    private readonly List<DroppedItem> reusableQueuedForceSkipRemovals = new List<DroppedItem>(128);
    private readonly List<VoxelCellKey> reusableSupportCells = new List<VoxelCellKey>(8);
    private readonly List<DroppedItem> upwardWakeList = new List<DroppedItem>(64);
    private readonly HashSet<DroppedItem> upwardWakeVisited = new HashSet<DroppedItem>();
    private readonly List<float> reusableCandidateDistances = new List<float>(128);
    private readonly Vector3[] supportSampleBuffer = new Vector3[5];
    private readonly Collider[] overlapBuffer = new Collider[2048];
    private TerrainManager cachedTerrainManager;
    private int dynamicDropLayer = -1;
    private int anchoredDropLayer = -1;
    private int toolLayer = -1;
    private bool dropQueueThresholdWarningIssued;
    private bool dropQueueSettingsErrorLogged;
    private bool missingFluidManagerLogged;

    // 静止・起床ロジックの定数
    private const float SleepCheckInterval = 0.2f; // 0.1秒ごとにチェック
    private const float SleepVelocityThreshold = 0.1f;
    private const int VelocityHistorySize = 3;
    private const float QueuedInitialForceDuplicateSkipSeconds = 0.25f;

    private float sleepCheckTimer = 0f;
    private float solidificationCheckTimer = 0f;
    private float nextAnchoredDebugLogTime = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            ResolveDropLayers();
        }
    }

    private Dictionary<Vector3Int, List<DroppedItemData>> itemsByChunk = new Dictionary<Vector3Int, List<DroppedItemData>>();

    void Start()
    {
        ResolveDropLayers();
        SubscribeTerrainEvents();
        // 永続化データからアイテムをロード
        PrepareItemLoading();
    }

    void OnDestroy()
    {
        UnsubscribeTerrainEvents();
        // シーン終了時にアイテムをセーブ
        SaveItems();
    }

    public void OnBeforeContentSceneUnload(string nextSceneName)
    {
        SaveItems();
        UnsubscribeTerrainEvents();
    }

    public void OnAfterContentSceneLoad(string previousSceneName)
    {
        UnsubscribeTerrainEvents();
        cachedTerrainManager = null;
        SubscribeTerrainEvents();
    }

    private void ResolveDropLayers()
    {
        dynamicDropLayer = LayerMask.NameToLayer(dynamicDropLayerName);
        anchoredDropLayer = LayerMask.NameToLayer(anchoredDropLayerName);
        toolLayer = LayerMask.NameToLayer(toolLayerName);

        if (dynamicDropLayer < 0)
        {
            dynamicDropLayer = LayerMask.NameToLayer("DroppedItem");
        }

        if (anchoredDropLayer < 0)
        {
            anchoredDropLayer = dynamicDropLayer;
            if (showAnchoredDebugInfo)
            {
                Debug.LogWarning("DroppedItemManager: AnchoredDrop layer was not found. Anchored drops will stay on DroppedItem layer.");
            }
        }

        ConfigureAnchoredLayerCollisions();
    }

    private void ConfigureAnchoredLayerCollisions()
    {
        if (anchoredDropLayer < 0 || anchoredDropLayer == dynamicDropLayer)
        {
            return;
        }

        if (!showAnchoredDebugInfo)
        {
            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0 || dynamicDropLayer < 0 || toolLayer < 0)
        {
            Debug.LogWarning("DroppedItemManager: AnchoredDrop collision layers are not fully configured in TagManager.");
        }
    }

    private void SubscribeTerrainEvents()
    {
        TerrainManager terrainManager = ResolveTerrainManager();
        if (terrainManager == null || terrainManager.VoxelManager == null)
        {
            return;
        }

        terrainManager.VoxelManager.TerrainCellsChanged -= OnTerrainCellsChanged;
        terrainManager.VoxelManager.TerrainCellsChanged += OnTerrainCellsChanged;
    }

    private void UnsubscribeTerrainEvents()
    {
        TerrainManager terrainManager = cachedTerrainManager;
        if (terrainManager != null && terrainManager.VoxelManager != null)
        {
            terrainManager.VoxelManager.TerrainCellsChanged -= OnTerrainCellsChanged;
        }
    }

    void Update()
    {
        CleanupExpiredQueuedInitialForceSkips();
        ProcessQueuedDropSpawns();
        ProcessInvalidatedQueue(32);
        solidificationCheckTimer += Time.deltaTime;
        if (solidificationCheckTimer >= SleepCheckInterval)
        {
            solidificationCheckTimer = 0f;
            ProcessSolidificationCandidates();
        }

        sleepCheckTimer += Time.deltaTime;
        if (sleepCheckTimer < SleepCheckInterval)
        {
            return;
        }
        sleepCheckTimer = 0f;
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            DroppedItem item = activeItems[i];

            if (item == null || !item.gameObject.activeInHierarchy || !itemStates.ContainsKey(item))
            {
                if (item != null)
                {
                    queuedInitialForceSkipUntil.Remove(item);
                    RemoveFluidTickCandidate(item);
                    if (itemStates.TryGetValue(item, out ItemState removedState))
                    {
                        RemoveFromAnchoredIndexes(item, removedState);
                        ReleaseSolidificationReservation(item);
                        itemStates.Remove(item);
                    }
                }
                activeItems.RemoveAt(i);
                continue;
            }
            
            // コンポーネントが無効になっていたら、強制的に有効化する
            if (!item.enabled)
            {
                item.enabled = true;
            }

            if (item.rb == null) continue;

            ItemState state = itemStates[item];

            // スリープクールダウンの処理
            if (state.sleepCooldownTimer > 0)
            {
                state.sleepCooldownTimer -= SleepCheckInterval;
            }

            UpdateItemMotionState(item, state);
        }

        LogDropStateCountsIfNeeded();
    }

    void FixedUpdate()
    {
        float fixedDeltaTime = Time.fixedDeltaTime;
        float currentTime = Time.time;
        for (int i = fluidTickCandidates.Count - 1; i >= 0; i--)
        {
            DroppedItem item = fluidTickCandidates[i];
            if (item == null)
            {
                fluidTickCandidates.RemoveAt(i);
                continue;
            }

            if (!itemStates.TryGetValue(item, out ItemState state) || !ShouldBeFluidTickCandidate(item, state))
            {
                RemoveFluidTickCandidateAt(i, item);
                continue;
            }

            item.TickFluidPhysics(fixedDeltaTime, currentTime);
        }
    }

    private bool ShouldBeFluidTickCandidate(DroppedItem item, ItemState state)
    {
        if (item == null || state == null || !item.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (state.state != DropState.Dynamic && state.state != DropState.Settling)
        {
            return false;
        }

        return item.ShouldTickFluidPhysics();
    }

    private void RefreshFluidTickCandidate(DroppedItem item, ItemState state)
    {
        if (ShouldBeFluidTickCandidate(item, state))
        {
            AddFluidTickCandidate(item);
        }
        else
        {
            RemoveFluidTickCandidate(item);
        }
    }

    private void AddFluidTickCandidate(DroppedItem item)
    {
        if (item == null || !fluidTickCandidateSet.Add(item))
        {
            return;
        }

        fluidTickCandidates.Add(item);
    }

    private void RemoveFluidTickCandidate(DroppedItem item)
    {
        if (item == null || !fluidTickCandidateSet.Remove(item))
        {
            return;
        }

        fluidTickCandidates.Remove(item);
    }

    private void RemoveFluidTickCandidateAt(int index, DroppedItem item)
    {
        if (item != null)
        {
            fluidTickCandidateSet.Remove(item);
        }

        if (index >= 0 && index < fluidTickCandidates.Count)
        {
            fluidTickCandidates.RemoveAt(index);
        }
    }

    private void UpdateItemMotionState(DroppedItem item, ItemState state)
    {
        if (state.state == DropState.Anchored || state.state == DropState.Invalidated || state.state == DropState.Solidified)
        {
            return;
        }

        state.velocityHistory.Enqueue(item.rb.linearVelocity.magnitude);
        if (state.velocityHistory.Count > VelocityHistorySize)
        {
            state.velocityHistory.Dequeue();
        }

        float averageVelocity = 0f;
        if (state.velocityHistory.Count > 0)
        {
            foreach (float v in state.velocityHistory) averageVelocity += v;
            averageVelocity /= state.velocityHistory.Count;
        }

        float angularSpeed = item.rb.angularVelocity.magnitude;
        bool cooldownReady = state.sleepCooldownTimer <= 0;
        bool linearAtRest = state.velocityHistory.Count == VelocityHistorySize && averageVelocity < SleepVelocityThreshold;
        bool angularAtRest = angularSpeed < angularSleepThreshold;
        bool atRest = linearAtRest && angularAtRest && cooldownReady;
        bool slowEnoughToAnchor = state.velocityHistory.Count == VelocityHistorySize &&
            averageVelocity < Mathf.Max(SleepVelocityThreshold, anchoredCandidateLinearSpeed) &&
            angularSpeed < Mathf.Max(angularSleepThreshold, anchoredCandidateAngularSpeed) &&
            cooldownReady;

        if (state.state == DropState.Dynamic)
        {
            if (atRest || (slowEnoughToAnchor && TryFindSupport(item, reusableSupportCells)))
            {
                SetSettling(item, state);
            }
            return;
        }

        bool movedTooMuchForSettling =
            averageVelocity > Mathf.Max(anchoredCandidateLinearSpeed, settlingAbortLinearSpeed) ||
            angularSpeed > Mathf.Max(anchoredCandidateAngularSpeed, settlingAbortAngularSpeed);
        if (movedTooMuchForSettling)
        {
            SetDynamic(item, state, WakeReason.Manual);
            return;
        }

        if (state.state == DropState.Settling &&
            state.settlingSince >= 0f &&
            Time.time - state.settlingSince >= minSettlingSeconds &&
            (atRest || slowEnoughToAnchor) &&
            TryFindSupport(item, reusableSupportCells))
        {
            SetAnchored(item, state, reusableSupportCells);
        }
    }

    private void SetSettling(DroppedItem item, ItemState state)
    {
        state.state = DropState.Settling;
        state.settlingSince = Time.time;
        state.solidificationStartedAt = -1f;
        item.SetAnchoredPhysicsMode(false);
        RefreshFluidTickCandidate(item, state);
    }

    private void SetAnchored(DroppedItem item, ItemState state, List<VoxelCellKey> supportCells)
    {
        RemoveFromAnchoredIndexes(item, state);
        ReleaseSolidificationReservation(item);

        state.state = DropState.Anchored;
        RemoveFluidTickCandidate(item);
        state.supportCells.Clear();
        for (int i = 0; i < supportCells.Count && i < maxSupportCells; i++)
        {
            state.supportCells.Add(supportCells[i]);
        }

        if (item.rb != null)
        {
            item.rb.linearVelocity = Vector3.zero;
            item.rb.angularVelocity = Vector3.zero;
            item.rb.isKinematic = true;
        }

        item.SetAnchoredPhysicsMode(true);
        SetAnchoredDebugTint(item, true);
        SetItemLayer(item.gameObject, anchoredDropLayer);
        AddToAnchoredIndexes(item, state);
    }

    private void SetDynamic(DroppedItem item, ItemState state, WakeReason reason)
    {
        RemoveFromAnchoredIndexes(item, state);
        ReleaseSolidificationReservation(item);

        state.state = DropState.Dynamic;
        state.settlingSince = -1f;
        state.solidificationStartedAt = -1f;
        state.lastInvalidationVersion = -1;
        state.velocityHistory.Clear();

        if (item.rb != null)
        {
            item.rb.isKinematic = false;
            item.rb.WakeUp();
        }

        item.SetAnchoredPhysicsMode(false);
        SetAnchoredDebugTint(item, false);
        SetItemLayer(item.gameObject, dynamicDropLayer);
        RefreshFluidTickCandidate(item, state);
    }

    private void SetInvalidated(DroppedItem item, ItemState state, int terrainVersion)
    {
        if (state.state != DropState.Anchored || state.lastInvalidationVersion == terrainVersion)
        {
            return;
        }

        state.state = DropState.Invalidated;
        RemoveFluidTickCandidate(item);
        state.lastInvalidationVersion = terrainVersion;
        if (invalidatedItems.Add(item))
        {
            invalidatedQueue.Enqueue(item);
        }
    }

    private void SetSolidified(DroppedItem item, ItemState state)
    {
        state.state = DropState.Solidified;
        state.settlingSince = -1f;
        RemoveFluidTickCandidate(item);
        RemoveFromAnchoredIndexes(item, state);
        ReleaseSolidificationReservation(item);
        item.SetAnchoredPhysicsMode(false);
        SetAnchoredDebugTint(item, false);
        SetItemLayer(item.gameObject, dynamicDropLayer);
    }

    private void WakeItem(DroppedItem item, WakeReason reason)
    {
        if (item == null || !itemStates.TryGetValue(item, out ItemState state) || state.state == DropState.Solidified)
        {
            return;
        }

        bool shouldWakeAbove = state.state == DropState.Anchored || state.state == DropState.Invalidated;
        SetDynamic(item, state, reason);
        state.sleepCooldownTimer = Mathf.Max(state.sleepCooldownTimer, GetWakeCooldownSeconds(reason));
        if (shouldWakeAbove)
        {
            WakeAnchoredItemsAbove(item, WakeReason.SupportLost);
        }
    }

    private float GetWakeCooldownSeconds(WakeReason reason)
    {
        switch (reason)
        {
            case WakeReason.MiningPreWake:
                return Mathf.Max(0f, miningPreWakeCooldownSeconds);
            case WakeReason.Force:
                return Mathf.Max(0f, forceWakeCooldownSeconds);
            case WakeReason.SupportLost:
                return Mathf.Max(0f, supportLostWakeCooldownSeconds);
            case WakeReason.Pickup:
                return Mathf.Max(0f, pickupWakeCooldownSeconds);
            case WakeReason.Pool:
            case WakeReason.Load:
                return 0f;
            default:
                return Mathf.Max(0f, miningPreWakeCooldownSeconds);
        }
    }

    private void LogDropStateCountsIfNeeded()
    {
        if (!showAnchoredDebugInfo || Time.time < nextAnchoredDebugLogTime)
        {
            return;
        }

        nextAnchoredDebugLogTime = Time.time + Mathf.Max(0.25f, debugStateLogInterval);
        int dynamicCount = 0;
        int settlingCount = 0;
        int anchoredCount = 0;
        int invalidatedCount = 0;
        int solidifiedCount = 0;

        foreach (DroppedItem item in activeItems)
        {
            if (item == null || !itemStates.TryGetValue(item, out ItemState state))
            {
                continue;
            }

            switch (state.state)
            {
                case DropState.Dynamic:
                    dynamicCount++;
                    break;
                case DropState.Settling:
                    settlingCount++;
                    break;
                case DropState.Anchored:
                    anchoredCount++;
                    break;
                case DropState.Invalidated:
                    invalidatedCount++;
                    break;
                case DropState.Solidified:
                    solidifiedCount++;
                    break;
            }
        }

        Debug.Log(
            $"DroppedItemManager states: Dynamic={dynamicCount}, Settling={settlingCount}, Anchored={anchoredCount}, Invalidated={invalidatedCount}, Solidified={solidifiedCount}, SupportKeys={anchoredBySupportCell.Count}, InvalidatedQueue={invalidatedQueue.Count}");
    }

    private void AddToAnchoredIndexes(DroppedItem item, ItemState state)
    {
        foreach (VoxelCellKey key in state.supportCells)
        {
            if (!anchoredBySupportCell.TryGetValue(key, out HashSet<DroppedItem> items))
            {
                items = new HashSet<DroppedItem>();
                anchoredBySupportCell[key] = items;
            }
            items.Add(item);
        }
    }

    private void RemoveFromAnchoredIndexes(DroppedItem item, ItemState state)
    {
        if (state.supportCells.Count == 0)
        {
            return;
        }

        foreach (VoxelCellKey key in state.supportCells)
        {
            if (anchoredBySupportCell.TryGetValue(key, out HashSet<DroppedItem> items))
            {
                items.Remove(item);
                if (items.Count == 0)
                {
                    anchoredBySupportCell.Remove(key);
                }
            }
        }

        state.supportCells.Clear();
    }

    private void SetItemLayer(GameObject itemObject, int layer)
    {
        if (itemObject != null && layer >= 0)
        {
            itemObject.layer = layer;
        }
    }

    private void SetAnchoredDebugTint(DroppedItem item, bool anchored)
    {
        if (item == null)
        {
            return;
        }

        item.SetTemporaryAnchoredDebugTint(debugTintAnchoredItems && anchored);
    }

    private void WarnIfOverlapBufferFull(int hitCount, string context)
    {
        if (showAnchoredDebugInfo && hitCount >= overlapBuffer.Length)
        {
            Debug.LogWarning($"DroppedItemManager: overlap buffer filled in {context}; some drops may be skipped this frame.");
        }
    }

    private bool TryFindSupport(DroppedItem item, List<VoxelCellKey> terrainSupportCells)
    {
        if (TryFindTerrainSupport(item, terrainSupportCells))
        {
            return true;
        }

        terrainSupportCells.Clear();
        return TryFindAnchoredItemSupport(item);
    }

    private bool TryFindTerrainSupport(DroppedItem item, List<VoxelCellKey> supportCells)
    {
        supportCells.Clear();
        TerrainManager terrainManager = ResolveTerrainManager();
        if (item == null || terrainManager == null || terrainManager.VoxelManager == null)
        {
            return false;
        }

        Bounds bounds = item.ItemBounds;
        float voxelWorldSize = terrainManager.Settings.blockSize / Mathf.Max(1, terrainManager.Settings.voxelsPerBlock);
        float maxGap = voxelWorldSize * Mathf.Max(0f, supportMaxGapRatio);
        float maxPenetration = voxelWorldSize * Mathf.Max(0f, supportMaxPenetrationRatio);
        float probeDistance = voxelWorldSize * Mathf.Max(0.01f, supportMaxGapRatio + supportProbeRatio);
        float inset = voxelWorldSize * 0.05f;

        float minX = Mathf.Min(bounds.min.x + inset, bounds.center.x);
        float maxX = Mathf.Max(bounds.max.x - inset, bounds.center.x);
        float minZ = Mathf.Min(bounds.min.z + inset, bounds.center.z);
        float maxZ = Mathf.Max(bounds.max.z - inset, bounds.center.z);
        float bottomY = bounds.min.y;
        float sampleY = bottomY - probeDistance;

        supportSampleBuffer[0] = new Vector3(bounds.center.x, sampleY, bounds.center.z);
        supportSampleBuffer[1] = new Vector3(minX, sampleY, minZ);
        supportSampleBuffer[2] = new Vector3(minX, sampleY, maxZ);
        supportSampleBuffer[3] = new Vector3(maxX, sampleY, minZ);
        supportSampleBuffer[4] = new Vector3(maxX, sampleY, maxZ);

        for (int i = 0; i < supportSampleBuffer.Length && supportCells.Count < maxSupportCells; i++)
        {
            if (!terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(supportSampleBuffer[i], out VoxelCellKey key) ||
                !terrainManager.VoxelManager.IsVoxelCellSolid(key) ||
                supportCells.Contains(key))
            {
                continue;
            }

            Bounds cellBounds = terrainManager.VoxelManager.GetVoxelCellWorldBounds(key);
            float gap = bottomY - cellBounds.max.y;
            if (gap >= -maxPenetration && gap <= maxGap)
            {
                supportCells.Add(key);
            }
        }

        return supportCells.Count > 0;
    }

    private bool TryFindAnchoredItemSupport(DroppedItem item)
    {
        if (item == null)
        {
            return false;
        }

        Bounds bounds = item.ItemBounds;
        float voxelWorldSize = GetVoxelWorldSize();
        float maxGap = voxelWorldSize * Mathf.Max(supportMaxGapRatio, itemSupportProbeDepthRatio);
        float maxPenetration = voxelWorldSize * Mathf.Max(supportMaxPenetrationRatio, itemSupportMaxPenetrationRatio);
        float horizontalPadding = voxelWorldSize * Mathf.Max(0f, itemSupportHorizontalPaddingRatio);
        float probeMinY = bounds.min.y - maxGap;
        float probeMaxY = bounds.min.y + maxPenetration;
        float probeHeight = Mathf.Max(0.01f, probeMaxY - probeMinY);
        Vector3 probeCenter = new Vector3(bounds.center.x, (probeMinY + probeMaxY) * 0.5f, bounds.center.z);
        Vector3 probeHalfExtents = new Vector3(
            Mathf.Max(0.01f, bounds.extents.x + horizontalPadding),
            probeHeight * 0.5f,
            Mathf.Max(0.01f, bounds.extents.z + horizontalPadding));

        int hitCount = Physics.OverlapBoxNonAlloc(probeCenter, probeHalfExtents, overlapBuffer, Quaternion.identity);
        WarnIfOverlapBufferFull(hitCount, nameof(TryFindAnchoredItemSupport));

        float requiredOverlapArea = Mathf.Max(0.0001f, bounds.size.x * bounds.size.z) * Mathf.Clamp01(itemSupportMinOverlapRatio);
        float accumulatedOverlapArea = 0f;
        for (int i = 0; i < hitCount; i++)
        {
            DroppedItem supportItem = overlapBuffer[i].GetComponent<DroppedItem>();
            if (supportItem == null || supportItem == item ||
                !itemStates.TryGetValue(supportItem, out ItemState supportState) ||
                supportState.state != DropState.Anchored)
            {
                continue;
            }

            Bounds supportBounds = supportItem.ItemBounds;
            if (supportBounds.center.y > bounds.center.y)
            {
                continue;
            }

            float gap = bounds.min.y - supportBounds.max.y;
            if (gap < -maxPenetration || gap > maxGap)
            {
                continue;
            }

            Bounds paddedSupportBounds = supportBounds;
            paddedSupportBounds.Expand(new Vector3(horizontalPadding * 2f, 0f, horizontalPadding * 2f));
            accumulatedOverlapArea += GetHorizontalOverlapArea(bounds, paddedSupportBounds);
            if (accumulatedOverlapArea >= requiredOverlapArea)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasEnoughHorizontalOverlap(Bounds itemBounds, Bounds supportBounds, float minOverlapRatio)
    {
        float itemArea = Mathf.Max(0.0001f, itemBounds.size.x * itemBounds.size.z);
        return GetHorizontalOverlapArea(itemBounds, supportBounds) >= itemArea * Mathf.Clamp01(minOverlapRatio);
    }

    private float GetHorizontalOverlapArea(Bounds itemBounds, Bounds supportBounds)
    {
        float overlapX = Mathf.Min(itemBounds.max.x, supportBounds.max.x) - Mathf.Max(itemBounds.min.x, supportBounds.min.x);
        float overlapZ = Mathf.Min(itemBounds.max.z, supportBounds.max.z) - Mathf.Max(itemBounds.min.z, supportBounds.min.z);
        if (overlapX <= 0f || overlapZ <= 0f)
        {
            return 0f;
        }

        return overlapX * overlapZ;
    }

    private float GetVoxelWorldSize()
    {
        TerrainManager terrainManager = ResolveTerrainManager();
        if (terrainManager == null)
        {
            return 0.25f;
        }

        return terrainManager.Settings.blockSize / Mathf.Max(1, terrainManager.Settings.voxelsPerBlock);
    }

    private void OnTerrainCellsChanged(TerrainChangeBatch change)
    {
        if (change == null || change.removedSolidCells.Count == 0)
        {
            return;
        }

        for (int i = 0; i < change.removedSolidCells.Count; i++)
        {
            VoxelCellKey removedCell = change.removedSolidCells[i];
            if (!anchoredBySupportCell.TryGetValue(removedCell, out HashSet<DroppedItem> anchoredItems))
            {
                continue;
            }

            foreach (DroppedItem item in anchoredItems)
            {
                if (item != null && itemStates.TryGetValue(item, out ItemState state))
                {
                    SetInvalidated(item, state, change.version);
                }
            }
        }

        ProcessInvalidatedQueue(int.MaxValue);
    }

    private void ProcessInvalidatedQueue(int maxCount)
    {
        int processed = 0;
        while (invalidatedQueue.Count > 0 && processed < maxCount)
        {
            DroppedItem item = invalidatedQueue.Dequeue();
            invalidatedItems.Remove(item);
            processed++;

            if (item == null || !item.gameObject.activeInHierarchy || !itemStates.TryGetValue(item, out ItemState state))
            {
                continue;
            }

            if (state.state != DropState.Invalidated)
            {
                continue;
            }

            if (TryFindSupport(item, reusableSupportCells))
            {
                SetAnchored(item, state, reusableSupportCells);
            }
            else
            {
                WakeItem(item, WakeReason.SupportLost);
            }
        }
    }

    public void WakeUpItemsInRadius(Vector3 center, Vector3 size, Quaternion rotation)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(center, size / 2, overlapBuffer, rotation);
        WarnIfOverlapBufferFull(hitCount, nameof(WakeUpItemsInRadius));
        reusableWakeSet.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapBuffer[i];
            DroppedItem item = hitCollider.GetComponent<DroppedItem>();
            if (item != null && itemStates.TryGetValue(item, out ItemState state) && state.state != DropState.Dynamic)
            {
                reusableWakeSet.Add(item);
            }
        }
        WakeItemsNow(reusableWakeSet, WakeReason.MiningPreWake);
        reusableWakeSet.Clear();
    }

    public void ApplyForceToItemsInRadius(Vector3 center, Vector3 size, Quaternion rotation, MiningInfo info)
    {
        if (info.Force <= 0) return;

        int hitCount = Physics.OverlapBoxNonAlloc(center, size / 2, overlapBuffer, rotation);
        WarnIfOverlapBufferFull(hitCount, nameof(ApplyForceToItemsInRadius));
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapBuffer[i];
            DroppedItem item = hitCollider.GetComponent<DroppedItem>();
            if (item != null && item.rb != null)
            {
                if (queuedInitialForceSkipUntil.TryGetValue(item, out float skipUntil))
                {
                    if (Time.time <= skipUntil)
                    {
                        continue;
                    }

                    queuedInitialForceSkipUntil.Remove(item);
                }

                // アイテムがスリープ状態なら起床させる
                if (itemStates.TryGetValue(item, out ItemState state) && state.state != DropState.Dynamic)
                {
                    WakeItem(item, WakeReason.Force);
                }

                item.rb.AddForce(CalculateMiningImpulse(item.transform.position, info), ForceMode.Impulse);
            }
        }
    }

    public void WakeUpItemsNearPosition(Vector3 position, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(position, radius, overlapBuffer);
        WarnIfOverlapBufferFull(hitCount, nameof(WakeUpItemsNearPosition));
        reusableWakeSet.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapBuffer[i];
            DroppedItem item = hitCollider.GetComponent<DroppedItem>();
            if (item != null && itemStates.TryGetValue(item, out ItemState state) && state.state != DropState.Dynamic)
            {
                reusableWakeSet.Add(item);
            }
        }
        WakeItemsNow(reusableWakeSet, WakeReason.Pickup);
        reusableWakeSet.Clear();
    }

    private void WakeItemsNow(HashSet<DroppedItem> items, WakeReason reason)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        foreach (DroppedItem item in items)
        {
            WakeItem(item, reason);
        }
    }

    private void WakeAnchoredItemsAbove(DroppedItem root, WakeReason reason)
    {
        if (root == null || maxUpwardWakePerEvent <= 0)
        {
            return;
        }

        upwardWakeList.Clear();
        upwardWakeVisited.Clear();
        upwardWakeVisited.Add(root);
        AddAnchoredItemsAbove(root, upwardWakeList, upwardWakeVisited);

        int processed = 0;
        for (int i = 0; i < upwardWakeList.Count && processed < maxUpwardWakePerEvent; i++)
        {
            DroppedItem item = upwardWakeList[i];
            if (item == null || !itemStates.TryGetValue(item, out ItemState state) ||
                (state.state != DropState.Anchored && state.state != DropState.Invalidated))
            {
                continue;
            }

            SetDynamic(item, state, reason);
            state.sleepCooldownTimer = Mathf.Max(state.sleepCooldownTimer, GetWakeCooldownSeconds(reason));
            processed++;
            AddAnchoredItemsAbove(item, upwardWakeList, upwardWakeVisited);
        }

        upwardWakeList.Clear();
        upwardWakeVisited.Clear();
    }

    private void AddAnchoredItemsAbove(DroppedItem source, List<DroppedItem> results, HashSet<DroppedItem> visited)
    {
        Bounds sourceBounds = source.ItemBounds;
        float voxelWorldSize = GetVoxelWorldSize();
        float verticalRange = Mathf.Max(sourceBounds.size.y, voxelWorldSize * Mathf.Max(1f, upwardWakeHeightRatio));
        float padding = voxelWorldSize * Mathf.Max(0f, upwardWakePaddingRatio);
        Vector3 center = new Vector3(
            sourceBounds.center.x,
            sourceBounds.max.y + verticalRange * 0.5f,
            sourceBounds.center.z);
        Vector3 halfExtents = new Vector3(
            sourceBounds.extents.x + padding,
            verticalRange * 0.5f,
            sourceBounds.extents.z + padding);

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, Quaternion.identity);
        WarnIfOverlapBufferFull(hitCount, nameof(AddAnchoredItemsAbove));

        for (int i = 0; i < hitCount && results.Count < maxUpwardWakePerEvent; i++)
        {
            DroppedItem candidate = overlapBuffer[i].GetComponent<DroppedItem>();
            if (candidate == null || visited.Contains(candidate) ||
                !itemStates.TryGetValue(candidate, out ItemState candidateState) ||
                candidateState.state != DropState.Anchored)
            {
                continue;
            }

            Bounds candidateBounds = candidate.ItemBounds;
            if (candidateBounds.center.y < sourceBounds.center.y ||
                !HasEnoughHorizontalOverlap(candidateBounds, sourceBounds, itemSupportMinOverlapRatio))
            {
                continue;
            }

            visited.Add(candidate);
            results.Add(candidate);
        }
    }

    public void EnqueueDropItem(
        Vector3 position,
        BlockData blockData,
        bool useTexture1,
        int voxelX,
        int voxelY,
        int voxelZ,
        int voxelsPerBlock,
        float voxelWorldSize,
        VoxelTextureExtractor textureExtractor,
        MiningInfo miningInfo,
        bool applyInitialForce)
    {
        if (blockData == null)
        {
            Debug.LogError("DroppedItemManager: BlockData is null. Cannot queue dropped item.", this);
            return;
        }

        if (blockData.droppedItemPrefab == null)
        {
            Debug.LogError($"DroppedItemManager: BlockData '{blockData.name}' has no droppedItemPrefab assigned.", blockData);
            return;
        }

        queuedDropSpawns.Enqueue(new DropSpawnRequest
        {
            position = position,
            blockData = blockData,
            useTexture1 = useTexture1,
            voxelX = voxelX,
            voxelY = voxelY,
            voxelZ = voxelZ,
            voxelsPerBlock = voxelsPerBlock,
            voxelWorldSize = voxelWorldSize,
            textureExtractor = textureExtractor,
            miningInfo = miningInfo,
            applyInitialForce = applyInitialForce
        });

        if (dropQueueWarningThreshold >= 0 &&
            queuedDropSpawns.Count > dropQueueWarningThreshold &&
            !dropQueueThresholdWarningIssued)
        {
            dropQueueThresholdWarningIssued = true;
            Debug.LogWarning(
                $"DroppedItemManager: queued drop spawn count exceeded threshold. Count={queuedDropSpawns.Count}, Threshold={dropQueueWarningThreshold}",
                this);
        }
    }

    private void ProcessQueuedDropSpawns()
    {
        if (queuedDropSpawns.Count == 0)
        {
            dropQueueThresholdWarningIssued = false;
            return;
        }

        if (!ValidateDropQueueSettings())
        {
            return;
        }

        using (ProcessQueuedDropSpawnsMarker.Auto())
        {
            int processedCount = 0;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            double budgetSeconds = maxQueuedDropSpawnMilliseconds / 1000.0;

            while (queuedDropSpawns.Count > 0 && processedCount < maxQueuedDropSpawnsPerFrame)
            {
                if (processedCount > 0 && Time.realtimeSinceStartupAsDouble - startedAt >= budgetSeconds)
                {
                    break;
                }

                DropSpawnRequest request = queuedDropSpawns.Dequeue();
                SpawnQueuedDrop(request);
                processedCount++;
            }

            if (queuedDropSpawns.Count <= dropQueueWarningThreshold)
            {
                dropQueueThresholdWarningIssued = false;
            }

            if (showDropQueueDebugInfo && processedCount > 0)
            {
                Debug.Log(
                    $"DroppedItemManager drop queue: processed={processedCount}, remaining={queuedDropSpawns.Count}",
                    this);
            }
        }
    }

    private void CleanupExpiredQueuedInitialForceSkips()
    {
        if (queuedInitialForceSkipUntil.Count == 0)
        {
            return;
        }

        reusableQueuedForceSkipRemovals.Clear();
        float now = Time.time;
        foreach (KeyValuePair<DroppedItem, float> entry in queuedInitialForceSkipUntil)
        {
            if (entry.Key == null || entry.Value < now)
            {
                reusableQueuedForceSkipRemovals.Add(entry.Key);
            }
        }

        for (int i = 0; i < reusableQueuedForceSkipRemovals.Count; i++)
        {
            queuedInitialForceSkipUntil.Remove(reusableQueuedForceSkipRemovals[i]);
        }

        reusableQueuedForceSkipRemovals.Clear();
    }

    private bool ValidateDropQueueSettings()
    {
        if (maxQueuedDropSpawnsPerFrame <= 0 ||
            maxQueuedDropSpawnMilliseconds <= 0f ||
            dropQueueWarningThreshold < 0)
        {
            if (!dropQueueSettingsErrorLogged)
            {
                dropQueueSettingsErrorLogged = true;
                Debug.LogError(
                    $"DroppedItemManager: invalid drop queue settings. maxQueuedDropSpawnsPerFrame={maxQueuedDropSpawnsPerFrame}, maxQueuedDropSpawnMilliseconds={maxQueuedDropSpawnMilliseconds}, dropQueueWarningThreshold={dropQueueWarningThreshold}",
                    this);
            }

            return false;
        }

        dropQueueSettingsErrorLogged = false;
        return true;
    }

    private void SpawnQueuedDrop(DropSpawnRequest request)
    {
        using (SpawnQueuedDropMarker.Auto())
        {
            if (request.blockData == null)
            {
                Debug.LogError("DroppedItemManager: queued drop request has null BlockData.", this);
                return;
            }

            if (request.blockData.droppedItemPrefab == null)
            {
                Debug.LogError($"DroppedItemManager: queued BlockData '{request.blockData.name}' has no droppedItemPrefab assigned.", request.blockData);
                return;
            }

            GameObject item = GetItem(request.blockData.droppedItemPrefab);
            if (item == null)
            {
                Debug.LogError($"DroppedItemManager: failed to get dropped item instance for '{request.blockData.name}'.", request.blockData);
                return;
            }

            item.transform.position = request.position;
            item.transform.rotation = Quaternion.identity;

            BlockItemDropper.SetupDroppedItem(
                item,
                request.blockData,
                request.useTexture1,
                request.voxelX,
                request.voxelY,
                request.voxelZ,
                request.voxelsPerBlock,
                request.voxelWorldSize,
                request.textureExtractor);

            DroppedItem droppedItem = item.GetComponent<DroppedItem>();
            if (request.applyInitialForce)
            {
                ApplyMiningForceToItem(droppedItem, request.miningInfo);
            }
        }
    }

    private void ApplyMiningForceToItem(DroppedItem item, MiningInfo info)
    {
        if (item == null || item.rb == null || info.Force <= 0f)
        {
            return;
        }

        Vector3 impulse = CalculateMiningImpulse(item.transform.position, info);
        item.rb.AddForce(impulse, ForceMode.Impulse);
        queuedInitialForceSkipUntil[item] = Time.time + QueuedInitialForceDuplicateSkipSeconds;
    }

    private static Vector3 CalculateMiningImpulse(Vector3 itemPosition, MiningInfo info)
    {
        switch (info.Type)
        {
            case MiningType.Directional:
                return info.Direction.normalized * info.Force;
            case MiningType.ArcSwing:
            {
                Vector3 itemDirection = itemPosition - info.SourcePoint;
                itemDirection.z = 0;
                Vector3 tangentDirection = info.IsFacingRight
                    ? new Vector3(itemDirection.y, -itemDirection.x, 0)
                    : new Vector3(-itemDirection.y, itemDirection.x, 0);
                return (tangentDirection.normalized + Vector3.up * 0.3f).normalized * info.Force;
            }
            case MiningType.Explosive:
            {
                Vector3 directionFromExplosion = (itemPosition - info.SourcePoint).normalized;
                directionFromExplosion = (directionFromExplosion + Vector3.up * 0.5f).normalized;
                return directionFromExplosion * info.Force;
            }
            default:
                Debug.LogError($"DroppedItemManager: Unsupported MiningType '{info.Type}' for queued drop force.");
                return Vector3.zero;
        }
    }

    public GameObject GetItem(GameObject prefab)
    {
        if (prefab == null) return null;

        if (!itemPools.ContainsKey(prefab))
        {
            itemPools[prefab] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = itemPools[prefab];
        GameObject itemInstance;

        if (pool.Count > 0)
        {
            itemInstance = pool.Dequeue();
            itemInstance.SetActive(true);
        }
        else
        {
            itemInstance = Instantiate(prefab, transform);
            itemInstance.layer = LayerMask.NameToLayer("DroppedItem");
            instancePrefabMap[itemInstance] = prefab;
        }

        var droppedItemComponent = itemInstance.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            InjectFluidManager(droppedItemComponent);
            SetItemLayer(itemInstance, dynamicDropLayer);
            droppedItemComponent.SetAnchoredPhysicsMode(false);
            SetAnchoredDebugTint(droppedItemComponent, false);
            if (droppedItemComponent.rb != null)
            {
                droppedItemComponent.rb.isKinematic = false;
            }

            // 管理リストに追加
            if (!activeItems.Contains(droppedItemComponent))
            {
                activeItems.Add(droppedItemComponent);
            }
            // 状態を初期化して追加
            if (!itemStates.ContainsKey(droppedItemComponent))
            {
                itemStates.Add(droppedItemComponent, new ItemState());
            }
            ItemState state = itemStates[droppedItemComponent];
            state.state = DropState.Dynamic;
            state.sleepCooldownTimer = Mathf.Max(0f, spawnSleepCooldownSeconds);
            state.settlingSince = -1f;
            state.solidificationStartedAt = -1f;
            state.hasSolidificationReservation = false;
            state.supportCells.Clear();
            state.velocityHistory.Clear();
            RefreshFluidTickCandidate(droppedItemComponent, state);
        }

        return itemInstance;
    }

    public List<DroppedItem> GetActiveItems()
    {
        return new List<DroppedItem>(activeItems);
    }

    public void CollectActiveItemsNear(
        Vector3 origin,
        float radius,
        List<DroppedItem> results,
        int maxCount,
        ISet<DroppedItem> excludedItems = null)
    {
        if (results == null)
        {
            Debug.LogError("DroppedItemManager: results list is null.", this);
            return;
        }

        results.Clear();
        reusableCandidateDistances.Clear();

        if (maxCount <= 0)
        {
            Debug.LogError($"DroppedItemManager: maxCount must be greater than 0. maxCount={maxCount}", this);
            return;
        }

        bool useRadius = radius > 0f;
        float radiusSqr = radius * radius;
        for (int i = 0; i < activeItems.Count; i++)
        {
            DroppedItem item = activeItems[i];
            if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (excludedItems != null && excludedItems.Contains(item))
            {
                continue;
            }

            float distanceSqr = (item.transform.position - origin).sqrMagnitude;
            if (useRadius && distanceSqr > radiusSqr)
            {
                continue;
            }

            InsertNearestCandidate(item, distanceSqr, results, maxCount);
        }

        reusableCandidateDistances.Clear();
    }

    public bool CollectActiveItemsNearIncremental(
        Vector3 origin,
        float radius,
        List<DroppedItem> results,
        List<float> candidateDistances,
        int maxCount,
        ISet<DroppedItem> excludedItems,
        ref int nextIndex,
        int maxItemsToScan)
    {
        if (results == null)
        {
            Debug.LogError("DroppedItemManager: results list is null.", this);
            return true;
        }

        if (candidateDistances == null)
        {
            Debug.LogError("DroppedItemManager: candidateDistances list is null.", this);
            return true;
        }

        if (maxCount <= 0)
        {
            Debug.LogError($"DroppedItemManager: maxCount must be greater than 0. maxCount={maxCount}", this);
            return true;
        }

        if (maxItemsToScan <= 0)
        {
            Debug.LogError($"DroppedItemManager: maxItemsToScan must be greater than 0. maxItemsToScan={maxItemsToScan}", this);
            return true;
        }

        if (candidateDistances.Count != results.Count)
        {
            Debug.LogError(
                $"DroppedItemManager: candidateDistances count must match results count. Distances={candidateDistances.Count}, Results={results.Count}",
                this);
            return true;
        }

        if (nextIndex < 0)
        {
            Debug.LogError($"DroppedItemManager: nextIndex must not be negative. nextIndex={nextIndex}", this);
            return true;
        }

        bool useRadius = radius > 0f;
        float radiusSqr = radius * radius;
        int scanned = 0;
        while (nextIndex < activeItems.Count && scanned < maxItemsToScan)
        {
            DroppedItem item = activeItems[nextIndex];
            nextIndex++;
            scanned++;

            if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (excludedItems != null && excludedItems.Contains(item))
            {
                continue;
            }

            float distanceSqr = (item.transform.position - origin).sqrMagnitude;
            if (useRadius && distanceSqr > radiusSqr)
            {
                continue;
            }

            InsertNearestCandidate(item, distanceSqr, results, candidateDistances, maxCount);
        }

        return nextIndex >= activeItems.Count;
    }

    private void InsertNearestCandidate(DroppedItem item, float distanceSqr, List<DroppedItem> results, int maxCount)
    {
        InsertNearestCandidate(item, distanceSqr, results, reusableCandidateDistances, maxCount);
    }

    private void InsertNearestCandidate(
        DroppedItem item,
        float distanceSqr,
        List<DroppedItem> results,
        List<float> candidateDistances,
        int maxCount)
    {
        if (results.Count >= maxCount && distanceSqr >= candidateDistances[candidateDistances.Count - 1])
        {
            return;
        }

        int insertIndex = 0;
        while (insertIndex < candidateDistances.Count && candidateDistances[insertIndex] <= distanceSqr)
        {
            insertIndex++;
        }

        results.Insert(insertIndex, item);
        candidateDistances.Insert(insertIndex, distanceSqr);

        if (results.Count > maxCount)
        {
            int removeIndex = results.Count - 1;
            results.RemoveAt(removeIndex);
            candidateDistances.RemoveAt(removeIndex);
        }
    }

    public bool ContainsActiveItem(DroppedItem item)
    {
        return item != null && activeItems.Contains(item);
    }

    public void ReturnItem(GameObject itemInstance)
    {
        if (itemInstance == null) return;

        var droppedItemComponent = itemInstance.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            queuedInitialForceSkipUntil.Remove(droppedItemComponent);
            // 状態とリストから削除
            if (itemStates.TryGetValue(droppedItemComponent, out ItemState state))
            {
                if (state.state == DropState.Anchored || state.state == DropState.Invalidated)
                {
                    WakeAnchoredItemsAbove(droppedItemComponent, WakeReason.SupportLost);
                }

                SetDynamic(droppedItemComponent, state, WakeReason.Pool);
            }
            RemoveFluidTickCandidate(droppedItemComponent);
            itemStates.Remove(droppedItemComponent);
            activeItems.Remove(droppedItemComponent);
        }

        if (instancePrefabMap.TryGetValue(itemInstance, out GameObject prefab))
        {
            if (itemPools.ContainsKey(prefab))
            {
                itemInstance.SetActive(false);
                itemPools[prefab].Enqueue(itemInstance);
            }
            else
            {
                Destroy(itemInstance);
            }
        }
        else
        {
            Destroy(itemInstance);
        }
    }

    private void ProcessSolidificationCandidates()
    {
        if (!enableSolidification || activeItems.Count == 0)
        {
            return;
        }

        TerrainManager terrainManager = ResolveTerrainManager();
        if (terrainManager == null || terrainManager.VoxelManager == null ||
            terrainManager.BlockManager == null || terrainManager.TerrainDataManager == null)
        {
            return;
        }

        solidifyAfterSleepingSeconds = Mathf.Max(0f, solidifyAfterSleepingSeconds);
        maxSolidificationsPerCheck = Mathf.Max(1, maxSolidificationsPerCheck);
        candidateLookupCount = Mathf.Max(1, candidateLookupCount);
        candidateMaxBlockRadius = Mathf.Max(1, candidateMaxBlockRadius);

        int attempts = 0;
        for (int i = activeItems.Count - 1; i >= 0 && attempts < maxSolidificationsPerCheck; i--)
        {
            DroppedItem item = activeItems[i];
            if (item == null || !itemStates.TryGetValue(item, out ItemState state))
            {
                continue;
            }

            if (!IsStableForSolidification(state))
            {
                continue;
            }

            if (Time.time - state.settlingSince < solidifyAfterSleepingSeconds)
            {
                continue;
            }

            attempts++;
            TrySolidifyItem(item, terrainManager, state);
        }
    }

    private TerrainManager ResolveTerrainManager()
    {
        if (cachedTerrainManager == null)
        {
            cachedTerrainManager = FindFirstObjectByType<TerrainManager>();
        }

        return cachedTerrainManager;
    }

    private FluidManager ResolveFluidManager()
    {
        TerrainManager terrainManager = ResolveTerrainManager();
        return terrainManager != null ? terrainManager.FluidManager : null;
    }

    private void InjectFluidManager(DroppedItem item)
    {
        if (item == null)
        {
            return;
        }

        FluidManager fluidManager = ResolveFluidManager();
        if (fluidManager == null)
        {
            if (!missingFluidManagerLogged)
            {
                missingFluidManagerLogged = true;
                Debug.LogError("DroppedItemManager: FluidManager is not configured. Dropped item fluid physics will be skipped.", this);
            }

            return;
        }

        missingFluidManagerLogged = false;
        item.SetFluidManager(fluidManager);
    }

    private bool IsStableForSolidification(ItemState state)
    {
        return state != null &&
               state.settlingSince >= 0f &&
               (state.state == DropState.Settling || state.state == DropState.Anchored);
    }

    private bool TrySolidifyItem(DroppedItem item, TerrainManager terrainManager, ItemState state)
    {
        if (item == null || !item.canSolidify || string.IsNullOrEmpty(item.blockDataName))
        {
            return false;
        }

        BlockData blockData = terrainManager.TerrainDataManager.GetBlockDataByName(item.blockDataName);
        if (blockData == null)
        {
            return false;
        }

        if (state.solidificationStartedAt < 0f)
        {
            state.solidificationStartedAt = Time.time;
        }

        SolidificationCandidate target;
        if (!TryFindSolidificationTarget(item, terrainManager, state, out target))
        {
            ReleaseSolidificationReservation(item);
            return false;
        }

        if (terrainManager.BlockManager.GetBlockAt(target.key.blockPosition) == null)
        {
            Transform chunkParent = terrainManager.ChunkManager != null
                ? terrainManager.ChunkManager.GetOrCreateChunkTransform(target.key.blockPosition)
                : null;
            if (chunkParent == null ||
                terrainManager.BlockManager.EnsureBlockExists(target.key.blockPosition, chunkParent) == null)
            {
                ReleaseSolidificationReservation(item);
                return false;
            }
        }

        if (!terrainManager.VoxelManager.SetVoxelCell(target.key.blockPosition, target.key.localVoxelPosition, blockData, true, useTexture1: item.useTexture1))
        {
            ReleaseSolidificationReservation(item);
            return false;
        }

        if (!terrainManager.BlockManager.ActivateAndRefreshBlock(target.key.blockPosition))
        {
            ReleaseSolidificationReservation(item);
            return false;
        }

        terrainManager.FluidManager?.MarkDirtyAroundWorldPosition(target.worldPosition, 2);

        item.transform.position = target.worldPosition;
        item.hasSolidificationTarget = true;
        item.solidifiedBlockPosition = target.key.blockPosition;
        item.solidifiedLocalVoxelPosition = target.key.localVoxelPosition;

        GameDataPersistenceManager.Instance.solidifiedVoxelHistory.Add(new SolidifiedVoxelRecord
        {
            blockPosition = target.key.blockPosition,
            localVoxelPosition = target.key.localVoxelPosition,
            blockDataName = blockData.name,
            worldPosition = target.worldPosition,
            solidifiedTime = Time.time
        });

        SetSolidified(item, state);
        ReturnItem(item.gameObject);
        return true;
    }

    private bool TryFindSolidificationTarget(DroppedItem item, TerrainManager terrainManager, ItemState state, out SolidificationCandidate target)
    {
        target = new SolidificationCandidate();

        var index = terrainManager.VoxelManager.CandidateIndex;
        if (index == null)
        {
            return false;
        }

        var hits = index.FindNearestCandidates(item.transform.position, candidateLookupCount, candidateMaxBlockRadius);
        if (hits.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];

            // 確認: 候補が今も有効か（インデックスのラグへの保険）
            if (!terrainManager.VoxelManager.IsVoxelCellEmpty(hit.blockPosition, hit.localPosition))
            {
                continue;
            }

            var key = new VoxelCellKey(hit.blockPosition, hit.localPosition);
            if (TryReserveSolidificationCell(item, state, key))
            {
                target = new SolidificationCandidate
                {
                    key = key,
                    worldPosition = hit.worldPosition,
                    distanceSqr = hit.distanceSqr
                };
                return true;
            }
        }

        return false;
    }

    private bool TryReserveSolidificationCell(DroppedItem item, ItemState state, VoxelCellKey key)
    {
        if (state.hasSolidificationReservation && state.reservedCell.Equals(key))
        {
            return true;
        }

        if (state.solidificationStartedAt < 0f)
        {
            state.solidificationStartedAt = Time.time;
        }

        if (solidificationReservations.TryGetValue(key, out SolidificationReservation existing))
        {
            if (!IsReservationActive(existing))
            {
                solidificationReservations.Remove(key);
            }
            else if (existing.item == item)
            {
                state.hasSolidificationReservation = true;
                state.reservedCell = key;
                return true;
            }
            else
            {
                float startDelta = existing.startedAt - state.solidificationStartedAt;
                if (startDelta < -0.001f)
                {
                    return false;
                }

                if (startDelta > 0.001f || UnityEngine.Random.value < 0.5f)
                {
                    ClearReservationOnState(existing.item, key);
                    solidificationReservations.Remove(key);
                }
                else
                {
                    return false;
                }
            }
        }

        if (state.hasSolidificationReservation)
        {
            ReleaseSolidificationReservation(item);
        }

        solidificationReservations[key] = new SolidificationReservation
        {
            item = item,
            startedAt = state.solidificationStartedAt
        };
        state.hasSolidificationReservation = true;
        state.reservedCell = key;
        item.hasSolidificationTarget = true;
        item.solidifiedBlockPosition = key.blockPosition;
        item.solidifiedLocalVoxelPosition = key.localVoxelPosition;
        return true;
    }

    private bool IsReservationActive(SolidificationReservation reservation)
    {
        return reservation != null &&
               reservation.item != null &&
               reservation.item.gameObject.activeInHierarchy &&
               itemStates.ContainsKey(reservation.item);
    }

    private void ReleaseSolidificationReservation(DroppedItem item)
    {
        if (item == null || !itemStates.TryGetValue(item, out ItemState state))
        {
            return;
        }

        if (state.hasSolidificationReservation)
        {
            solidificationReservations.Remove(state.reservedCell);
            state.hasSolidificationReservation = false;
        }

        item.hasSolidificationTarget = false;
    }

    private void ClearReservationOnState(DroppedItem item, VoxelCellKey key)
    {
        if (item != null && itemStates.TryGetValue(item, out ItemState state) &&
            state.hasSolidificationReservation && state.reservedCell.Equals(key))
        {
            state.hasSolidificationReservation = false;
            item.hasSolidificationTarget = false;
        }
    }

    private void SaveItems()
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        persistenceManager.droppedItems.Clear();

        foreach (var item in activeItems)
        {
            if (item == null) continue;

            float solidificationElapsedSeconds = 0f;
            bool hasSolidificationTarget = item.hasSolidificationTarget;
            Vector3Int solidifiedBlockPosition = item.solidifiedBlockPosition;
            Vector3Int solidifiedLocalVoxelPosition = item.solidifiedLocalVoxelPosition;

            if (itemStates.TryGetValue(item, out ItemState state))
            {
                if (state.solidificationStartedAt >= 0f)
                {
                    solidificationElapsedSeconds = Mathf.Max(0f, Time.time - state.solidificationStartedAt);
                }

                if (state.hasSolidificationReservation)
                {
                    hasSolidificationTarget = true;
                    solidifiedBlockPosition = state.reservedCell.blockPosition;
                    solidifiedLocalVoxelPosition = state.reservedCell.localVoxelPosition;
                }
            }

            DroppedItemData data = new DroppedItemData
            {
                position = item.transform.position,
                rotation = item.transform.rotation,
                scale = item.transform.localScale,
                blockDataName = item.blockDataName,
                faceTextureData = item.faceTextureData != null ? (DroppedItemFaceTextureData[])item.faceTextureData.Clone() : null,
                uvBase = item.uvBase,
                uvSize = item.uvSize,
                useTexture1 = item.useTexture1,
                hasSolidificationData = true,
                canSolidify = item.canSolidify,
                hasSolidificationTarget = hasSolidificationTarget,
                solidifiedBlockPosition = solidifiedBlockPosition,
                solidifiedLocalVoxelPosition = solidifiedLocalVoxelPosition,
                solidificationElapsedSeconds = solidificationElapsedSeconds
            };
            persistenceManager.droppedItems.Add(data);
        }
    }

    private void PrepareItemLoading()
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager.droppedItems == null || persistenceManager.droppedItems.Count == 0) return;

        TerrainManager terrainManager = FindFirstObjectByType<TerrainManager>();
        if (terrainManager == null)
        {
            Debug.LogError("TerrainManager not found. Cannot prepare item loading.");
            return;
        }
        if (terrainManager.ChunkManager == null)
        {
            Debug.LogError("ChunkManager not found. Cannot prepare item loading.");
            return;
        }

        itemsByChunk.Clear();
        foreach (var itemData in persistenceManager.droppedItems)
        {
            Vector3Int chunkPos = terrainManager.ChunkManager.GetChunkPositionFromWorld(itemData.position);

            if (!itemsByChunk.ContainsKey(chunkPos))
            {
                itemsByChunk[chunkPos] = new List<DroppedItemData>();
            }
            itemsByChunk[chunkPos].Add(itemData);
        }
        
        persistenceManager.droppedItems.Clear();
    }

    public void LoadItemsInChunk(Vector3Int chunkPosition)
    {
        if (!itemsByChunk.TryGetValue(chunkPosition, out var itemsToLoad)) return;

        TerrainManager terrainManager = FindFirstObjectByType<TerrainManager>();
        if (terrainManager == null || terrainManager.TerrainDataManager == null)
        {
            Debug.LogError("TerrainManager or TerrainDataManager not found. Cannot load items.");
            return;
        }

        foreach (var data in itemsToLoad)
        {
            BlockData blockData = terrainManager.TerrainDataManager.GetBlockDataByName(data.blockDataName);
            if (blockData == null)
            {
                Debug.LogWarning($"Failed to load BlockData: {data.blockDataName}");
                continue;
            }

            GameObject item = GetItem(blockData.droppedItemPrefab);
            if (item == null) continue;

            item.transform.position = data.position;
            item.transform.rotation = data.rotation;
            item.transform.localScale = data.scale;
            SetItemLayer(item, dynamicDropLayer);

            Rigidbody itemRigidbody = item.GetComponent<Rigidbody>();
            if (itemRigidbody == null)
            {
                itemRigidbody = item.AddComponent<Rigidbody>();
            }

            if (terrainManager != null)
            {
                 float voxelWorldSize = terrainManager.Settings.blockSize / terrainManager.Settings.voxelsPerBlock;
                 float volume = Mathf.Pow(voxelWorldSize, 3);
                 itemRigidbody.mass = volume * blockData.density;
            }

            SetDroppedItemConstraints(itemRigidbody);

            if (!item.CompareTag("DroppedItem"))
            {
                item.tag = "DroppedItem";
            }

            DroppedItem droppedItem = item.GetComponent<DroppedItem>();
            Texture2D tex1 = (blockData.textures != null && blockData.textures.Count > 0) ? blockData.textures[0] : null;
            Texture2D tex2 = (blockData.textures != null && blockData.textures.Count > 1) ? blockData.textures[1] : null;

            if (droppedItem != null)
            {
                droppedItem.blockDataName = data.blockDataName;
                droppedItem.resourceType = blockData.resourceType;
                droppedItem.canSolidify = !data.hasSolidificationData || data.canSolidify;
                droppedItem.hasSolidificationTarget = data.hasSolidificationTarget;
                droppedItem.solidifiedBlockPosition = data.solidifiedBlockPosition;
                droppedItem.solidifiedLocalVoxelPosition = data.solidifiedLocalVoxelPosition;
                DroppedItemFaceTextureData[] savedFaceData = data.faceTextureData;
                if (savedFaceData == null || savedFaceData.Length != DroppedItem.FaceNormals.Length)
                {
                    savedFaceData = DroppedItem.CreateLegacyFaceTextureData(data.uvBase, data.uvSize, data.useTexture1);
                }

                droppedItem.ApplyFaceTextureData(savedFaceData, tex1, tex2);

                if (itemRigidbody != null)
                {
                    itemRigidbody.isKinematic = false;
                }
                if (itemStates.TryGetValue(droppedItem, out var state))
                {
                    state.state = DropState.Dynamic;
                    state.settlingSince = -1f;
                    state.solidificationStartedAt = data.solidificationElapsedSeconds > 0f
                        ? Time.time - data.solidificationElapsedSeconds
                        : -1f;
                    state.hasSolidificationReservation = false;
                    state.supportCells.Clear();
                }
            }

        }
        
        itemsByChunk.Remove(chunkPosition);
    }

    /// <summary>
    /// ドロップアイテムのRigidbodyに移動モードに応じた制約を設定
    /// </summary>
    private void SetDroppedItemConstraints(Rigidbody itemRigidbody)
    {
        if (itemRigidbody == null) return;

        itemRigidbody.constraints = RigidbodyConstraints.FreezePositionZ |
                                    RigidbodyConstraints.FreezeRotationX |
                                    RigidbodyConstraints.FreezeRotationY;
    }
}
