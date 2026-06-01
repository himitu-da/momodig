using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiningSceneRestoreCoordinator : MonoBehaviour
{
    private static readonly ProfilerMarker StartupMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.Startup");
    private static readonly ProfilerMarker ValidateMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.Validate");
    private static readonly ProfilerMarker TerrainBaselineMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.TerrainBaseline");
    private static readonly ProfilerMarker TerrainInitializationMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.TerrainInitialization");
    private static readonly ProfilerMarker ChunkRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.ChunkRestore");
    private static readonly ProfilerMarker PlayerRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.PlayerRestore");
    private static readonly ProfilerMarker PostRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.PostRestore");
    private static readonly ProfilerMarker CompletedMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.Completed");

    [Header("Required Scene References")]
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private DroppedItemManager droppedItemManager;
    [SerializeField] private TorchPlacementManager torchPlacementManager;
    [SerializeField] private FluidManager fluidManager;
    [SerializeField] private MiningLightManager miningLightManager;

    [Header("Diagnostics")]
    [SerializeField] private bool logPhaseTransitions = true;

    public MiningRestorePhase CurrentPhase { get; private set; } = MiningRestorePhase.Validate;
    public bool HasValidContext { get; private set; }
    public bool HasValidationErrors { get; private set; }
    public bool HasTerrainBaseline { get; private set; }
    public bool IsCompleted { get; private set; }
    public MiningRestoreContext Context { get; private set; }

    private bool hasRunValidatePhase;
    private bool hasRunTerrainBaselinePhase;

    private void Awake()
    {
        EnsureContext();
    }

    private void Start()
    {
        if (!HasValidContext)
        {
            return;
        }

        using (StartupMarker.Auto())
        {
            GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
            if (!RunValidatePhase(persistenceManager))
            {
                return;
            }

            if (!RunTerrainBaselinePhase(persistenceManager))
            {
                return;
            }

            RunPhase(MiningRestorePhase.TerrainInitialization, TerrainInitializationMarker);
            RunPhase(MiningRestorePhase.ChunkRestore, ChunkRestoreMarker);
            RunPhase(MiningRestorePhase.PlayerRestore, PlayerRestoreMarker);
            RunPhase(MiningRestorePhase.PostRestore, PostRestoreMarker);
            RunPhase(MiningRestorePhase.Completed, CompletedMarker);
            IsCompleted = true;
        }
    }

    public bool EnsureTerrainBaselineReadyForTerrainInitialization(GameDataPersistenceManager persistenceManager)
    {
        if (!EnsureContext())
        {
            return false;
        }

        if (!RunValidatePhase(persistenceManager))
        {
            return false;
        }

        return RunTerrainBaselinePhase(persistenceManager);
    }

    private bool RunValidatePhase(GameDataPersistenceManager persistenceManager)
    {
        if (hasRunValidatePhase)
        {
            return !HasValidationErrors;
        }

        using (ValidateMarker.Auto())
        {
            SetCurrentPhase(MiningRestorePhase.Validate);

            bool isValid = MiningRestoreDataValidator.Validate(Context, persistenceManager, this);
            hasRunValidatePhase = true;
            HasValidationErrors = !isValid;
            if (!isValid)
            {
                Debug.LogError("MiningSceneRestoreCoordinator: restore data validation failed. Restore coordinator is stopped.", this);
                enabled = false;
                return false;
            }

            return true;
        }
    }

    private bool RunTerrainBaselinePhase(GameDataPersistenceManager persistenceManager)
    {
        if (hasRunTerrainBaselinePhase)
        {
            return HasTerrainBaseline;
        }

        using (TerrainBaselineMarker.Auto())
        {
            SetCurrentPhase(MiningRestorePhase.TerrainBaseline);

            if (persistenceManager == null)
            {
                Debug.LogError("MiningSceneRestoreCoordinator: GameDataPersistenceManager is not initialized. Terrain baseline cannot be restored.", this);
                enabled = false;
                return false;
            }

            TerrainSettings settings = terrainManager.Settings;
            if (settings == null)
            {
                Debug.LogError("MiningSceneRestoreCoordinator: TerrainSettings is not configured. Terrain baseline cannot be restored.", this);
                enabled = false;
                return false;
            }

            if (!persistenceManager.hasInitializedSeed)
            {
                persistenceManager.terrainSeed = settings.useRandomSeed
                    ? Random.Range(int.MinValue, int.MaxValue)
                    : settings.seed;
                persistenceManager.hasInitializedSeed = true;
            }

            terrainManager.ApplyTerrainBaselineSeed(persistenceManager.terrainSeed);
            HasTerrainBaseline = true;
            hasRunTerrainBaselinePhase = true;
            return true;
        }
    }

    private bool EnsureContext()
    {
        if (HasValidContext)
        {
            return true;
        }

        using (ValidateMarker.Auto())
        {
            if (!TryCreateContext(out MiningRestoreContext context))
            {
                enabled = false;
                return false;
            }

            Context = context;
            HasValidContext = true;
            return true;
        }
    }

    private bool TryCreateContext(out MiningRestoreContext context)
    {
        context = default;
        bool isValid = true;

        ValidateRequiredReference(terrainManager, nameof(terrainManager), ref isValid);
        ValidateRequiredReference(chunkManager, nameof(chunkManager), ref isValid);
        ValidateRequiredReference(droppedItemManager, nameof(droppedItemManager), ref isValid);
        ValidateRequiredReference(torchPlacementManager, nameof(torchPlacementManager), ref isValid);
        ValidateRequiredReference(fluidManager, nameof(fluidManager), ref isValid);
        ValidateRequiredReference(miningLightManager, nameof(miningLightManager), ref isValid);

        if (!isValid)
        {
            Debug.LogError("MiningSceneRestoreCoordinator: missing required scene references. Restore coordinator is stopped.", this);
            return false;
        }

        context = new MiningRestoreContext(
            terrainManager,
            chunkManager,
            droppedItemManager,
            torchPlacementManager,
            fluidManager,
            miningLightManager);
        return true;
    }

    private void ValidateRequiredReference(Object reference, string fieldName, ref bool isValid)
    {
        if (reference != null)
        {
            return;
        }

        isValid = false;
        Debug.LogError($"MiningSceneRestoreCoordinator: {fieldName} is not assigned.", this);
    }

    private void RunPhase(MiningRestorePhase phase, ProfilerMarker marker)
    {
        using (marker.Auto())
        {
            SetCurrentPhase(phase);
        }
    }

    private void SetCurrentPhase(MiningRestorePhase phase)
    {
        CurrentPhase = phase;
        if (logPhaseTransitions)
        {
            Debug.Log($"MiningSceneRestoreCoordinator: {phase}", this);
        }
    }
}
