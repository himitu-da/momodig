using System.Collections.Generic;
using UnityEngine;

public static class MiningRestoreDataValidator
{
    public static bool Validate(
        MiningRestoreContext context,
        GameDataPersistenceManager persistence,
        Object logContext)
    {
        bool isValid = true;

        if (persistence == null)
        {
            Debug.LogError("MiningRestoreDataValidator: GameDataPersistenceManager is not initialized.", logContext);
            return false;
        }

        TerrainManager terrainManager = context.TerrainManager;
        if (terrainManager == null)
        {
            Debug.LogError("MiningRestoreDataValidator: TerrainManager is not assigned.", logContext);
            return false;
        }

        TerrainDataManager terrainDataManager = terrainManager.TerrainDataManager;
        if (terrainDataManager == null)
        {
            Debug.LogError("MiningRestoreDataValidator: TerrainDataManager is not assigned.", logContext);
            isValid = false;
        }

        int voxelsPerBlock = terrainManager.Settings != null ? terrainManager.Settings.voxelsPerBlock : 0;
        if (voxelsPerBlock <= 0)
        {
            Debug.LogError(
                $"MiningRestoreDataValidator: voxelsPerBlock must be positive. value={voxelsPerBlock}",
                logContext);
            isValid = false;
        }

        isValid &= ValidateTerrainSeed(persistence, logContext);
        isValid &= ValidateDestroyedBlocks(persistence, voxelsPerBlock, logContext);
        isValid &= ValidateVoxelCellOverrides(persistence, terrainDataManager, voxelsPerBlock, logContext);
        isValid &= ValidateSolidifiedVoxelHistory(persistence, terrainDataManager, voxelsPerBlock, logContext);
        isValid &= ValidateDroppedItems(persistence, terrainDataManager, voxelsPerBlock, logContext);
        isValid &= ValidateTorchPlacements(persistence, voxelsPerBlock, logContext);
        isValid &= ValidateToolInventory(persistence, logContext);
        ValidateFluidPersistenceData();

        return isValid;
    }

    private static bool ValidateTerrainSeed(GameDataPersistenceManager persistence, Object logContext)
    {
        if (persistence.hasInitializedSeed)
        {
            return true;
        }

        // New game data can legitimately arrive before a seed is persisted.
        return true;
    }

    private static bool ValidateDestroyedBlocks(
        GameDataPersistenceManager persistence,
        int voxelsPerBlock,
        Object logContext)
    {
        bool isValid = true;

        if (persistence.destroyedBlockPositions == null)
        {
            Debug.LogError("MiningRestoreDataValidator: destroyedBlockPositions is not configured.", logContext);
            isValid = false;
        }

        if (persistence.partiallyDestroyedBlocks == null)
        {
            Debug.LogError("MiningRestoreDataValidator: partiallyDestroyedBlocks is not configured.", logContext);
            return false;
        }

        foreach (KeyValuePair<Vector3Int, HashSet<Vector3Int>> pair in persistence.partiallyDestroyedBlocks)
        {
            Vector3Int blockPosition = pair.Key;
            HashSet<Vector3Int> localPositions = pair.Value;
            if (localPositions == null)
            {
                Debug.LogError(
                    $"MiningRestoreDataValidator: partiallyDestroyedBlocks contains a null voxel set at block={blockPosition}.",
                    logContext);
                isValid = false;
                continue;
            }

            if (persistence.destroyedBlockPositions != null &&
                persistence.destroyedBlockPositions.Contains(blockPosition))
            {
                Debug.LogError(
                    $"MiningRestoreDataValidator: block={blockPosition} is listed as both fully and partially destroyed.",
                    logContext);
                isValid = false;
            }

            foreach (Vector3Int localPosition in localPositions)
            {
                isValid &= ValidateLocalVoxelPosition(
                    localPosition,
                    voxelsPerBlock,
                    $"partiallyDestroyedBlocks[{blockPosition}]",
                    logContext);
            }
        }

        return isValid;
    }

