using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class MiningTerrainBrightnessApplier : MonoBehaviour
{
    private static readonly ProfilerMarker ApplyBrightnessMarker =
        new ProfilerMarker("MiningTerrainBrightnessApplier.ApplyBrightness");

    [Header("Required References")]
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private MiningLightManager lightManager;

    [Header("Frame Budget")]
    [SerializeField, Min(1)] private int maxBrightnessCellsPerFrame = 512;

    private readonly List<VoxelCellKey> dirtyBrightnessCells = new List<VoxelCellKey>(512);
    private readonly Queue<Block> queuedBlockRefreshes = new Queue<Block>();
    private readonly HashSet<Block> queuedBlockRefreshSet = new HashSet<Block>();

    private BlockRefreshWork activeBlockRefresh;

    private sealed class BlockRefreshWork
    {
        public readonly Block block;
        public readonly List<Vector3Int> localCells = new List<Vector3Int>(128);
        public int nextLocalCellIndex;

        public BlockRefreshWork(Block block)
        {
            this.block = block;
            block.CollectBrightnessLocalCells(localCells);
        }
    }

    private void OnEnable()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        terrainManager.VoxelManager.TerrainCellsChanged += HandleTerrainCellsChanged;
        QueueAllActiveBlocksForRefresh();
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

        using (ApplyBrightnessMarker.Auto())
        {
            int remainingCellBudget = Mathf.Max(1, maxBrightnessCellsPerFrame);
            int appliedDirtyCells = ApplyDirtyBrightnessCells(remainingCellBudget);
            remainingCellBudget -= appliedDirtyCells;
            if (remainingCellBudget > 0)
            {
                ApplyQueuedBlockRefreshes(ref remainingCellBudget);
            }
        }
    }

    private void ApplyQueuedBlockRefreshes(ref int remainingCellBudget)
    {
        while (remainingCellBudget > 0)
        {
            if (activeBlockRefresh == null)
            {
                if (queuedBlockRefreshes.Count == 0)
                {
                    return;
                }

                Block block = queuedBlockRefreshes.Dequeue();
                queuedBlockRefreshSet.Remove(block);
                if (block == null || !block.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeBlockRefresh = new BlockRefreshWork(block);
            }

            while (remainingCellBudget > 0 &&
                   activeBlockRefresh.nextLocalCellIndex < activeBlockRefresh.localCells.Count)
            {
                Vector3Int localCell = activeBlockRefresh.localCells[activeBlockRefresh.nextLocalCellIndex];
                VoxelCellKey key = new VoxelCellKey(activeBlockRefresh.block.BlockPosition, localCell);
                if (lightManager.TryGetBrightness(key, out float resolvedBrightness))
                {
                    activeBlockRefresh.block.ApplyBrightness(key, resolvedBrightness);
                }

                activeBlockRefresh.nextLocalCellIndex++;
                remainingCellBudget--;
            }

            if (activeBlockRefresh.nextLocalCellIndex >= activeBlockRefresh.localCells.Count)
            {
                activeBlockRefresh = null;
            }
        }
    }

    private int ApplyDirtyBrightnessCells(int maxCells)
    {
        int drained = lightManager.DrainDirtyBrightnessCells(dirtyBrightnessCells, maxCells);
        for (int i = 0; i < drained; i++)
        {
            VoxelCellKey key = dirtyBrightnessCells[i];
            BlockManager.BlockInstanceData blockInstance = terrainManager.BlockManager.GetBlockAt(key.blockPosition);
            if (blockInstance == null || blockInstance.block == null || !blockInstance.block.gameObject.activeInHierarchy)
            {
                continue;
            }

            float brightness = lightManager.TryGetBrightness(key, out float resolvedBrightness)
                ? resolvedBrightness
                : 0f;

            blockInstance.block.ApplyBrightness(key, brightness);
        }

        return drained;
    }

    private void HandleTerrainCellsChanged(TerrainChangeBatch change)
    {
        if (change == null)
        {
            return;
        }

        QueueChangedBlocks(change.removedSolidCells);
        QueueChangedBlocks(change.addedSolidCells);
    }

    public void QueueAllActiveBlocksForPostRestoreRefresh()
    {
        if (!ValidateConfiguration())
        {
            Debug.LogError("MiningTerrainBrightnessApplier: cannot queue post-restore brightness refresh because configuration is invalid.", this);
            return;
        }

        QueueAllActiveBlocksForRefresh();
    }

    public void QueueChunkBlocksForRuntimeRefresh(Vector3Int chunkPosition)
    {
        if (!ValidateConfiguration())
        {
            Debug.LogError("MiningTerrainBrightnessApplier: cannot queue chunk runtime brightness refresh because configuration is invalid.", this);
            return;
        }

        if (terrainManager.ChunkManager == null)
        {
            Debug.LogError("MiningTerrainBrightnessApplier: TerrainManager.ChunkManager is not assigned.", this);
            return;
        }

        List<Vector3Int> blockPositions = terrainManager.ChunkManager.GetBlockPositionsInChunk(chunkPosition);
        for (int i = 0; i < blockPositions.Count; i++)
        {
            BlockManager.BlockInstanceData blockInstance = terrainManager.BlockManager.GetBlockAt(blockPositions[i]);
            if (blockInstance != null && blockInstance.block != null && blockInstance.block.gameObject.activeInHierarchy)
            {
                QueueBlockRefresh(blockInstance.block);
            }
        }
    }

    public void QueueChunkBlocksForPostRestoreRefresh(Vector3Int chunkPosition)
    {
        QueueChunkBlocksForRuntimeRefresh(chunkPosition);
    }

    private void QueueChangedBlocks(List<VoxelCellKey> changedCells)
    {
        for (int i = 0; i < changedCells.Count; i++)
        {
            BlockManager.BlockInstanceData blockInstance = terrainManager.BlockManager.GetBlockAt(changedCells[i].blockPosition);
            if (blockInstance != null && blockInstance.block != null)
            {
                QueueBlockRefresh(blockInstance.block);
            }
        }
    }

    private void QueueAllActiveBlocksForRefresh()
    {
        List<BlockManager.BlockInstanceData> blocks = terrainManager.BlockManager.GetAllBlocks();
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] != null && blocks[i].block != null && blocks[i].block.gameObject.activeInHierarchy)
            {
                QueueBlockRefresh(blocks[i].block);
            }
        }
    }

    private void QueueBlockRefresh(Block block)
    {
        if (block != null && queuedBlockRefreshSet.Add(block))
        {
            queuedBlockRefreshes.Enqueue(block);
        }
    }

    private bool ValidateConfiguration()
    {
        if (terrainManager == null)
        {
            Debug.LogError("MiningTerrainBrightnessApplier: TerrainManager is not assigned.", this);
            return false;
        }

        if (terrainManager.VoxelManager == null)
        {
            Debug.LogError("MiningTerrainBrightnessApplier: TerrainManager.VoxelManager is not assigned.", this);
            return false;
        }

        if (terrainManager.BlockManager == null)
        {
            Debug.LogError("MiningTerrainBrightnessApplier: TerrainManager.BlockManager is not assigned.", this);
            return false;
        }

        if (lightManager == null)
        {
            Debug.LogError("MiningTerrainBrightnessApplier: MiningLightManager is not assigned.", this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxBrightnessCellsPerFrame = Mathf.Max(1, maxBrightnessCellsPerFrame);
    }
#endif
}
