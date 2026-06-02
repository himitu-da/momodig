using System.Collections.Generic;
using UnityEngine;

public partial class FluidManager
{
    private bool CanFluidMoveIntoCell(Vector3Int cellPosition, FluidDefinition definition)
    {
        using var canMoveScope = CanFluidMoveIntoCellMarker.Auto();
        if (definition == null)
        {
            return false;
        }

        if (IsTerrainSolidAtCell(cellPosition))
        {
            return false;
        }

        if (useDynamicObstacleLayers && IsDynamicObstacleAtCell(cellPosition))
        {
            return false;
        }

        if (cells.TryGetValue(cellPosition, out FluidCellState existing))
        {
            if (existing.Definition != null && existing.Definition != definition && existing.Liters > MinLitersEpsilon)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsDynamicObstacleAtCell(Vector3Int cellPosition)
    {
        using var dynamicObstacleScope = IsDynamicObstacleAtCellMarker.Auto();
        if (dynamicObstacleLayers.value == 0)
        {
            return false;
        }

        if (dynamicObstacleCache.TryGetValue(cellPosition, out bool cached))
        {
            return cached;
        }

        Vector3 center = InternalCellToWorldCenter(cellPosition);
        Vector3 halfExtents = Vector3.one * (InternalVoxelSize * 0.45f);
        bool blocked = Physics.CheckBox(center, halfExtents, Quaternion.identity, dynamicObstacleLayers, QueryTriggerInteraction.Ignore);
        dynamicObstacleCache[cellPosition] = blocked;
        return blocked;
    }

    private bool IsTerrainSolidAtCell(Vector3Int cellPosition)
    {
        using var terrainSolidScope = IsTerrainSolidAtCellMarker.Auto();
        if (terrainSolidCache.TryGetValue(cellPosition, out bool cached))
        {
            return cached;
        }

        bool result = IsTerrainSolidAtCellUncached(cellPosition);
        terrainSolidCache[cellPosition] = result;
        return result;
    }

    private bool IsTerrainSolidAtCellUncached(Vector3Int cellPosition)
    {
        using var terrainSolidUncachedScope = IsTerrainSolidAtCellUncachedMarker.Auto();
        if (terrainManager == null)
        {
            Debug.LogError("FluidManager: TerrainManager is not assigned.", this);
            return false;
        }

        TerrainSettings settings = terrainManager.Settings;
        if (settings == null || settings.voxelsPerBlock <= 0 || settings.blockSize <= 0f)
        {
            return false;
        }

        Vector3 worldCenter = InternalCellToWorldCenter(cellPosition);
        if (IsOutsideGenerationSlice(worldCenter, settings))
        {
            return true;
        }

        float voxelSize = settings.blockSize / settings.voxelsPerBlock;

        Vector3 terrainRelative = worldCenter - new Vector3(settings.center.x, settings.center.y, settings.center.z);

        int blockX = Mathf.RoundToInt(terrainRelative.x / settings.blockSize);
        int blockY = Mathf.RoundToInt(terrainRelative.y / settings.blockSize);
        int blockZ = Mathf.RoundToInt(terrainRelative.z / settings.blockSize);

        Vector3 blockWorldCenter = new Vector3(
            settings.center.x + blockX * settings.blockSize,
            settings.center.y + blockY * settings.blockSize,
            settings.center.z + blockZ * settings.blockSize);

        Vector3 blockLocal = worldCenter - blockWorldCenter;
        int localX = Mathf.Clamp(Mathf.FloorToInt(blockLocal.x / voxelSize + settings.voxelsPerBlock / 2f), 0, settings.voxelsPerBlock - 1);
        int localY = Mathf.Clamp(Mathf.FloorToInt(blockLocal.y / voxelSize + settings.voxelsPerBlock / 2f), 0, settings.voxelsPerBlock - 1);
        int localZ = Mathf.Clamp(Mathf.FloorToInt(blockLocal.z / voxelSize + settings.voxelsPerBlock / 2f), 0, settings.voxelsPerBlock - 1);

        Vector3Int blockPos = new Vector3Int(blockX, blockY, blockZ);
        Vector3Int localVoxelPos = new Vector3Int(localX, localY, localZ);

        if (terrainManager.ChunkManager != null && terrainManager.ChunkManager.IsBlockGenerationSkipped(blockPos))
        {
            return false;
        }

        GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager.destroyedBlockPositions.Contains(blockPos))
        {
            return false;
        }

        if (persistenceManager.partiallyDestroyedBlocks.TryGetValue(blockPos, out HashSet<Vector3Int> destroyedVoxels) &&
            destroyedVoxels.Contains(localVoxelPos))
        {
            return false;
        }

        if (terrainManager.TerrainDataManager == null || terrainManager.TerrainDataManager.GetBiomeForHeight(blockPos.y) == null)
        {
            return false;
        }

        if (terrainManager.BlockGenerator == null)
        {
            return false;
        }

        if (terrainManager.BlockGenerator.GetBlockDataForPosition(blockPos) == null)
        {
            return false;
        }

        return terrainManager.BlockGenerator.IsVoxelSolid(
            settings.generationType,
            settings.voxelsPerBlock,
            settings.blockSize,
            blockPos,
            localVoxelPos);
    }

    private bool IsOutsideGenerationSlice(Vector3 worldPosition, TerrainSettings settings)
    {
        return Mathf.Abs(worldPosition.z - settings.center.z) > generationSliceHalfThickness;
    }
}