    private static bool ValidateVoxelCellOverrides(
        GameDataPersistenceManager persistence,
        TerrainDataManager terrainDataManager,
        int voxelsPerBlock,
        Object logContext)
    {
        if (persistence.voxelCellOverrides == null)
        {
            Debug.LogError("MiningRestoreDataValidator: voxelCellOverrides is not configured.", logContext);
            return false;
        }

        bool isValid = true;
        foreach (KeyValuePair<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> blockPair in persistence.voxelCellOverrides)
        {
            Vector3Int blockPosition = blockPair.Key;
            Dictionary<Vector3Int, VoxelCellData> overrides = blockPair.Value;
            if (overrides == null)
            {
                Debug.LogError(
                    $"MiningRestoreDataValidator: voxelCellOverrides contains a null cell map at block={blockPosition}.",
                    logContext);
                isValid = false;
                continue;
            }

            foreach (KeyValuePair<Vector3Int, VoxelCellData> cellPair in overrides)
            {
                Vector3Int localPosition = cellPair.Key;
                VoxelCellData data = cellPair.Value;

                isValid &= ValidateLocalVoxelPosition(
                    localPosition,
                    voxelsPerBlock,
                    $"voxelCellOverrides[{blockPosition}]",
                    logContext);

                if (data.blockPosition != blockPosition)
                {
                    Debug.LogError(
                        $"MiningRestoreDataValidator: voxel override block key mismatch. key={blockPosition}, data={data.blockPosition}.",
                        logContext);
                    isValid = false;
                }

                if (data.localVoxelPosition != localPosition)
                {
                    Debug.LogError(
                        $"MiningRestoreDataValidator: voxel override local key mismatch. key={localPosition}, data={data.localVoxelPosition}.",
                        logContext);
                    isValid = false;
                }

                if (data.maxHealth <= 0)
                {
                    Debug.LogError(
                        $"MiningRestoreDataValidator: voxel override maxHealth must be positive. block={blockPosition}, local={localPosition}, maxHealth={data.maxHealth}.",
                        logContext);
                    isValid = false;
                }

                if (data.health < 0 || data.health > data.maxHealth)
                {
                    Debug.LogError(
                        $"MiningRestoreDataValidator: voxel override health is out of range. block={blockPosition}, local={localPosition}, health={data.health}, maxHealth={data.maxHealth}.",
                        logContext);
                    isValid = false;
                }

                if (!string.IsNullOrEmpty(data.blockDataName))
                {
                    isValid &= ValidateBlockDataName(
                        terrainDataManager,
                        data.blockDataName,
                        $"voxelCellOverrides[{blockPosition}][{localPosition}]",
                        logContext);
                }
            }
        }

        return isValid;
    }

    private static bool ValidateSolidifiedVoxelHistory(
        GameDataPersistenceManager persistence,
        TerrainDataManager terrainDataManager,
        int voxelsPerBlock,
        Object logContext)
    {
        if (persistence.solidifiedVoxelHistory == null)
        {
            Debug.LogError("MiningRestoreDataValidator: solidifiedVoxelHistory is not configured.", logContext);
            return false;
        }

        bool isValid = true;
        for (int i = 0; i < persistence.solidifiedVoxelHistory.Count; i++)
        {
            SolidifiedVoxelRecord record = persistence.solidifiedVoxelHistory[i];
            isValid &= ValidateLocalVoxelPosition(
                record.localVoxelPosition,
                voxelsPerBlock,
                $"solidifiedVoxelHistory[{i}]",
                logContext);
            isValid &= ValidateRequiredBlockDataName(
                terrainDataManager,
                record.blockDataName,
                $"solidifiedVoxelHistory[{i}]",
                logContext);
            isValid &= ValidateFiniteVector(record.worldPosition, $"solidifiedVoxelHistory[{i}].worldPosition", logContext);

            if (record.solidifiedTime < 0f || !IsFinite(record.solidifiedTime))
            {
                Debug.LogError(
                    $"MiningRestoreDataValidator: solidifiedTime is invalid at solidifiedVoxelHistory[{i}]. value={record.solidifiedTime}",
                    logContext);
                isValid = false;
            }
        }

        return isValid;
    }

    private static bool ValidateDroppedItems(
        GameDataPersistenceManager persistence,
        TerrainDataManager terrainDataManager,
        int voxelsPerBlock,
        Object logContext)
    {
        if (persistence.droppedItems == null)
        {
            Debug.LogError("MiningRestoreDataValidator: droppedItems is not configured.", logContext);
            return false;
        }

        bool isValid = true;
        for (int i = 0; i < persistence.droppedItems.Count; i++)
        {
            DroppedItemData item = persistence.droppedItems[i];
            string label = $"droppedItems[{i}]";

            isValid &= ValidateRequiredBlockDataName(terrainDataManager, item.blockDataName, label, logContext);
            isValid &= ValidateFiniteVector(item.position, $"{label}.position", logContext);
            isValid &= ValidateFiniteQuaternion(item.rotation, $"{label}.rotation", logContext);
            isValid &= ValidateFiniteVector(item.scale, $"{label}.scale", logContext);

            if (item.hasSolidificationTarget)
            {
                isValid &= ValidateLocalVoxelPosition(
                    item.solidifiedLocalVoxelPosition,
                    voxelsPerBlock,
                    $"{label}.solidifiedLocalVoxelPosition",
                    logContext);
            }

            if (item.solidificationElapsedSeconds < 0f || !IsFinite(item.solidificationElapsedSeconds))
            {
                Debug.LogError(
                    $"MiningRestoreDataValidator: solidificationElapsedSeconds is invalid at {label}. value={item.solidificationElapsedSeconds}",
                    logContext);
                isValid = false;
            }
        }

        return isValid;
    }

