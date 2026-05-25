using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class MiningLightManager : MonoBehaviour
{
    private static readonly ProfilerMarker RestartPropagationMarker = new ProfilerMarker("MiningLightManager.RestartPropagation");
    private static readonly ProfilerMarker PropagationStepMarker = new ProfilerMarker("MiningLightManager.PropagationStep");
    private static readonly ProfilerMarker BurstLightUpdateMarker = new ProfilerMarker("MiningLightManager.BurstLightUpdate");
    private static readonly ProfilerMarker CollectTerrainAffectedSourcesMarker =
        new ProfilerMarker("MiningLightManager.CollectTerrainAffectedSources");
    private static readonly ProfilerMarker RestartTerrainAffectedSourcesMarker =
        new ProfilerMarker("MiningLightManager.RestartTerrainAffectedSources");
    private static readonly ProfilerMarker DrawGizmosMarker = new ProfilerMarker("MiningLightManager.DrawGizmos");

    private static readonly Vector3Int[] OrthogonalNeighborOffsets =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    private static readonly Vector3Int[] FaceAndEdgeNeighborOffsets = BuildFaceAndEdgeNeighborOffsets();
    private static readonly Vector3Int[] FullNeighborOffsets = BuildFullNeighborOffsets();

    private enum PropagationRunKind
    {
        FullSource,
        TerrainRepair
    }

    private static Vector3Int[] BuildFaceAndEdgeNeighborOffsets()
    {
        Vector3Int[] offsets = new Vector3Int[18];
        int index = 0;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                    {
                        continue;
                    }

                    if (Mathf.Abs(x) + Mathf.Abs(y) + Mathf.Abs(z) == 3)
                    {
                        continue;
                    }

                    offsets[index] = new Vector3Int(x, y, z);
                    index++;
                }
            }
        }

        return offsets;
    }

    private static Vector3Int[] BuildFullNeighborOffsets()
    {
        Vector3Int[] offsets = new Vector3Int[26];
        int index = 0;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                    {
                        continue;
                    }

                    offsets[index] = new Vector3Int(x, y, z);
                    index++;
                }
            }
        }

        return offsets;
    }

    [Header("Required References")]
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private List<MiningLightSource> initialLightSources = new List<MiningLightSource>();

    [Header("Light Propagation Budget")]
    [SerializeField, Min(1)] private int maxCalculatedCells = 4096;
    [SerializeField, Min(1)] private int maxPropagationCellsPerFrame = 256;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = false;
    [SerializeField, Min(1)] private int maxGizmoCells = 8192;
    [SerializeField, Range(0.05f, 1f)] private float gizmoCellScale = 0.75f;
    [SerializeField] private Color airCellGizmoColor = new Color(1f, 0.92f, 0.25f, 0.45f);
    [SerializeField] private Color solidCellGizmoColor = new Color(1f, 0.45f, 0.15f, 0.45f);

    private readonly Queue<VoxelCellKey> dirtyBrightnessCells = new Queue<VoxelCellKey>(512);
    private readonly HashSet<VoxelCellKey> queuedDirtyBrightnessCells = new HashSet<VoxelCellKey>();
    private readonly List<MiningLightSource> registeredLightSources = new List<MiningLightSource>(8);
    private readonly HashSet<MiningLightSource> registeredLightSourceSet = new HashSet<MiningLightSource>();
    private readonly List<TemporaryLightSource> temporaryLightSources = new List<TemporaryLightSource>(8);
    private readonly List<BurstLightSource> burstLightSources = new List<BurstLightSource>(8);
    private readonly List<LightSourceCell> currentLightSourceCells = new List<LightSourceCell>(8);
    private readonly Dictionary<object, VoxelCellKey> lastLightSourceCells =
        new Dictionary<object, VoxelCellKey>();
    private readonly Dictionary<object, MiningLightProfile> lastLightSourceProfiles =
        new Dictionary<object, MiningLightProfile>();
    private readonly Dictionary<object, LightRuntimeState> lightStates =
        new Dictionary<object, LightRuntimeState>();
    private readonly Dictionary<VoxelCellKey, float> composedBrightness =
        new Dictionary<VoxelCellKey, float>();
    private readonly List<VoxelCellKey> reusableCellBuffer = new List<VoxelCellKey>(256);
    private readonly List<object> reusableSourceBuffer = new List<object>(16);
    private readonly HashSet<VoxelCellKey> reusableAffectedTerrainCells = new HashSet<VoxelCellKey>();
    private readonly Dictionary<object, TerrainRepairRequest> pendingTerrainRepairRequests =
        new Dictionary<object, TerrainRepairRequest>();
    private readonly List<VoxelCellKey> reusableTraversalCells = new List<VoxelCellKey>(256);
    private readonly HashSet<VoxelCellKey> reusableTraversalCellSet = new HashSet<VoxelCellKey>();
    private readonly List<VoxelCellKey> reusableRemovedDisplayCells = new List<VoxelCellKey>(256);

    private readonly List<LightRuntimeState> activeLightStates = new List<LightRuntimeState>(4);
    private readonly HashSet<LightRuntimeState> activeLightStateSet = new HashSet<LightRuntimeState>();
    private int roundRobinLightIndex;

    private bool terrainDirty = true;
    private bool lightSourcesDirty = true;
    private bool hasCalculated;
    private bool invalidSourceLogged;
    private bool noPropagationSourceLogged;

    private readonly struct LightSourceCell
    {
        public readonly object sourceKey;
        public readonly string sourceName;
        public readonly MiningLightProfile profile;
        public readonly VoxelCellKey key;

        public LightSourceCell(
            object sourceKey,
            string sourceName,
            MiningLightProfile profile,
            VoxelCellKey key)
        {
            this.sourceKey = sourceKey;
            this.sourceName = sourceName;
            this.profile = profile;
            this.key = key;
        }
    }

    private sealed class TemporaryLightSource
    {
        private readonly Transform sourceTransform;
        private readonly Vector3 fixedWorldPosition;
        private readonly bool followTransform;
        private readonly float expiresAt;

        public readonly MiningLightProfile profile;
        public readonly string sourceName;

        public TemporaryLightSource(
            Vector3 worldPosition,
            MiningLightProfile profile,
            float lifetimeSeconds)
        {
            fixedWorldPosition = worldPosition;
            this.profile = profile;
            expiresAt = Time.time + lifetimeSeconds;
            sourceName = "TemporaryLight(Fixed)";
        }

        public TemporaryLightSource(
            Transform sourceTransform,
            MiningLightProfile profile,
            float lifetimeSeconds)
        {
            this.sourceTransform = sourceTransform;
            followTransform = true;
            this.profile = profile;
            expiresAt = Time.time + lifetimeSeconds;
            sourceName = sourceTransform != null
                ? $"TemporaryLight({sourceTransform.name})"
                : "TemporaryLight(NullTransform)";
        }

        public bool IsExpired => Time.time >= expiresAt;
        public bool IsInvalid => followTransform && sourceTransform == null;

        public bool TryGetSourceCell(TerrainManager terrainManager, out VoxelCellKey key)
        {
            key = default;
            if (terrainManager == null || terrainManager.VoxelManager == null)
            {
                return false;
            }

            Vector3 worldPosition = followTransform ? sourceTransform.position : fixedWorldPosition;
            return terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(worldPosition, out key);
        }
    }

    private sealed class BurstLightSource
    {
        public readonly object sourceKey;
        public readonly MiningLightProfile profile;
        public readonly AnimationCurve brightnessCurve;
        public readonly float startTime;
        public readonly float expiresAt;
        public readonly float updateIntervalSeconds;
        public readonly string sourceName;
        public readonly VoxelCellKey sourceCell;
        public readonly Dictionary<VoxelCellKey, float> baseBrightness =
            new Dictionary<VoxelCellKey, float>();

        public LightRuntimeState state;
        public float currentTimeBrightness;
        public float nextUpdateAt;
        public int displaySequence;

        public BurstLightSource(
            VoxelCellKey sourceCell,
            MiningLightProfile profile,
            float lifetimeSeconds,
            AnimationCurve brightnessCurve,
            float updateIntervalSeconds)
        {
            sourceKey = this;
            this.sourceCell = sourceCell;
            this.profile = profile;
            this.brightnessCurve = brightnessCurve;
            this.updateIntervalSeconds = Mathf.Max(0.001f, updateIntervalSeconds);
            startTime = Time.time;
            expiresAt = startTime + lifetimeSeconds;
            nextUpdateAt = startTime;
            currentTimeBrightness = EvaluateTimeBrightness();
            sourceName = "TemporaryBurstLight";
        }

        public bool IsExpired => Time.time >= expiresAt;

        public float EvaluateTimeBrightness()
        {
            float duration = Mathf.Max(0.001f, expiresAt - startTime);
            float normalizedTime = Mathf.Clamp01((Time.time - startTime) / duration);
            return Mathf.Clamp01(brightnessCurve.Evaluate(normalizedTime));
        }

        public float CalculateDisplayBrightness(float distanceBrightness)
        {
            return Mathf.Clamp01(distanceBrightness) * currentTimeBrightness;
        }
    }

    private readonly struct SourceCellDisplay
    {
        public readonly int sequence;
        public readonly float brightness;
        public readonly int distanceFromSourceCells;
        public readonly bool hasPredecessor;
        public readonly VoxelCellKey predecessor;
        public readonly int revision;

        public SourceCellDisplay(
            int sequence,
            float brightness,
            int distanceFromSourceCells,
            bool hasPredecessor,
            VoxelCellKey predecessor,
            int revision)
        {
            this.sequence = sequence;
            this.brightness = brightness;
            this.distanceFromSourceCells = distanceFromSourceCells;
            this.hasPredecessor = hasPredecessor;
            this.predecessor = predecessor;
            this.revision = revision;
        }
    }

    private readonly struct TerrainRepairSeed
    {
        public readonly VoxelCellKey key;
        public readonly float brightness;
        public readonly int distanceFromSourceCells;
        public readonly bool hasPredecessor;
        public readonly VoxelCellKey predecessor;

        public TerrainRepairSeed(
            VoxelCellKey key,
            float brightness,
            int distanceFromSourceCells,
            bool hasPredecessor,
            VoxelCellKey predecessor)
        {
            this.key = key;
            this.brightness = brightness;
            this.distanceFromSourceCells = distanceFromSourceCells;
            this.hasPredecessor = hasPredecessor;
            this.predecessor = predecessor;
        }
    }

    private sealed class TerrainRepairRequest
    {
        public readonly List<TerrainRepairSeed> seeds = new List<TerrainRepairSeed>(8);
        public readonly HashSet<VoxelCellKey> seedCells = new HashSet<VoxelCellKey>();
        public readonly List<VoxelCellKey> pruneRoots = new List<VoxelCellKey>(8);
        public readonly HashSet<VoxelCellKey> pruneRootCells = new HashSet<VoxelCellKey>();

        public void AddSeed(TerrainRepairSeed seed)
        {
            if (seedCells.Add(seed.key))
            {
                seeds.Add(seed);
            }
        }

        public void AddPruneRoot(VoxelCellKey root)
        {
            if (pruneRootCells.Add(root))
            {
                pruneRoots.Add(root);
            }
        }
    }

    private sealed class LightRuntimeState
    {
        public readonly object sourceKey;
        public readonly List<PropagationRun> activeRuns = new List<PropagationRun>(4);
        public readonly Dictionary<VoxelCellKey, SourceCellDisplay> displayBrightness =
            new Dictionary<VoxelCellKey, SourceCellDisplay>();
        public readonly Dictionary<VoxelCellKey, HashSet<VoxelCellKey>> displayChildrenByCell =
            new Dictionary<VoxelCellKey, HashSet<VoxelCellKey>>();

        public string sourceName;
        public MiningLightProfile profile;
        public BurstLightSource burstLight;
        public int nextRunSequence;
        public int latestFullSourceSequence = -1;
        public int nextDisplayRevision;
        public int roundRobinRunIndex;

        public LightRuntimeState(object sourceKey, string sourceName, MiningLightProfile profile)
        {
            this.sourceKey = sourceKey;
            this.sourceName = sourceName;
            this.profile = profile;
        }
    }

    private sealed class PropagationRun
    {
        public readonly LightRuntimeState owner;
        public readonly object sourceKey;
        public readonly string sourceName;
        public readonly MiningLightProfile profile;
        public readonly int sequence;
        public readonly PropagationRunKind kind;
        public readonly Dictionary<VoxelCellKey, float> cellBrightness = new Dictionary<VoxelCellKey, float>();
        public readonly Dictionary<VoxelCellKey, int> terrainRepairPrunedDisplayRevisions =
            new Dictionary<VoxelCellKey, int>();
        public readonly Dictionary<VoxelCellKey, bool> propagationCellCache = new Dictionary<VoxelCellKey, bool>();
        public readonly Dictionary<VoxelCellKey, bool> solidCellCache = new Dictionary<VoxelCellKey, bool>();
        public readonly Dictionary<VoxelCellKey, MiningLightProfile> sourceProfiles =
            new Dictionary<VoxelCellKey, MiningLightProfile>();
        public readonly List<PropagationJob> activeJobs = new List<PropagationJob>(32);
        public readonly HashSet<VoxelCellKey> sourceCells = new HashSet<VoxelCellKey>();
        public readonly List<VoxelCellKey> sourceCellOrder = new List<VoxelCellKey>(32);
        public readonly HashSet<VoxelCellKey> terrainRepairPrunedCells = new HashSet<VoxelCellKey>();

        public int roundRobinJobIndex;
        public bool maxCalculatedCellsLogged;
        public bool hasSourceProfile;
        public float minSourceBrightness = 0.001f;
        public float maxSourceBrightness = 1f;

        public PropagationRun(
            LightRuntimeState owner,
            object sourceKey,
            string sourceName,
            MiningLightProfile profile,
            int sequence,
            PropagationRunKind kind)
        {
            this.owner = owner;
            this.sourceKey = sourceKey;
            this.sourceName = sourceName;
            this.profile = profile;
            this.sequence = sequence;
            this.kind = kind;
        }

        public void ClearCellStateCaches()
        {
            propagationCellCache.Clear();
            solidCellCache.Clear();
        }

        public void RecordSourceProfile(MiningLightProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (!hasSourceProfile)
            {
                minSourceBrightness = profile.MinBrightness;
                maxSourceBrightness = profile.Brightness;
                hasSourceProfile = true;
                return;
            }

            minSourceBrightness = Mathf.Min(minSourceBrightness, profile.MinBrightness);
            maxSourceBrightness = Mathf.Max(maxSourceBrightness, profile.Brightness);
        }
    }

    private sealed class PropagationJob
    {
        public readonly VoxelCellKey sourceCell;
        public readonly MiningLightProfile profile;
        public readonly Queue<FrontierCell> frontier = new Queue<FrontierCell>(64);

        public PropagationJob(VoxelCellKey sourceCell, MiningLightProfile profile, float brightness)
            : this(sourceCell, profile, brightness, 0)
        {
        }

        public PropagationJob(
            VoxelCellKey sourceCell,
            MiningLightProfile profile,
            float brightness,
            int distanceFromSourceCells)
        {
            this.sourceCell = sourceCell;
            this.profile = profile;
            frontier.Enqueue(new FrontierCell(sourceCell, brightness, distanceFromSourceCells));
        }
    }

    private readonly struct FrontierCell
    {
        public readonly VoxelCellKey key;
        public readonly float brightness;

        public readonly int distanceFromSourceCells;

        public FrontierCell(VoxelCellKey key, float brightness, int distanceFromSourceCells)
        {
            this.key = key;
            this.brightness = brightness;
            this.distanceFromSourceCells = distanceFromSourceCells;
        }
    }

    public bool TryGetBrightness(VoxelCellKey key, out float brightness)
    {
        return composedBrightness.TryGetValue(key, out brightness);
    }

    public bool TrySampleAverageBrightnessAtWorldPositions(
        Vector3[] worldPositions,
        int positionCount,
        out float averageBrightness)
    {
        averageBrightness = 0f;
        if (worldPositions == null)
        {
            Debug.LogError("MiningLightManager: world position sample buffer is null.", this);
            return false;
        }

        if (positionCount <= 0 || positionCount > worldPositions.Length)
        {
            Debug.LogError(
                $"MiningLightManager: invalid world position sample count. Count={positionCount}, BufferLength={worldPositions.Length}",
                this);
            return false;
        }

        if (!ValidateConfiguration())
        {
            return false;
        }

        float totalBrightness = 0f;
        for (int i = 0; i < positionCount; i++)
        {
            if (!terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(worldPositions[i], out VoxelCellKey key))
            {
                return false;
            }

            totalBrightness += TryGetBrightness(key, out float brightness)
                ? Mathf.Clamp01(brightness)
                : 0f;
        }

        averageBrightness = totalBrightness / positionCount;
        return true;
    }

    public bool SpawnTemporaryLight(
        Vector3 worldPosition,
        MiningLightProfile profile,
        float lifetimeSeconds)
    {
        if (!ValidateTemporaryLightRequest(profile, lifetimeSeconds))
        {
            return false;
        }

        TemporaryLightSource source =
            new TemporaryLightSource(worldPosition, profile, lifetimeSeconds);
        temporaryLightSources.Add(source);
        MarkLightSourcesDirty();
        return true;
    }

    public bool SpawnTemporaryLight(
        Transform sourceTransform,
        MiningLightProfile profile,
        float lifetimeSeconds)
    {
        if (sourceTransform == null)
        {
            Debug.LogError("MiningLightManager: cannot spawn a temporary light with a null Transform.", this);
            return false;
        }

        if (!ValidateTemporaryLightRequest(profile, lifetimeSeconds))
        {
            return false;
        }

        TemporaryLightSource source =
            new TemporaryLightSource(sourceTransform, profile, lifetimeSeconds);
        temporaryLightSources.Add(source);
        MarkLightSourcesDirty();
        return true;
    }

    public bool SpawnTemporaryBurstLight(
        Vector3 worldPosition,
        MiningLightProfile profile,
        float lifetimeSeconds,
        AnimationCurve brightnessCurve,
        float updateIntervalSeconds)
    {
        if (!ValidateTemporaryBurstLightRequest(
                profile,
                lifetimeSeconds,
                brightnessCurve,
                updateIntervalSeconds))
        {
            return false;
        }

        if (!terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(worldPosition, out VoxelCellKey sourceCell))
        {
            Debug.LogError(
                $"MiningLightManager: temporary burst light position could not be converted to a voxel cell. worldPosition={worldPosition}",
                this);
            return false;
        }

        BurstLightSource burstSource =
            new BurstLightSource(sourceCell, profile, lifetimeSeconds, brightnessCurve, updateIntervalSeconds);
        LightRuntimeState state =
            new LightRuntimeState(burstSource.sourceKey, burstSource.sourceName, profile);
        state.burstLight = burstSource;
        burstSource.state = state;
        lightStates.Add(burstSource.sourceKey, state);
        burstLightSources.Add(burstSource);

        PropagationRun propagation = new PropagationRun(
            state,
            burstSource.sourceKey,
            burstSource.sourceName,
            profile,
            state.nextRunSequence++,
            PropagationRunKind.FullSource);
        state.latestFullSourceSequence = propagation.sequence;
        burstSource.displaySequence = propagation.sequence;

        AddSourcePropagation(propagation, sourceCell, profile);
        int radius = Mathf.Max(0, profile.SourceRadiusCells);
        for (int shell = 1; shell <= radius; shell++)
        {
            AddSourceShell(propagation, sourceCell, shell, profile);
        }

        if (propagation.activeJobs.Count == 0)
        {
            Debug.LogWarning("MiningLightManager: temporary burst light did not generate any propagation jobs.", this);
            RemoveBurstLightSource(burstSource);
            return false;
        }

        state.activeRuns.Add(propagation);
        EnsureActiveLightStateQueued(state);
        return true;
    }

    public void RegisterLightSource(MiningLightSource source)
    {
        if (source == null)
        {
            Debug.LogError("MiningLightManager: attempted to register a null light source.", this);
            return;
        }

        if (registeredLightSourceSet.Add(source))
        {
            registeredLightSources.Add(source);
            MarkLightSourcesDirty();
        }
    }

    public void UnregisterLightSource(MiningLightSource source)
    {
        if (source == null)
        {
            return;
        }

        if (registeredLightSourceSet.Remove(source))
        {
            registeredLightSources.Remove(source);
            MarkLightSourcesDirty();
        }
    }

    public void MarkLightSourcesDirty()
    {
        lightSourcesDirty = true;
    }

    public int DrainDirtyBrightnessCells(List<VoxelCellKey> buffer, int maxCells)
    {
        if (buffer == null)
        {
            Debug.LogError("MiningLightManager: dirty brightness cell buffer is null.", this);
            return 0;
        }

        buffer.Clear();
        if (maxCells <= 0)
        {
            Debug.LogError($"MiningLightManager: maxCells must be greater than 0. maxCells={maxCells}", this);
            return 0;
        }

        int drained = 0;
        while (dirtyBrightnessCells.Count > 0 && drained < maxCells)
        {
            VoxelCellKey key = dirtyBrightnessCells.Dequeue();
            queuedDirtyBrightnessCells.Remove(key);
            buffer.Add(key);
            drained++;
        }

        return drained;
    }

    private void OnEnable()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        RegisterInitialLightSources();
        terrainManager.VoxelManager.TerrainCellsChanged += HandleTerrainCellsChanged;
        terrainDirty = true;
        lightSourcesDirty = true;
    }

    private void OnDisable()
    {
        if (terrainManager != null && terrainManager.VoxelManager != null)
        {
            terrainManager.VoxelManager.TerrainCellsChanged -= HandleTerrainCellsChanged;
        }
    }

    private void Update()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        UpdateBurstLightSources();
        PruneExpiredTemporaryLightSources();
        CollectCurrentLightSourceCells(currentLightSourceCells);
        if (currentLightSourceCells.Count == 0)
        {
            if (burstLightSources.Count == 0)
            {
                ClearLightState();
                if (!noPropagationSourceLogged)
                {
                    noPropagationSourceLogged = true;
                    Debug.LogWarning("MiningLightManager: no registered light sources were available for light propagation.", this);
                }
                return;
            }

            HashSet<object> nextSourceKeys = new HashSet<object>();
            AddActiveBurstLightKeys(nextSourceKeys);
            RemoveMissingLightStates(nextSourceKeys);
            StoreLightSourceState(currentLightSourceCells);
            terrainDirty = false;
            hasCalculated = true;
            noPropagationSourceLogged = false;
        }
        else
        {
            noPropagationSourceLogged = false;

            if (!hasCalculated || terrainDirty || HaveLightSourcesChanged(currentLightSourceCells))
            {
                RestartPropagation(currentLightSourceCells);
            }
        }

        if (pendingTerrainRepairRequests.Count > 0)
        {
            ProcessTerrainRepairRequests();
        }

        if (activeLightStates.Count > 0)
        {
            ProcessPropagationStep();
        }
    }

    private void UpdateBurstLightSources()
    {
        if (burstLightSources.Count == 0)
        {
            return;
        }

        using (BurstLightUpdateMarker.Auto())
        {
            for (int i = burstLightSources.Count - 1; i >= 0; i--)
            {
                BurstLightSource source = burstLightSources[i];
                if (source == null || source.state == null || source.IsExpired)
                {
                    RemoveBurstLightSourceAt(i);
                    continue;
                }

                if (Time.time < source.nextUpdateAt)
                {
                    continue;
                }

                source.nextUpdateAt = Time.time + source.updateIntervalSeconds;
                float nextTimeBrightness = source.EvaluateTimeBrightness();
                if (Mathf.Approximately(source.currentTimeBrightness, nextTimeBrightness))
                {
                    continue;
                }

                source.currentTimeBrightness = nextTimeBrightness;
                UpdateBurstDisplayBrightness(source);
            }
        }
    }

    private void RegisterInitialLightSources()
    {
        if (initialLightSources == null)
        {
            Debug.LogError("MiningLightManager: Initial Light Sources list is null.", this);
            return;
        }

        for (int i = 0; i < initialLightSources.Count; i++)
        {
            MiningLightSource source = initialLightSources[i];
            if (source == null)
            {
                Debug.LogError($"MiningLightManager: Initial Light Sources contains a null entry at index {i}.", this);
                continue;
            }

            RegisterLightSource(source);
        }
    }

    private void RemoveBurstLightSourceAt(int index)
    {
        if (index < 0 || index >= burstLightSources.Count)
        {
            return;
        }

        BurstLightSource source = burstLightSources[index];
        burstLightSources.RemoveAt(index);
        if (source != null)
        {
            RemoveLightState(source.sourceKey);
        }
    }

    private void RemoveBurstLightSource(BurstLightSource source)
    {
        if (source == null)
        {
            return;
        }

        burstLightSources.Remove(source);
        RemoveLightState(source.sourceKey);
    }

    private void PruneExpiredTemporaryLightSources()
    {
        for (int i = temporaryLightSources.Count - 1; i >= 0; i--)
        {
            TemporaryLightSource source = temporaryLightSources[i];
            if (source == null || source.IsExpired || source.IsInvalid)
            {
                temporaryLightSources.RemoveAt(i);
                MarkLightSourcesDirty();
            }
        }
    }

    private void CollectCurrentLightSourceCells(List<LightSourceCell> buffer)
    {
        buffer.Clear();
        bool foundInvalidSource = false;

        for (int i = registeredLightSources.Count - 1; i >= 0; i--)
        {
            MiningLightSource source = registeredLightSources[i];
            if (source == null)
            {
                registeredLightSources.RemoveAt(i);
                lightSourcesDirty = true;
                continue;
            }

            if (!source.isActiveAndEnabled)
            {
                continue;
            }

            MiningLightProfile profile = source.Profile;
            if (profile == null)
            {
                foundInvalidSource = true;
                Debug.LogError("MiningLightManager: registered light source has no MiningLightProfile.", source);
                continue;
            }

            if (!source.TryGetSourceCell(terrainManager, out VoxelCellKey sourceCell))
            {
                foundInvalidSource = true;
                continue;
            }

            buffer.Add(new LightSourceCell(source, source.name, profile, sourceCell));
        }

        for (int i = temporaryLightSources.Count - 1; i >= 0; i--)
        {
            TemporaryLightSource source = temporaryLightSources[i];
            if (source == null || source.IsExpired || source.IsInvalid)
            {
                temporaryLightSources.RemoveAt(i);
                lightSourcesDirty = true;
                continue;
            }

            if (source.profile == null)
            {
                foundInvalidSource = true;
                Debug.LogError("MiningLightManager: temporary light source has no MiningLightProfile.", this);
                temporaryLightSources.RemoveAt(i);
                lightSourcesDirty = true;
                continue;
            }

            if (!source.TryGetSourceCell(terrainManager, out VoxelCellKey sourceCell))
            {
                foundInvalidSource = true;
                continue;
            }

            buffer.Add(new LightSourceCell(source, source.sourceName, source.profile, sourceCell));
        }

        if (foundInvalidSource)
        {
            if (!invalidSourceLogged)
            {
                invalidSourceLogged = true;
                Debug.LogWarning("MiningLightManager: one or more light sources could not be converted to voxel cells.", this);
            }
        }
        else
        {
            invalidSourceLogged = false;
        }
    }

    private bool HaveLightSourcesChanged(List<LightSourceCell> sourceCells)
    {
        if (lightSourcesDirty || sourceCells.Count != lastLightSourceCells.Count)
        {
            return true;
        }

        for (int i = 0; i < sourceCells.Count; i++)
        {
            LightSourceCell sourceCell = sourceCells[i];
            if (!lastLightSourceCells.TryGetValue(sourceCell.sourceKey, out VoxelCellKey previousCell) ||
                !previousCell.Equals(sourceCell.key))
            {
                return true;
            }

            if (!lastLightSourceProfiles.TryGetValue(sourceCell.sourceKey, out MiningLightProfile previousProfile) ||
                previousProfile != sourceCell.profile)
            {
                return true;
            }

        }

        return false;
    }

    private void StoreLightSourceState(List<LightSourceCell> sourceCells)
    {
        lastLightSourceCells.Clear();
        lastLightSourceProfiles.Clear();

        for (int i = 0; i < sourceCells.Count; i++)
        {
            LightSourceCell sourceCell = sourceCells[i];
            lastLightSourceCells[sourceCell.sourceKey] = sourceCell.key;
            lastLightSourceProfiles[sourceCell.sourceKey] = sourceCell.profile;
        }

        lightSourcesDirty = false;
    }

    private void RestartPropagation(List<LightSourceCell> sourceCells)
    {
        using (RestartPropagationMarker.Auto())
        {
            HashSet<object> nextSourceKeys = new HashSet<object>();
            bool restartForTerrain = terrainDirty || !hasCalculated;

            terrainDirty = false;
            hasCalculated = true;

            for (int i = 0; i < sourceCells.Count; i++)
            {
                LightSourceCell lightSource = sourceCells[i];
                nextSourceKeys.Add(lightSource.sourceKey);
                LightRuntimeState state = GetOrCreateLightState(lightSource);
                state.sourceName = lightSource.sourceName;
                state.profile = lightSource.profile;

                if (!restartForTerrain && !HasLightSourceChanged(lightSource))
                {
                    EnsureActiveLightStateQueued(state);
                    continue;
                }

                QueuePropagationRun(
                    state,
                    lightSource.sourceKey,
                    lightSource.sourceName,
                    lightSource.profile,
                    lightSource.key,
                    null,
                    PropagationRunKind.FullSource);
            }

            AddActiveBurstLightKeys(nextSourceKeys);
            RemoveMissingLightStates(nextSourceKeys);

            StoreLightSourceState(sourceCells);
            ClampRoundRobinLightIndex();

            if (activeLightStates.Count == 0 && lightStates.Count == 0)
            {
                if (!noPropagationSourceLogged)
                {
                    noPropagationSourceLogged = true;
                    Debug.LogWarning("MiningLightManager: no generated light source cells were available for light propagation.", this);
                }
                return;
            }

            noPropagationSourceLogged = false;
        }
    }

    private void ProcessTerrainRepairRequests()
    {
        if (pendingTerrainRepairRequests.Count == 0)
        {
            return;
        }

        using (RestartTerrainAffectedSourcesMarker.Auto())
        {
            reusableSourceBuffer.Clear();
            foreach (KeyValuePair<object, TerrainRepairRequest> pair in pendingTerrainRepairRequests)
            {
                reusableSourceBuffer.Add(pair.Key);
            }

            for (int i = 0; i < reusableSourceBuffer.Count; i++)
            {
                object sourceKey = reusableSourceBuffer[i];
                if (!pendingTerrainRepairRequests.TryGetValue(sourceKey, out TerrainRepairRequest request))
                {
                    continue;
                }

                if (!lightStates.TryGetValue(sourceKey, out LightRuntimeState state) || state == null)
                {
                    continue;
                }

                if (state.profile == null)
                {
                    Debug.LogError($"MiningLightManager: source '{state.sourceName}' has no MiningLightProfile while processing terrain repair.", this);
                    continue;
                }

                QueueTerrainRepairRun(state, request);
            }

            pendingTerrainRepairRequests.Clear();
            reusableSourceBuffer.Clear();
            ClampRoundRobinLightIndex();
        }
    }

    private bool QueueTerrainRepairRun(LightRuntimeState state, TerrainRepairRequest request)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while queueing terrain repair.", this);
            return false;
        }

        if (state.profile == null)
        {
            Debug.LogError($"MiningLightManager: source '{state.sourceName}' has no MiningLightProfile while queueing terrain repair.", this);
            return false;
        }

        if (request == null)
        {
            Debug.LogError("MiningLightManager: terrain repair request is null.", this);
            return false;
        }

        int sequence = state.latestFullSourceSequence >= 0
            ? state.latestFullSourceSequence
            : state.nextRunSequence++;
        PropagationRun repairPropagation = new PropagationRun(
            state,
            state.sourceKey,
            state.sourceName,
            state.profile,
            sequence,
            PropagationRunKind.TerrainRepair);
        CollectTerrainRepairPrunedCells(
            state,
            request.pruneRoots,
            repairPropagation.terrainRepairPrunedCells,
            repairPropagation.terrainRepairPrunedDisplayRevisions);
        if (repairPropagation.terrainRepairPrunedCells.Count > 0)
        {
            RemoveCellsFromActivePropagationRuns(state, repairPropagation.terrainRepairPrunedCells);
        }

        BurstLightSource burstSource = state.burstLight;
        if (burstSource != null)
        {
            burstSource.displaySequence = repairPropagation.sequence;
        }

        for (int i = 0; i < request.seeds.Count; i++)
        {
            TerrainRepairSeed seed = request.seeds[i];
            if (!AddOrUpdateCell(
                    repairPropagation,
                    seed.key,
                    seed.brightness,
                    seed.distanceFromSourceCells,
                    seed.hasPredecessor,
                    seed.predecessor))
            {
                continue;
            }

            repairPropagation.activeJobs.Add(new PropagationJob(
                seed.key,
                state.profile,
                seed.brightness,
                seed.distanceFromSourceCells));
        }

        if (repairPropagation.activeJobs.Count == 0)
        {
            return false;
        }

        state.activeRuns.Add(repairPropagation);
        EnsureActiveLightStateQueued(state);
        return true;
    }

    private void CollectTerrainRepairPrunedCells(
        LightRuntimeState state,
        List<VoxelCellKey> pruneRoots,
        HashSet<VoxelCellKey> buffer,
        Dictionary<VoxelCellKey, int> displayRevisions)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while collecting terrain repair pruned cells.", this);
            return;
        }

        if (pruneRoots == null)
        {
            Debug.LogError("MiningLightManager: terrain repair prune root list is null.", this);
            return;
        }

        if (buffer == null)
        {
            Debug.LogError("MiningLightManager: terrain repair pruned cell buffer is null.", this);
            return;
        }

        if (displayRevisions == null)
        {
            Debug.LogError("MiningLightManager: terrain repair pruned display revision buffer is null.", this);
            return;
        }

        buffer.Clear();
        displayRevisions.Clear();
        reusableTraversalCells.Clear();
        reusableTraversalCellSet.Clear();

        for (int i = 0; i < pruneRoots.Count; i++)
        {
            VoxelCellKey root = pruneRoots[i];
            if (state.displayBrightness.ContainsKey(root) && reusableTraversalCellSet.Add(root))
            {
                reusableTraversalCells.Add(root);
            }
        }

        for (int i = 0; i < reusableTraversalCells.Count; i++)
        {
            VoxelCellKey key = reusableTraversalCells[i];
            if (!state.displayChildrenByCell.TryGetValue(key, out HashSet<VoxelCellKey> children))
            {
                continue;
            }

            foreach (VoxelCellKey child in children)
            {
                if (reusableTraversalCellSet.Add(child))
                {
                    reusableTraversalCells.Add(child);
                }
            }
        }

        for (int i = 0; i < reusableTraversalCells.Count; i++)
        {
            VoxelCellKey key = reusableTraversalCells[i];
            buffer.Add(key);
            if (state.displayBrightness.TryGetValue(key, out SourceCellDisplay display))
            {
                displayRevisions[key] = display.revision;
            }
        }

        reusableTraversalCells.Clear();
        reusableTraversalCellSet.Clear();
    }

    private void RemoveCellsFromActivePropagationRuns(LightRuntimeState state, HashSet<VoxelCellKey> cells)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while pruning active propagation cells.", this);
            return;
        }

        if (cells == null)
        {
            Debug.LogError("MiningLightManager: propagation prune cell list is null.", this);
            return;
        }

        for (int i = 0; i < state.activeRuns.Count; i++)
        {
            PropagationRun run = state.activeRuns[i];
            if (run == null)
            {
                continue;
            }

            foreach (VoxelCellKey cell in cells)
            {
                run.cellBrightness.Remove(cell);
            }
        }
    }

    private bool QueuePropagationRun(
        LightRuntimeState state,
        object sourceKey,
        string sourceName,
        MiningLightProfile profile,
        VoxelCellKey sourceCell,
        BurstLightSource burstSource,
        PropagationRunKind kind)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while queueing propagation.", this);
            return false;
        }

        if (sourceKey == null)
        {
            Debug.LogError("MiningLightManager: source key is null while queueing propagation.", this);
            return false;
        }

        if (profile == null)
        {
            Debug.LogError($"MiningLightManager: source '{sourceName}' has no MiningLightProfile while queueing propagation.", this);
            return false;
        }

        if (burstSource != null)
        {
            burstSource.baseBrightness.Clear();
        }

        int sequence = kind == PropagationRunKind.TerrainRepair && state.latestFullSourceSequence >= 0
            ? state.latestFullSourceSequence
            : state.nextRunSequence++;
        if (kind == PropagationRunKind.FullSource)
        {
            state.latestFullSourceSequence = sequence;
        }

        PropagationRun nextPropagation = new PropagationRun(
            state,
            sourceKey,
            sourceName,
            profile,
            sequence,
            kind);

        if (burstSource != null)
        {
            burstSource.displaySequence = nextPropagation.sequence;
        }

        AddSourcePropagation(nextPropagation, sourceCell, profile);

        int radius = Mathf.Max(0, profile.SourceRadiusCells);
        for (int shell = 1; shell <= radius; shell++)
        {
            AddSourceShell(nextPropagation, sourceCell, shell, profile);
        }

        if (nextPropagation.activeJobs.Count == 0)
        {
            RemoveStaleSourceDisplayCells(nextPropagation);
            return false;
        }

        state.activeRuns.Add(nextPropagation);
        EnsureActiveLightStateQueued(state);
        return true;
    }

    private LightRuntimeState GetOrCreateLightState(LightSourceCell lightSource)
    {
        if (!lightStates.TryGetValue(lightSource.sourceKey, out LightRuntimeState state))
        {
            state = new LightRuntimeState(lightSource.sourceKey, lightSource.sourceName, lightSource.profile);
            lightStates.Add(lightSource.sourceKey, state);
        }

        return state;
    }

    private bool HasLightSourceChanged(LightSourceCell sourceCell)
    {
        if (!lastLightSourceCells.TryGetValue(sourceCell.sourceKey, out VoxelCellKey previousCell) ||
            !previousCell.Equals(sourceCell.key))
        {
            return true;
        }

        if (!lastLightSourceProfiles.TryGetValue(sourceCell.sourceKey, out MiningLightProfile previousProfile) ||
            previousProfile != sourceCell.profile)
        {
            return true;
        }

        return false;
    }

    private void AddSourceShell(PropagationRun propagation, VoxelCellKey centerCell, int shell, MiningLightProfile profile)
    {
        for (int x = -shell; x <= shell; x++)
        {
            for (int y = -shell; y <= shell; y++)
            {
                for (int z = -shell; z <= shell; z++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != shell)
                    {
                        continue;
                    }

                    if (TryGetOffsetCell(centerCell, new Vector3Int(x, y, z), out VoxelCellKey sourceCell))
                    {
                        AddSourcePropagation(propagation, sourceCell, profile);
                    }
                }
            }
        }
    }

    private void AddSourcePropagation(PropagationRun propagation, VoxelCellKey sourceCell, MiningLightProfile profile)
    {
        if (profile == null)
        {
            Debug.LogError("MiningLightManager: MiningLightProfile is null while adding a propagation source.", this);
            return;
        }

        if (!IsLightPropagationCellCached(propagation, sourceCell))
        {
            return;
        }

        float brightness = Mathf.Clamp01(profile.Brightness);
        if (brightness <= 0f)
        {
            return;
        }

        if (!AddOrUpdateCell(propagation, sourceCell, brightness, 0, false, default))
        {
            return;
        }

        if (propagation.sourceCells.Add(sourceCell))
        {
            propagation.sourceCellOrder.Add(sourceCell);
            propagation.sourceProfiles.Add(sourceCell, profile);
        }

        propagation.RecordSourceProfile(profile);
        propagation.activeJobs.Add(new PropagationJob(sourceCell, profile, brightness));
    }

    private void ProcessPropagationStep()
    {
        using (PropagationStepMarker.Auto())
        {
            int processedCells = 0;
            int globalMaxCellsThisFrame = Mathf.Max(1, maxPropagationCellsPerFrame);
            int lightSlotsThisFrame = activeLightStates.Count;
            while (activeLightStates.Count > 0 &&
                   lightSlotsThisFrame > 0 &&
                   processedCells < globalMaxCellsThisFrame)
            {
                ClampRoundRobinLightIndex();
                LightRuntimeState state = activeLightStates[roundRobinLightIndex];
                if (state.profile == null)
                {
                    Debug.LogError($"MiningLightManager: light source '{state.sourceName}' has no MiningLightProfile while processing propagation.", this);
                    RemoveActiveLightStateAt(roundRobinLightIndex);
                    lightSlotsThisFrame--;
                    continue;
                }

                int sourceFrameBudget = Mathf.Min(
                    state.profile.MaxPropagationCellsPerLightPerFrame,
                    globalMaxCellsThisFrame - processedCells);

                int processedSourceCells = 0;
                int runSlotsThisFrame = state.activeRuns.Count;
                while (state.activeRuns.Count > 0 &&
                       runSlotsThisFrame > 0 &&
                       processedSourceCells < sourceFrameBudget &&
                       processedCells < globalMaxCellsThisFrame)
                {
                    int remainingSourceBudget = sourceFrameBudget - processedSourceCells;
                    int remainingGlobalBudget = globalMaxCellsThisFrame - processedCells;
                    int processed = ProcessOnePropagationRunSlice(
                        state,
                        Mathf.Min(remainingSourceBudget, remainingGlobalBudget));
                    if (processed <= 0)
                    {
                        break;
                    }

                    processedSourceCells += processed;
                    processedCells += processed;
                    runSlotsThisFrame--;
                }

                if (state.activeRuns.Count == 0)
                {
                    RemoveActiveLightStateAt(roundRobinLightIndex);
                    lightSlotsThisFrame--;
                    continue;
                }

                roundRobinLightIndex++;
                lightSlotsThisFrame--;
            }
        }
    }

    private int ProcessOnePropagationRunSlice(LightRuntimeState state, int maxCells)
    {
        if (state.activeRuns.Count == 0 || maxCells <= 0)
        {
            return 0;
        }

        if (state.roundRobinRunIndex >= state.activeRuns.Count)
        {
            state.roundRobinRunIndex = 0;
        }

        PropagationRun run = state.activeRuns[state.roundRobinRunIndex];
        if (run.profile == null)
        {
            Debug.LogError($"MiningLightManager: propagation run for source '{run.sourceName}' has no MiningLightProfile.", this);
            CompletePropagation(state, run);
            return 0;
        }

        int processed = 0;
        int runFrameBudget = Mathf.Min(run.profile.MaxPropagationCellsPerRunPerFrame, maxCells);
        int jobSlotsThisFrame = run.activeJobs.Count;
        while (run.activeJobs.Count > 0 &&
               jobSlotsThisFrame > 0 &&
               processed < runFrameBudget)
        {
            int processedJobCells = ProcessOnePropagationJobSlice(run, runFrameBudget - processed);
            processed += processedJobCells;
            jobSlotsThisFrame--;

            if (processedJobCells <= 0 && run.activeJobs.Count == 0)
            {
                break;
            }
        }

        if (run.activeJobs.Count == 0)
        {
            CompletePropagation(state, run);
            return processed;
        }

        state.roundRobinRunIndex++;
        if (state.roundRobinRunIndex >= state.activeRuns.Count)
        {
            state.roundRobinRunIndex = 0;
        }

        return processed;
    }

    private int ProcessOnePropagationJobSlice(PropagationRun propagation, int maxCells)
    {
        if (propagation.activeJobs.Count == 0 || maxCells <= 0)
        {
            return 0;
        }

        if (propagation.roundRobinJobIndex >= propagation.activeJobs.Count)
        {
            propagation.roundRobinJobIndex = 0;
        }

        PropagationJob job = propagation.activeJobs[propagation.roundRobinJobIndex];
        int processedCells = 0;
        while (job.frontier.Count > 0 && processedCells < maxCells)
        {
            ProcessOneFrontierCell(propagation, job);
            processedCells++;
        }

        bool jobStillActive = job.frontier.Count > 0;
        if (!jobStillActive)
        {
            propagation.activeJobs.RemoveAt(propagation.roundRobinJobIndex);
            if (propagation.roundRobinJobIndex >= propagation.activeJobs.Count)
            {
                propagation.roundRobinJobIndex = 0;
            }
            return processedCells;
        }

        propagation.roundRobinJobIndex++;
        if (propagation.roundRobinJobIndex >= propagation.activeJobs.Count)
        {
            propagation.roundRobinJobIndex = 0;
        }

        return processedCells;
    }

    private void CompletePropagation(LightRuntimeState state, PropagationRun propagation)
    {
        if (propagation.kind == PropagationRunKind.FullSource)
        {
            RemoveStaleSourceDisplayCells(propagation);
        }
        else if (propagation.kind == PropagationRunKind.TerrainRepair)
        {
            RemoveTerrainRepairStaleDisplayCells(propagation);
        }

        int index = state.activeRuns.IndexOf(propagation);
        if (index >= 0)
        {
            state.activeRuns.RemoveAt(index);
            if (state.roundRobinRunIndex >= state.activeRuns.Count)
            {
                state.roundRobinRunIndex = 0;
            }
        }
    }

    private void RemoveStaleSourceDisplayCells(PropagationRun propagation)
    {
        if (propagation == null || propagation.owner == null)
        {
            Debug.LogError("MiningLightManager: propagation owner is null while removing stale brightness cells.", this);
            return;
        }

        LightRuntimeState state = propagation.owner;
        reusableCellBuffer.Clear();
        foreach (KeyValuePair<VoxelCellKey, SourceCellDisplay> pair in state.displayBrightness)
        {
            if (pair.Value.sequence < propagation.sequence &&
                !propagation.cellBrightness.ContainsKey(pair.Key))
            {
                reusableCellBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < reusableCellBuffer.Count; i++)
        {
            VoxelCellKey key = reusableCellBuffer[i];
            if (state.displayBrightness.TryGetValue(key, out SourceCellDisplay display) &&
                display.sequence < propagation.sequence)
            {
                RemoveDisplayChildIndex(state, key, display);
                state.displayBrightness.Remove(key);
                state.displayChildrenByCell.Remove(key);
                RecomposeBrightnessCell(key);
            }
        }

        reusableCellBuffer.Clear();
    }

    private void RemoveTerrainRepairStaleDisplayCells(PropagationRun propagation)
    {
        if (propagation == null || propagation.owner == null)
        {
            Debug.LogError("MiningLightManager: propagation owner is null while removing terrain repair stale brightness cells.", this);
            return;
        }

        if (propagation.terrainRepairPrunedCells.Count == 0)
        {
            return;
        }

        LightRuntimeState state = propagation.owner;
        reusableCellBuffer.Clear();
        foreach (KeyValuePair<VoxelCellKey, int> pair in propagation.terrainRepairPrunedDisplayRevisions)
        {
            VoxelCellKey key = pair.Key;
            if (!propagation.cellBrightness.ContainsKey(key))
            {
                reusableCellBuffer.Add(key);
            }
        }

        for (int i = 0; i < reusableCellBuffer.Count; i++)
        {
            VoxelCellKey key = reusableCellBuffer[i];
            if (!state.displayBrightness.TryGetValue(key, out SourceCellDisplay display) ||
                display.revision != propagation.terrainRepairPrunedDisplayRevisions[key])
            {
                continue;
            }

            RemoveDisplayChildIndex(state, key, display);
            state.displayBrightness.Remove(key);
            state.displayChildrenByCell.Remove(key);
            state.burstLight?.baseBrightness.Remove(key);
            RecomposeBrightnessCell(key);
        }

        reusableCellBuffer.Clear();
    }

    private void ClampRoundRobinLightIndex()
    {
        if (activeLightStates.Count == 0)
        {
            roundRobinLightIndex = 0;
            return;
        }

        if (roundRobinLightIndex >= activeLightStates.Count)
        {
            roundRobinLightIndex = 0;
        }
    }

    private void EnsureActiveLightStateQueued(LightRuntimeState state)
    {
        if (state == null || state.activeRuns.Count == 0)
        {
            return;
        }

        if (activeLightStateSet.Add(state))
        {
            activeLightStates.Add(state);
        }
    }

    private void RemoveActiveLightStateAt(int index)
    {
        if (index < 0 || index >= activeLightStates.Count)
        {
            return;
        }

        LightRuntimeState state = activeLightStates[index];
        activeLightStateSet.Remove(state);
        activeLightStates.RemoveAt(index);
        ClampRoundRobinLightIndex();
    }

    private bool ProcessOneFrontierCell(PropagationRun propagation, PropagationJob job)
    {
        if (job.frontier.Count == 0)
        {
            return false;
        }

        FrontierCell current = job.frontier.Dequeue();
        if (propagation.cellBrightness.TryGetValue(current.key, out float recordedBrightness) &&
            current.brightness >= recordedBrightness)
        {
            PropagateFromCell(propagation, job, current);
        }

        return job.frontier.Count > 0;
    }

    private void PropagateFromCell(PropagationRun propagation, PropagationJob job, FrontierCell current)
    {
        if (job.profile == null)
        {
            Debug.LogError("MiningLightManager: propagation job has no MiningLightProfile.", this);
            return;
        }

        Vector3Int[] neighborOffsets = GetNeighborOffsets(job.profile);
        for (int i = 0; i < neighborOffsets.Length; i++)
        {
            if (!TryGetOffsetCell(current.key, neighborOffsets[i], out VoxelCellKey neighbor) ||
                !IsLightPropagationCellCached(propagation, neighbor))
            {
                continue;
            }

            bool solid = IsSolidCellCached(propagation, neighbor);
            float transmission = solid ? job.profile.SolidCellTransmission : job.profile.AirCellTransmission;
            int nextDistanceFromSourceCells = current.distanceFromSourceCells + 1;
            bool attenuate = nextDistanceFromSourceCells > job.profile.FalloffStartDistanceCells;
            float nextBrightness = attenuate ? current.brightness * transmission : current.brightness;
            if (nextBrightness < job.profile.MinBrightness)
            {
                continue;
            }

            if (AddOrUpdateCell(propagation, neighbor, nextBrightness, nextDistanceFromSourceCells, true, current.key))
            {
                job.frontier.Enqueue(new FrontierCell(neighbor, nextBrightness, nextDistanceFromSourceCells));
            }
        }
    }

    private static Vector3Int[] GetNeighborOffsets(MiningLightProfile profile)
    {
        if (profile == null)
        {
            return OrthogonalNeighborOffsets;
        }

        switch (profile.PropagationNeighborhood)
        {
            case MiningLightPropagationNeighborhood.Orthogonal6:
                return OrthogonalNeighborOffsets;
            case MiningLightPropagationNeighborhood.FaceAndEdge18:
                return FaceAndEdgeNeighborOffsets;
            case MiningLightPropagationNeighborhood.Full26:
                return FullNeighborOffsets;
            default:
                return OrthogonalNeighborOffsets;
        }
    }

    private bool AddOrUpdateCell(
        PropagationRun propagation,
        VoxelCellKey key,
        float brightness,
        int distanceFromSourceCells,
        bool hasPredecessor,
        VoxelCellKey predecessor)
    {
        if (propagation.cellBrightness.TryGetValue(key, out float existingBrightness))
        {
            if (brightness <= existingBrightness)
            {
                return false;
            }

            propagation.cellBrightness[key] = brightness;
            UpdateSourceDisplayBrightness(
                propagation,
                key,
                brightness,
                distanceFromSourceCells,
                hasPredecessor,
                predecessor);
            return true;
        }

        if (propagation.cellBrightness.Count >= maxCalculatedCells)
        {
            if (!propagation.maxCalculatedCellsLogged)
            {
                propagation.maxCalculatedCellsLogged = true;
                Debug.LogWarning(
                    $"MiningLightManager: source '{propagation.sourceName}' reached maxCalculatedCells={maxCalculatedCells}. Increase the manager limit if light is visibly clipped.",
                    this);
            }
            return false;
        }

        propagation.cellBrightness.Add(key, brightness);
        UpdateSourceDisplayBrightness(
            propagation,
            key,
            brightness,
            distanceFromSourceCells,
            hasPredecessor,
            predecessor);
        return true;
    }

    private void UpdateSourceDisplayBrightness(
        PropagationRun propagation,
        VoxelCellKey key,
        float brightness,
        int distanceFromSourceCells,
        bool hasPredecessor,
        VoxelCellKey predecessor)
    {
        if (propagation == null || propagation.owner == null)
        {
            Debug.LogError("MiningLightManager: propagation owner is null while updating source brightness.", this);
            return;
        }

        LightRuntimeState state = propagation.owner;
        BurstLightSource burstSource = state.burstLight;
        float baseBrightness = brightness;
        if (burstSource != null)
        {
            brightness = burstSource.CalculateDisplayBrightness(brightness);
        }

        if (ShouldApplySourceDisplayBrightness(propagation, state, key, brightness))
        {
            if (burstSource != null)
            {
                burstSource.baseBrightness[key] = baseBrightness;
            }
        }
        else
        {
            return;
        }

        SetSourceDisplayBrightness(
            state,
            propagation.sequence,
            key,
            brightness,
            distanceFromSourceCells,
            hasPredecessor,
            predecessor);
    }

    private void SetSourceDisplayBrightness(
        LightRuntimeState state,
        int sequence,
        VoxelCellKey key,
        float brightness,
        int distanceFromSourceCells,
        bool hasPredecessor,
        VoxelCellKey predecessor)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while setting source brightness.", this);
            return;
        }

        bool hasExisting = state.displayBrightness.TryGetValue(key, out SourceCellDisplay existing);
        if (hasExisting && existing.sequence > sequence)
        {
            return;
        }

        if (brightness <= 0f)
        {
            if (hasExisting && existing.sequence <= sequence)
            {
                RemoveDisplayChildIndex(state, key, existing);
                state.displayBrightness.Remove(key);
                RecomposeBrightnessCell(key);
            }
            return;
        }

        if (hasExisting &&
            existing.sequence == sequence &&
            Mathf.Approximately(existing.brightness, brightness) &&
            existing.distanceFromSourceCells == distanceFromSourceCells &&
            existing.hasPredecessor == hasPredecessor &&
            (!hasPredecessor || existing.predecessor.Equals(predecessor)))
        {
            return;
        }

        UpdateDisplayChildIndex(state, key, hasExisting, existing, hasPredecessor, predecessor);
        state.displayBrightness[key] = new SourceCellDisplay(
            sequence,
            brightness,
            distanceFromSourceCells,
            hasPredecessor,
            predecessor,
            state.nextDisplayRevision++);
        RecomposeBrightnessCell(key);
    }

    private void UpdateDisplayChildIndex(
        LightRuntimeState state,
        VoxelCellKey key,
        bool hasExisting,
        SourceCellDisplay existing,
        bool hasPredecessor,
        VoxelCellKey predecessor)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while updating display child index.", this);
            return;
        }

        if (hasExisting)
        {
            RemoveDisplayChildIndex(state, key, existing);
        }

        if (!hasPredecessor)
        {
            return;
        }

        if (!state.displayChildrenByCell.TryGetValue(predecessor, out HashSet<VoxelCellKey> children))
        {
            children = new HashSet<VoxelCellKey>();
            state.displayChildrenByCell.Add(predecessor, children);
        }

        children.Add(key);
    }

    private void RemoveDisplayChildIndex(
        LightRuntimeState state,
        VoxelCellKey key,
        SourceCellDisplay display)
    {
        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while removing display child index.", this);
            return;
        }

        if (!display.hasPredecessor)
        {
            return;
        }

        if (!state.displayChildrenByCell.TryGetValue(display.predecessor, out HashSet<VoxelCellKey> children))
        {
            return;
        }

        children.Remove(key);
        if (children.Count == 0)
        {
            state.displayChildrenByCell.Remove(display.predecessor);
        }
    }

    private void UpdateBurstDisplayBrightness(BurstLightSource burstSource)
    {
        if (burstSource == null || burstSource.state == null)
        {
            Debug.LogError("MiningLightManager: burst light source is invalid while updating display brightness.", this);
            return;
        }

        foreach (KeyValuePair<VoxelCellKey, float> pair in burstSource.baseBrightness)
        {
            SourceCellDisplay existingDisplay = burstSource.state.displayBrightness.TryGetValue(
                pair.Key,
                out SourceCellDisplay display)
                ? display
                : new SourceCellDisplay(burstSource.displaySequence, 0f, 0, false, default, -1);
            SetSourceDisplayBrightness(
                burstSource.state,
                burstSource.displaySequence,
                pair.Key,
                burstSource.CalculateDisplayBrightness(pair.Value),
                existingDisplay.distanceFromSourceCells,
                existingDisplay.hasPredecessor,
                existingDisplay.predecessor);
        }
    }

    private bool ShouldApplySourceDisplayBrightness(
        PropagationRun propagation,
        LightRuntimeState state,
        VoxelCellKey key,
        float brightness)
    {
        if (propagation == null || propagation.kind != PropagationRunKind.TerrainRepair)
        {
            return true;
        }

        if (propagation.terrainRepairPrunedCells.Contains(key))
        {
            return true;
        }

        if (state == null)
        {
            Debug.LogError("MiningLightManager: light runtime state is null while checking terrain repair brightness.", this);
            return false;
        }

        if (!state.displayBrightness.TryGetValue(key, out SourceCellDisplay existing))
        {
            return true;
        }

        return brightness > existing.brightness && !Mathf.Approximately(brightness, existing.brightness);
    }

    private void AddActiveBurstLightKeys(HashSet<object> sourceKeys)
    {
        if (sourceKeys == null)
        {
            Debug.LogError("MiningLightManager: source key set is null while adding burst light keys.", this);
            return;
        }

        for (int i = 0; i < burstLightSources.Count; i++)
        {
            BurstLightSource source = burstLightSources[i];
            if (source != null && source.state != null && !source.IsExpired)
            {
                sourceKeys.Add(source.sourceKey);
            }
        }
    }

    private void RemoveMissingLightStates(HashSet<object> nextSourceKeys)
    {
        reusableSourceBuffer.Clear();
        foreach (KeyValuePair<object, LightRuntimeState> pair in lightStates)
        {
            if (!nextSourceKeys.Contains(pair.Key))
            {
                reusableSourceBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < reusableSourceBuffer.Count; i++)
        {
            RemoveLightState(reusableSourceBuffer[i]);
        }

        reusableSourceBuffer.Clear();
    }

    private void RemoveLightState(object sourceKey)
    {
        if (sourceKey == null || !lightStates.TryGetValue(sourceKey, out LightRuntimeState state))
        {
            return;
        }

        reusableCellBuffer.Clear();
        foreach (KeyValuePair<VoxelCellKey, SourceCellDisplay> pair in state.displayBrightness)
        {
            reusableCellBuffer.Add(pair.Key);
        }

        if (state.burstLight != null)
        {
            burstLightSources.Remove(state.burstLight);
        }

        lightStates.Remove(sourceKey);
        activeLightStateSet.Remove(state);
        activeLightStates.Remove(state);
        for (int i = 0; i < reusableCellBuffer.Count; i++)
        {
            RecomposeBrightnessCell(reusableCellBuffer[i]);
        }

        reusableCellBuffer.Clear();
        ClampRoundRobinLightIndex();
    }

    private void RecomposeBrightnessCell(VoxelCellKey key)
    {
        bool found = false;
        float maxBrightnessValue = 0f;
        foreach (KeyValuePair<object, LightRuntimeState> sourcePair in lightStates)
        {
            if (sourcePair.Value.displayBrightness.TryGetValue(key, out SourceCellDisplay display) &&
                (!found || display.brightness > maxBrightnessValue))
            {
                maxBrightnessValue = display.brightness;
                found = true;
            }
        }

        if (found && maxBrightnessValue > 0f)
        {
            if (!composedBrightness.TryGetValue(key, out float existing) ||
                !Mathf.Approximately(existing, maxBrightnessValue))
            {
                composedBrightness[key] = maxBrightnessValue;
                EnqueueDirtyBrightnessCell(key);
            }
            return;
        }

        if (composedBrightness.Remove(key))
        {
            EnqueueDirtyBrightnessCell(key);
        }
    }

    private void EnqueueDirtyBrightnessCell(VoxelCellKey key)
    {
        if (queuedDirtyBrightnessCells.Add(key))
        {
            dirtyBrightnessCells.Enqueue(key);
        }
    }

    private bool TryGetOffsetCell(VoxelCellKey key, Vector3Int offset, out VoxelCellKey offsetCell)
    {
        Vector3Int blockPosition = key.blockPosition;
        Vector3Int localPosition = key.localVoxelPosition + offset;
        if (!terrainManager.VoxelManager.NormalizeVoxelPosition(ref blockPosition, ref localPosition))
        {
            offsetCell = default;
            return false;
        }

        offsetCell = new VoxelCellKey(blockPosition, localPosition);
        return true;
    }

    private bool IsLightPropagationCell(VoxelCellKey key)
    {
        if (terrainManager == null || terrainManager.BlockManager == null || terrainManager.TerrainDataManager == null)
        {
            return false;
        }

        BlockManager.BlockInstanceData block = terrainManager.BlockManager.GetBlockAt(key.blockPosition);
        if (block != null && block.block != null)
        {
            return true;
        }

        return terrainManager.TerrainDataManager.IsBlockGenerationExcluded(key.blockPosition);
    }

    private bool IsLightPropagationCellCached(PropagationRun propagation, VoxelCellKey key)
    {
        if (propagation == null)
        {
            Debug.LogError("MiningLightManager: propagation run is null while resolving light propagation cell.", this);
            return false;
        }

        if (propagation.propagationCellCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool resolved = IsLightPropagationCell(key);
        propagation.propagationCellCache.Add(key, resolved);
        return resolved;
    }

    private bool IsSolidCellCached(PropagationRun propagation, VoxelCellKey key)
    {
        if (propagation == null)
        {
            Debug.LogError("MiningLightManager: propagation run is null while resolving solid cell.", this);
            return false;
        }

        if (propagation.solidCellCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool resolved = terrainManager.VoxelManager.IsVoxelCellSolid(key);
        propagation.solidCellCache.Add(key, resolved);
        return resolved;
    }

    private void HandleTerrainCellsChanged(TerrainChangeBatch change)
    {
        ClearActivePropagationCellStateCaches();
        if (change == null)
        {
            Debug.LogError("MiningLightManager: terrain change batch is null.", this);
            return;
        }

        using (CollectTerrainAffectedSourcesMarker.Auto())
        {
            CollectAffectedTerrainCells(change, reusableAffectedTerrainCells);
            if (reusableAffectedTerrainCells.Count > 0)
            {
                CollectTerrainRepairRequests(reusableAffectedTerrainCells);
            }

            reusableAffectedTerrainCells.Clear();
        }
    }

    private void CollectAffectedTerrainCells(TerrainChangeBatch change, HashSet<VoxelCellKey> buffer)
    {
        if (change == null)
        {
            Debug.LogError("MiningLightManager: terrain change batch is null while collecting affected cells.", this);
            return;
        }

        if (buffer == null)
        {
            Debug.LogError("MiningLightManager: affected terrain cell buffer is null.", this);
            return;
        }

        buffer.Clear();
        AddChangedCellsAndNeighbors(change.removedSolidCells, buffer);
        AddChangedCellsAndNeighbors(change.addedSolidCells, buffer);
    }

    private void AddChangedCellsAndNeighbors(List<VoxelCellKey> changedCells, HashSet<VoxelCellKey> buffer)
    {
        if (changedCells == null)
        {
            Debug.LogError("MiningLightManager: changed terrain cell list is null.", this);
            return;
        }

        if (buffer == null)
        {
            Debug.LogError("MiningLightManager: affected terrain cell buffer is null while adding changed cells.", this);
            return;
        }

        for (int i = 0; i < changedCells.Count; i++)
        {
            VoxelCellKey changedCell = changedCells[i];
            buffer.Add(changedCell);
            for (int j = 0; j < FullNeighborOffsets.Length; j++)
            {
                if (TryGetOffsetCell(changedCell, FullNeighborOffsets[j], out VoxelCellKey neighbor))
                {
                    buffer.Add(neighbor);
                }
            }
        }
    }

    private void CollectTerrainRepairRequests(HashSet<VoxelCellKey> affectedCells)
    {
        if (affectedCells == null)
        {
            Debug.LogError("MiningLightManager: affected terrain cell set is null.", this);
            return;
        }

        foreach (KeyValuePair<object, LightRuntimeState> pair in lightStates)
        {
            LightRuntimeState state = pair.Value;
            if (state == null)
            {
                Debug.LogError("MiningLightManager: lightStates contains a null runtime state while collecting terrain affected sources.", this);
                continue;
            }

            foreach (VoxelCellKey affectedCell in affectedCells)
            {
                if (!state.displayBrightness.TryGetValue(affectedCell, out SourceCellDisplay affectedDisplay))
                {
                    continue;
                }

                VoxelCellKey seedCell = affectedDisplay.hasPredecessor
                    ? affectedDisplay.predecessor
                    : affectedCell;
                if (!state.displayBrightness.TryGetValue(seedCell, out SourceCellDisplay seedDisplay))
                {
                    continue;
                }

                float seedBrightness = seedDisplay.brightness;
                if (state.burstLight != null &&
                    state.burstLight.baseBrightness.TryGetValue(seedCell, out float burstBaseBrightness))
                {
                    seedBrightness = burstBaseBrightness;
                }

                TerrainRepairRequest request = GetOrCreateTerrainRepairRequest(pair.Key);
                if (request == null)
                {
                    continue;
                }

                request.AddPruneRoot(affectedCell);
                request.AddSeed(new TerrainRepairSeed(
                    seedCell,
                    seedBrightness,
                    seedDisplay.distanceFromSourceCells,
                    seedDisplay.hasPredecessor,
                    seedDisplay.predecessor));
            }
        }
    }

    private TerrainRepairRequest GetOrCreateTerrainRepairRequest(object sourceKey)
    {
        if (sourceKey == null)
        {
            Debug.LogError("MiningLightManager: source key is null while creating terrain repair request.", this);
            return null;
        }

        if (!pendingTerrainRepairRequests.TryGetValue(sourceKey, out TerrainRepairRequest request))
        {
            request = new TerrainRepairRequest();
            pendingTerrainRepairRequests.Add(sourceKey, request);
        }

        return request;
    }

    private void ClearActivePropagationCellStateCaches()
    {
        for (int i = 0; i < activeLightStates.Count; i++)
        {
            LightRuntimeState state = activeLightStates[i];
            if (state == null)
            {
                continue;
            }

            for (int j = 0; j < state.activeRuns.Count; j++)
            {
                state.activeRuns[j]?.ClearCellStateCaches();
            }
        }
    }

    private void ClearLightState()
    {
        ClearBrightnessCaches();
        lightStates.Clear();
        burstLightSources.Clear();
        activeLightStates.Clear();
        activeLightStateSet.Clear();
        roundRobinLightIndex = 0;
        hasCalculated = false;
        lastLightSourceCells.Clear();
        lastLightSourceProfiles.Clear();
        reusableAffectedTerrainCells.Clear();
        pendingTerrainRepairRequests.Clear();
        reusableTraversalCells.Clear();
        reusableTraversalCellSet.Clear();
        reusableRemovedDisplayCells.Clear();
    }

    private void ClearBrightnessCaches()
    {
        reusableCellBuffer.Clear();
        foreach (KeyValuePair<VoxelCellKey, float> pair in composedBrightness)
        {
            reusableCellBuffer.Add(pair.Key);
        }

        for (int i = 0; i < reusableCellBuffer.Count; i++)
        {
            EnqueueDirtyBrightnessCell(reusableCellBuffer[i]);
        }

        reusableCellBuffer.Clear();
        composedBrightness.Clear();
    }

    private bool ValidateTemporaryLightRequest(MiningLightProfile profile, float lifetimeSeconds)
    {
        if (!ValidateConfiguration())
        {
            return false;
        }

        if (profile == null)
        {
            Debug.LogError("MiningLightManager: cannot spawn a temporary light with a null MiningLightProfile.", this);
            return false;
        }

        if (lifetimeSeconds <= 0f)
        {
            Debug.LogError($"MiningLightManager: temporary light lifetime must be greater than 0. lifetimeSeconds={lifetimeSeconds}", this);
            return false;
        }

        return true;
    }

    private bool ValidateTemporaryBurstLightRequest(
        MiningLightProfile profile,
        float lifetimeSeconds,
        AnimationCurve brightnessCurve,
        float updateIntervalSeconds)
    {
        if (!ValidateTemporaryLightRequest(profile, lifetimeSeconds))
        {
            return false;
        }

        if (brightnessCurve == null)
        {
            Debug.LogError("MiningLightManager: cannot spawn a temporary burst light with a null brightness curve.", this);
            return false;
        }

        if (updateIntervalSeconds <= 0f)
        {
            Debug.LogError(
                $"MiningLightManager: temporary burst light update interval must be greater than 0. updateIntervalSeconds={updateIntervalSeconds}",
                this);
            return false;
        }

        return true;
    }

    private bool ValidateConfiguration()
    {
        if (terrainManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager is not assigned.", this);
            return false;
        }

        if (terrainManager.VoxelManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager.VoxelManager is not assigned.", this);
            return false;
        }

        if (terrainManager.BlockManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager.BlockManager is not assigned.", this);
            return false;
        }

        if (terrainManager.TerrainDataManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager.TerrainDataManager is not assigned.", this);
            return false;
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawOnlyWhenSelected)
        {
            DrawLightGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (drawOnlyWhenSelected)
        {
            DrawLightGizmos();
        }
    }

    private void DrawLightGizmos()
    {
        if (!drawGizmos ||
            terrainManager == null ||
            terrainManager.VoxelManager == null ||
            activeLightStates.Count == 0)
        {
            return;
        }

        using (DrawGizmosMarker.Auto())
        {
            int drawn = 0;
            for (int i = 0; i < activeLightStates.Count && drawn < maxGizmoCells; i++)
            {
                LightRuntimeState state = activeLightStates[i];
                if (state == null)
                {
                    continue;
                }

                for (int j = 0; j < state.activeRuns.Count && drawn < maxGizmoCells; j++)
                {
                    PropagationRun propagation = state.activeRuns[j];
                    if (propagation == null || propagation.cellBrightness.Count == 0)
                    {
                        continue;
                    }

                    DrawSourceGizmos(propagation, ref drawn);
                    DrawBrightnessGizmos(propagation, ref drawn);
                }
            }
        }
    }

    private void DrawSourceGizmos(PropagationRun propagation, ref int drawn)
    {
        for (int i = 0; i < propagation.sourceCellOrder.Count && drawn < maxGizmoCells; i++)
        {
            VoxelCellKey sourceCell = propagation.sourceCellOrder[i];
            if (!propagation.cellBrightness.TryGetValue(sourceCell, out float brightness))
            {
                continue;
            }

            DrawGizmoCell(propagation, sourceCell, brightness);
            drawn++;
        }
    }

    private void DrawBrightnessGizmos(PropagationRun propagation, ref int drawn)
    {
        foreach (KeyValuePair<VoxelCellKey, float> pair in propagation.cellBrightness)
        {
            if (drawn >= maxGizmoCells)
            {
                break;
            }

            if (propagation.sourceCells.Contains(pair.Key) || pair.Value <= propagation.minSourceBrightness)
            {
                continue;
            }

            DrawGizmoCell(propagation, pair.Key, pair.Value);
            drawn++;
        }
    }

    private void DrawGizmoCell(PropagationRun propagation, VoxelCellKey key, float brightness)
    {
        Bounds bounds = terrainManager.VoxelManager.GetVoxelCellWorldBounds(key);
        Color color = GetGizmoColor(propagation, key, brightness);
        Gizmos.color = color;
        Gizmos.DrawCube(bounds.center, bounds.size * gizmoCellScale);
    }

    private Color GetGizmoColor(PropagationRun propagation, VoxelCellKey key, float brightness)
    {
        Color color;
        if (propagation.sourceCells.Contains(key))
        {
            color = propagation.sourceProfiles.TryGetValue(key, out MiningLightProfile profile) && profile != null
                ? profile.SourceGizmoColor
                : Color.white;
        }
        else
        {
            color = terrainManager.VoxelManager.IsVoxelCellSolid(key) ? solidCellGizmoColor : airCellGizmoColor;
        }

        float maxBrightness = Mathf.Max(propagation.minSourceBrightness, propagation.maxSourceBrightness);
        float alphaScale = Mathf.InverseLerp(propagation.minSourceBrightness, maxBrightness, brightness);
        color.a *= Mathf.Clamp01(alphaScale);
        return color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxCalculatedCells = Mathf.Max(1, maxCalculatedCells);
        maxPropagationCellsPerFrame = Mathf.Max(1, maxPropagationCellsPerFrame);
        maxGizmoCells = Mathf.Max(1, maxGizmoCells);
        gizmoCellScale = Mathf.Clamp(gizmoCellScale, 0.05f, 1f);
        MarkLightSourcesDirty();
    }
#endif
}
