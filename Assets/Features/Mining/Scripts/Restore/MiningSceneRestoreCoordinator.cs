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
    public bool IsCompleted { get; private set; }
    public MiningRestoreContext Context { get; private set; }

    private void Awake()
    {
        using (ValidateMarker.Auto())
        {
            if (!TryCreateContext(out MiningRestoreContext context))
            {
                enabled = false;
                return;
            }

            Context = context;
            HasValidContext = true;
        }
    }

    private void Start()
    {
        if (!HasValidContext)
        {
            return;
        }

        using (StartupMarker.Auto())
        {
            if (!RunValidatePhase())
            {
                return;
            }

            RunPhase(MiningRestorePhase.TerrainBaseline, TerrainBaselineMarker);
            RunPhase(MiningRestorePhase.TerrainInitialization, TerrainInitializationMarker);
            RunPhase(MiningRestorePhase.ChunkRestore, ChunkRestoreMarker);
            RunPhase(MiningRestorePhase.PlayerRestore, PlayerRestoreMarker);
            RunPhase(MiningRestorePhase.PostRestore, PostRestoreMarker);
            RunPhase(MiningRestorePhase.Completed, CompletedMarker);
            IsCompleted = true;
        }
    }

    private bool RunValidatePhase()
    {
        using (ValidateMarker.Auto())
        {
            SetCurrentPhase(MiningRestorePhase.Validate);

            GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
            bool isValid = MiningRestoreDataValidator.Validate(Context, persistenceManager, this);
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