    private static bool ValidateTorchPlacements(
        GameDataPersistenceManager persistence,
        int voxelsPerBlock,
        Object logContext)
    {
        if (persistence.torchPlacements == null)
        {
            Debug.LogError("MiningRestoreDataValidator: torchPlacements is not configured.", logContext);
            return false;
        }

        bool isValid = true;
        HashSet<VoxelCellKey> uniquePlacements = new HashSet<VoxelCellKey>();
        for (int i = 0; i < persistence.torchPlacements.Count; i++)
        {
            TorchPlacementData placement = persistence.torchPlacements[i];
            if (placement == null)
            {
                Debug.LogError($"MiningRestoreDataValidator: torchPlacements contains a null record at index {i}.", logContext);
                isValid = false;
                continue;
            }

            isValid &= ValidateLocalVoxelPosition(
                placement.localVoxelPosition,
                voxelsPerBlock,
                $"torchPlacements[{i}]",
                logContext);

            VoxelCellKey key = new VoxelCellKey(placement.blockPosition, placement.localVoxelPosition);
            if (!uniquePlacements.Add(key))
            {
                Debug.LogError(
                    $"MiningRestoreDataValidator: duplicate torch placement at block={placement.blockPosition}, local={placement.localVoxelPosition}.",
                    logContext);
                isValid = false;
            }
        }

        return isValid;
    }

    private static bool ValidateToolInventory(GameDataPersistenceManager persistence, Object logContext)
    {
        if (!persistence.hasToolInventoryData)
        {
            return true;
        }

        if (persistence.toolSlots == null)
        {
            Debug.LogError("MiningRestoreDataValidator: toolSlots is not configured.", logContext);
            return false;
        }

        bool isValid = true;
        HashSet<string> slotIds = new HashSet<string>();
        for (int i = 0; i < persistence.toolSlots.Count; i++)
        {
            ToolSlotPersistenceData slot = persistence.toolSlots[i];
            if (slot == null)
            {
                Debug.LogError($"MiningRestoreDataValidator: toolSlots contains a null record at index {i}.", logContext);
                isValid = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(slot.slotId))
            {
                Debug.LogError($"MiningRestoreDataValidator: toolSlots[{i}] has no slotId.", logContext);
                isValid = false;
                continue;
            }

            if (!slotIds.Add(slot.slotId))
            {
                Debug.LogError($"MiningRestoreDataValidator: duplicate tool slotId='{slot.slotId}'.", logContext);
                isValid = false;
            }
        }

        if (!string.IsNullOrEmpty(persistence.mainToolSlotId) && !slotIds.Contains(persistence.mainToolSlotId))
        {
            Debug.LogError(
                $"MiningRestoreDataValidator: mainToolSlotId='{persistence.mainToolSlotId}' does not exist in toolSlots.",
                logContext);
            isValid = false;
        }

        if (!string.IsNullOrEmpty(persistence.subToolSlotId) && !slotIds.Contains(persistence.subToolSlotId))
        {
            Debug.LogError(
                $"MiningRestoreDataValidator: subToolSlotId='{persistence.subToolSlotId}' does not exist in toolSlots.",
                logContext);
            isValid = false;
        }

        return isValid;
    }

    private static void ValidateFluidPersistenceData()
    {
        // Fluid persistence has no serialized persistence records yet; keep this explicit hook for the phase.
    }

    private static bool ValidateRequiredBlockDataName(
        TerrainDataManager terrainDataManager,
        string blockDataName,
        string label,
        Object logContext)
    {
        if (string.IsNullOrWhiteSpace(blockDataName))
        {
            Debug.LogError($"MiningRestoreDataValidator: {label} has no blockDataName.", logContext);
            return false;
        }

        return ValidateBlockDataName(terrainDataManager, blockDataName, label, logContext);
    }

    private static bool ValidateBlockDataName(
        TerrainDataManager terrainDataManager,
        string blockDataName,
        string label,
        Object logContext)
    {
        if (terrainDataManager == null)
        {
            return false;
        }

        if (terrainDataManager.GetBlockDataByName(blockDataName) != null)
        {
            return true;
        }

        Debug.LogError($"MiningRestoreDataValidator: {label} has unknown blockDataName='{blockDataName}'.", logContext);
        return false;
    }

    private static bool ValidateLocalVoxelPosition(
        Vector3Int localPosition,
        int voxelsPerBlock,
        string label,
        Object logContext)
    {
        if (voxelsPerBlock <= 0)
        {
            return false;
        }

        if (localPosition.x >= 0 && localPosition.x < voxelsPerBlock &&
            localPosition.y >= 0 && localPosition.y < voxelsPerBlock &&
            localPosition.z >= 0 && localPosition.z < voxelsPerBlock)
        {
            return true;
        }

        Debug.LogError(
            $"MiningRestoreDataValidator: {label} has out-of-range local voxel position={localPosition}, voxelsPerBlock={voxelsPerBlock}.",
            logContext);
        return false;
    }

    private static bool ValidateFiniteVector(Vector3 value, string label, Object logContext)
    {
        if (IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z))
        {
            return true;
        }

        Debug.LogError($"MiningRestoreDataValidator: {label} contains NaN or Infinity. value={value}", logContext);
        return false;
    }

    private static bool ValidateFiniteQuaternion(Quaternion value, string label, Object logContext)
    {
        if (IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w))
        {
            return true;
        }

        Debug.LogError($"MiningRestoreDataValidator: {label} contains NaN or Infinity. value={value}", logContext);
        return false;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
