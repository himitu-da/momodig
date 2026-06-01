using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameDataSaveFile
{
    public int version;
    public string savedAtUtc;
    public GameDataSaveData data = new GameDataSaveData();
}

[Serializable]
public class GameDataSaveData
{
    public int terrainSeed;
    public bool hasInitializedSeed;
    public List<Vector3Int> destroyedBlockPositions = new List<Vector3Int>();
    public List<PartiallyDestroyedBlockSaveRecord> partiallyDestroyedBlocks =
        new List<PartiallyDestroyedBlockSaveRecord>();
    public List<ResourceAmountSaveRecord> storedResources = new List<ResourceAmountSaveRecord>();
    public List<DroppedItemData> droppedItems = new List<DroppedItemData>();
    public List<VoxelCellOverrideBlockSaveRecord> voxelCellOverrides =
        new List<VoxelCellOverrideBlockSaveRecord>();
    public List<SolidifiedVoxelRecord> solidifiedVoxelHistory = new List<SolidifiedVoxelRecord>();
    public List<FacilityUpgradeProgressRecord> facilityUpgradeProgress =
        new List<FacilityUpgradeProgressRecord>();
    public bool hasToolInventoryData;
    public List<ToolSlotSaveRecord> toolSlots = new List<ToolSlotSaveRecord>();
    public string mainToolSlotId = "";
    public string subToolSlotId = "";
    public List<TorchPlacementData> torchPlacements = new List<TorchPlacementData>();
}

[Serializable]
public class PartiallyDestroyedBlockSaveRecord
{
    public Vector3Int blockPosition;
    public List<Vector3Int> localVoxelPositions = new List<Vector3Int>();
}

[Serializable]
public class ResourceAmountSaveRecord
{
    public ResourceType resourceType;
    public int amount;
}

[Serializable]
public class VoxelCellOverrideBlockSaveRecord
{
    public Vector3Int blockPosition;
    public List<VoxelCellData> cells = new List<VoxelCellData>();
}

[Serializable]
public class ToolSlotSaveRecord
{
    public string slotId = "";
    public string toolId = "";
}
