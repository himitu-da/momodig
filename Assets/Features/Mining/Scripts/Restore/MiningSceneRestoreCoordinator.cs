using System.Collections.Generic;
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
    private static readonly ProfilerMarker ChunkGeneratedMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.ChunkGenerated");
    private static readonly ProfilerMarker ChunkRestoredMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.ChunkRestored");
    private static readonly ProfilerMarker InitialChunkRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.InitialChunkRestore");
    private static readonly ProfilerMarker FluidRestorePauseMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.FluidRestorePause");
    private static readonly ProfilerMarker TorchRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.TorchRestore");
    private static readonly ProfilerMarker DroppedItemRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.DroppedItemRestore");
    private static readonly ProfilerMarker PlayerRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.PlayerRestore");
    private static readonly ProfilerMarker PlayerChunkRestoreMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.PlayerChunkRestore");
    private static readonly ProfilerMarker PlayerGameplayUnlockMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.PlayerGameplayUnlock");
    private static readonly ProfilerMarker PostRestoreRecalculationMarker =
        new ProfilerMarker("MiningSceneRestoreCoordinator.PostRestoreRecalculation");
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
    [SerializeField] private MiningTerrainBrightnessApplier terrainBrightnessApplier;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private MinecartManager minecartManager;
    [SerializeField] private FairyCarrierManager fairyCarrierManager;
    [SerializeField] private CameraFollowController cameraFollowController;

    [Header("Diagnostics")]
    [SerializeField] private bool logPhaseTransitions = true;

    public MiningRestorePhase CurrentPhase { get; private set; } = MiningRestorePhase.Validate;
    public bool HasValidContext { get; private set; }
    public bool HasValidationErrors { get; private set; }
    public bool HasTerrainBaseline { get; private set; }
    public bool IsCompleted { get; private set; }
    public MiningRestoreContext Context { get; private set; }
    public bool HasPlayerChunkPosition { get; private set; }
    public Vector3Int PlayerChunkPosition { get; private set; }
    public bool IsPlayerChunkGenerated => HasPlayerChunkPosition && generatedChunks.Contains(PlayerChunkPosition);
    public bool IsPlayerChunkRestored => HasPlayerChunkPosition && restoredChunks.Contains(PlayerChunkPosition);
    public int InitialChunkCount => initialChunks.Count;
    public int RestoredInitialChunkCount => restoredInitialChunks.Count;
    public int PlayerGameplayUnlockChunkCount => playerGameplayUnlockChunks.Count;
    public int RestoredPlayerGameplayUnlockChunkCount => restoredPlayerGameplayUnlockChunks.Count;
    public bool IsPlayerGameplayUnlocked { get; private set; }
    public bool IsInitialChunkRestoreComplete { get; private set; }

    private bool hasRunValidatePhase;
    private bool hasRunTerrainBaselinePhase;
    private bool hasRunTerrainInitializationPhase;
    private bool hasCompletedInitialChunkRestore;
    private bool hasPreparedInitialGameplayLock;
    private bool hasRestoredPlayerChunkDependents;
    private bool hasReleasedPlayerGameplayForRestore;
    private readonly HashSet<Vector3Int> generatedChunks = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> restoredChunks = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> initialChunks = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> restoredInitialChunks = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> playerGameplayUnlockChunks = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> restoredPlayerGameplayUnlockChunks = new HashSet<Vector3Int>();

    private void Awake()
    {
        if (EnsureContext())
        {
            PrepareInitialGameplayLock();
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
            GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
            if (!RunValidatePhase(persistenceManager))
            {
                return;
            }

            if (!RunTerrainBaselinePhase(persistenceManager))
            {
                return;
            }

            RunTerrainInitializationPhase();
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

    public void ResetChunkRestoreTracking()
    {
        using (InitialChunkRestoreMarker.Auto())
        {
            generatedChunks.Clear();
            restoredChunks.Clear();
            initialChunks.Clear();
            restoredInitialChunks.Clear();
            playerGameplayUnlockChunks.Clear();
            restoredPlayerGameplayUnlockChunks.Clear();
            HasPlayerChunkPosition = false;
            PlayerChunkPosition = default;
            IsPlayerGameplayUnlocked = false;
            IsInitialChunkRestoreComplete = false;
            hasCompletedInitialChunkRestore = false;
            hasPreparedInitialGameplayLock = false;
            hasRestoredPlayerChunkDependents = false;
            hasReleasedPlayerGameplayForRestore = false;
            IsCompleted = false;
        }
    }

    public void BeginInitialChunkRestore(Vector3Int playerChunkPosition, IReadOnlyList<Vector3Int> chunkPositions)
    {
        using (InitialChunkRestoreMarker.Auto())
        {
            ResetChunkRestoreTracking();
            PrepareInitialGameplayLock();
            RunTerrainInitializationPhase();
            SetCurrentPhase(MiningRestorePhase.ChunkRestore);
            HasPlayerChunkPosition = true;
            PlayerChunkPosition = playerChunkPosition;

            if (chunkPositions == null)
            {
                Debug.LogError("MiningSceneRestoreCoordinator: initial chunk list is not configured.", this);
                return;
            }

            for (int i = 0; i < chunkPositions.Count; i++)
            {
                initialChunks.Add(chunkPositions[i]);
            }

            BuildPlayerGameplayUnlockChunks();
            PauseFluidSimulationForRestore();
            torchPlacementManager.PreparePersistedTorchLoading();
            droppedItemManager.PreparePersistedItemLoading();
            UpdatePlayerGameplayUnlockCompletion();
            UpdateInitialChunkRestoreCompletion();
        }
    }

    public void NotifyChunkGenerated(Vector3Int chunkPosition)
    {
        using (ChunkGeneratedMarker.Auto())
        {
            generatedChunks.Add(chunkPosition);
        }
    }

    public void NotifyChunkRestored(Vector3Int chunkPosition)
    {
        using (ChunkRestoredMarker.Auto())
        {
            restoredChunks.Add(chunkPosition);
            RestoreTorchesInChunk(chunkPosition);
            RestoreDroppedItemsInChunk(chunkPosition);
            if (initialChunks.Contains(chunkPosition))
            {
                restoredInitialChunks.Add(chunkPosition);
                if (playerGameplayUnlockChunks.Contains(chunkPosition))
                {
                    restoredPlayerGameplayUnlockChunks.Add(chunkPosition);
                }

                RestorePlayerChunkDependentsIfReady(chunkPosition);
                UpdatePlayerGameplayUnlockCompletion();
                UpdateInitialChunkRestoreCompletion();
            }
        }
    }

    private void RestoreTorchesInChunk(Vector3Int chunkPosition)
    {
        using (TorchRestoreMarker.Auto())
        {
            torchPlacementManager.LoadTorchesInChunk(chunkPosition);
        }
    }

    private void RestoreDroppedItemsInChunk(Vector3Int chunkPosition)
    {
        using (DroppedItemRestoreMarker.Auto())
        {
            droppedItemManager.LoadItemsInChunk(chunkPosition);
        }
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

    private void RunTerrainInitializationPhase()
    {
        if (hasRunTerrainInitializationPhase)
        {
            return;
        }

        hasRunTerrainInitializationPhase = true;
        RunPhase(MiningRestorePhase.TerrainInitialization, TerrainInitializationMarker);
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
        ValidateRequiredReference(terrainBrightnessApplier, nameof(terrainBrightnessApplier), ref isValid);
        ValidateRequiredReference(playerController, nameof(playerController), ref isValid);
        ValidateRequiredReference(minecartManager, nameof(minecartManager), ref isValid);
        ValidateRequiredReference(fairyCarrierManager, nameof(fairyCarrierManager), ref isValid);
        ValidateRequiredReference(cameraFollowController, nameof(cameraFollowController), ref isValid);

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
            miningLightManager,
            terrainBrightnessApplier,
            playerController,
            minecartManager,
            fairyCarrierManager,
            cameraFollowController);
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

    private void UpdateInitialChunkRestoreCompletion()
    {
        if (IsInitialChunkRestoreComplete)
        {
            return;
        }

        if (initialChunks.Count == 0 || restoredInitialChunks.Count < initialChunks.Count)
        {
            return;
        }

        IsInitialChunkRestoreComplete = true;
        CompleteInitialChunkRestore();
    }

    private void PrepareInitialGameplayLock()
    {
        if (hasPreparedInitialGameplayLock)
        {
            return;
        }

        hasPreparedInitialGameplayLock = true;
        playerController.SetControlLocked(true);
        playerController.SetItemPickupLocked(true);
        minecartManager.PauseMovementForRestore();
        fairyCarrierManager.PauseForRestore();
    }

    private void BuildPlayerGameplayUnlockChunks()
    {
        playerGameplayUnlockChunks.Clear();
        restoredPlayerGameplayUnlockChunks.Clear();

        if (!HasPlayerChunkPosition)
        {
            Debug.LogError("MiningSceneRestoreCoordinator: player chunk position is not available. Player gameplay unlock chunks cannot be built.", this);
            return;
        }

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int chunkPosition = new Vector3Int(
                    PlayerChunkPosition.x + x,
                    PlayerChunkPosition.y + y,
                    PlayerChunkPosition.z);
                playerGameplayUnlockChunks.Add(chunkPosition);
                if (restoredChunks.Contains(chunkPosition))
                {
                    restoredPlayerGameplayUnlockChunks.Add(chunkPosition);
                }

                if (!initialChunks.Contains(chunkPosition))
                {
                    Debug.LogError(
                        $"MiningSceneRestoreCoordinator: initial chunk list does not include required player gameplay unlock chunk {chunkPosition}.",
                        this);
                }
            }
        }
    }

    private void UpdatePlayerGameplayUnlockCompletion()
    {
        if (IsPlayerGameplayUnlocked)
        {
            return;
        }

        if (playerGameplayUnlockChunks.Count == 0 ||
            restoredPlayerGameplayUnlockChunks.Count < playerGameplayUnlockChunks.Count)
        {
            return;
        }

        IsPlayerGameplayUnlocked = true;
        ReleasePlayerGameplayForRestore();
    }

    private void ReleasePlayerGameplayForRestore()
    {
        if (hasReleasedPlayerGameplayForRestore)
        {
            return;
        }

        using (PlayerGameplayUnlockMarker.Auto())
        {
            hasReleasedPlayerGameplayForRestore = true;
            playerController.SetItemPickupLocked(false);
            playerController.SetControlLocked(false);
        }
    }

    private void RestorePlayerChunkDependentsIfReady(Vector3Int restoredChunkPosition)
    {
        if (hasRestoredPlayerChunkDependents ||
            !HasPlayerChunkPosition ||
            restoredChunkPosition != PlayerChunkPosition ||
            !restoredChunks.Contains(restoredChunkPosition))
        {
            return;
        }

        using (PlayerChunkRestoreMarker.Auto())
        {
            hasRestoredPlayerChunkDependents = true;
            playerController.ResetMotion();
            minecartManager.ResetPathToPlayer();
            fairyCarrierManager.ResetHomePositionAfterRestore();
            if (!cameraFollowController.SnapToFollowTargetAndEnable())
            {
                Debug.LogError("MiningSceneRestoreCoordinator: failed to snap camera after player chunk restore.", this);
            }
        }
    }

    private void CompleteInitialChunkRestore()
    {
        if (hasCompletedInitialChunkRestore)
        {
            return;
        }

        hasCompletedInitialChunkRestore = true;
        RestorePlayerChunkDependentsIfReady(PlayerChunkPosition);
        RunPhase(MiningRestorePhase.PlayerRestore, PlayerRestoreMarker);
        RunPostRestorePhase();
        UpdatePlayerGameplayUnlockCompletion();
        minecartManager.ResumeMovementAfterRestore();
        fairyCarrierManager.ResumeAfterRestore();
        fluidManager.ResumeSimulationAfterRestore();
        RunPhase(MiningRestorePhase.Completed, CompletedMarker);
        IsCompleted = true;
    }

    private void RunPostRestorePhase()
    {
        using (PostRestoreMarker.Auto())
        {
            SetCurrentPhase(MiningRestorePhase.PostRestore);
            using (PostRestoreRecalculationMarker.Auto())
            {
                miningLightManager.MarkLightSourcesDirty();
                terrainBrightnessApplier.QueueAllActiveBlocksForPostRestoreRefresh();
                droppedItemManager.RefreshActiveItemsAfterRestore();
                fluidManager.QueuePostRestoreActiveCells();
                if (!cameraFollowController.SnapToFollowTargetAndEnable())
                {
                    Debug.LogError("MiningSceneRestoreCoordinator: failed to snap camera during post-restore recalculation.", this);
                }
            }
        }
    }

    private void PauseFluidSimulationForRestore()
    {
        using (FluidRestorePauseMarker.Auto())
        {
            fluidManager.PauseSimulationForRestore();
        }
    }
}
