public readonly struct MiningRestoreContext
{
    public MiningRestoreContext(
        TerrainManager terrainManager,
        ChunkManager chunkManager,
        DroppedItemManager droppedItemManager,
        TorchPlacementManager torchPlacementManager,
        FluidManager fluidManager,
        MiningLightManager miningLightManager,
        MiningTerrainBrightnessApplier terrainBrightnessApplier,
        PlayerController playerController,
        MinecartManager minecartManager,
        FairyCarrierManager fairyCarrierManager,
        CameraFollowController cameraFollowController)
    {
        TerrainManager = terrainManager;
        ChunkManager = chunkManager;
        DroppedItemManager = droppedItemManager;
        TorchPlacementManager = torchPlacementManager;
        FluidManager = fluidManager;
        MiningLightManager = miningLightManager;
        TerrainBrightnessApplier = terrainBrightnessApplier;
        PlayerController = playerController;
        MinecartManager = minecartManager;
        FairyCarrierManager = fairyCarrierManager;
        CameraFollowController = cameraFollowController;
    }

    public TerrainManager TerrainManager { get; }
    public ChunkManager ChunkManager { get; }
    public DroppedItemManager DroppedItemManager { get; }
    public TorchPlacementManager TorchPlacementManager { get; }
    public FluidManager FluidManager { get; }
    public MiningLightManager MiningLightManager { get; }
    public MiningTerrainBrightnessApplier TerrainBrightnessApplier { get; }
    public PlayerController PlayerController { get; }
    public MinecartManager MinecartManager { get; }
    public FairyCarrierManager FairyCarrierManager { get; }
    public CameraFollowController CameraFollowController { get; }
}
