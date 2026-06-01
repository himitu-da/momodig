public readonly struct MiningRestoreContext
{
    public MiningRestoreContext(
        TerrainManager terrainManager,
        ChunkManager chunkManager,
        DroppedItemManager droppedItemManager,
        TorchPlacementManager torchPlacementManager,
        FluidManager fluidManager,
        MiningLightManager miningLightManager)
    {
        TerrainManager = terrainManager;
        ChunkManager = chunkManager;
        DroppedItemManager = droppedItemManager;
        TorchPlacementManager = torchPlacementManager;
        FluidManager = fluidManager;
        MiningLightManager = miningLightManager;
    }

    public TerrainManager TerrainManager { get; }
    public ChunkManager ChunkManager { get; }
    public DroppedItemManager DroppedItemManager { get; }
    public TorchPlacementManager TorchPlacementManager { get; }
    public FluidManager FluidManager { get; }
    public MiningLightManager MiningLightManager { get; }
}
