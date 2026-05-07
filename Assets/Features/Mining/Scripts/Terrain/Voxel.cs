using UnityEngine;

[System.Serializable]
public class Voxel
{
    public Vector3Int blockPosition;
    public Vector3Int localPosition;
    public Vector3 worldPosition;
    public bool isActive;
    public int health;
    public int maxHealth;
    public VoxelType voxelType;
    public BlockData blockData;
    public string blockDataName;
    public ResourceType resourceType;
    public bool useTexture1;
    public float lastModifiedTime;

    public Voxel(Vector3Int blockPos, Vector3Int localPos, Vector3 worldPos, int hp, VoxelType type, BlockData data = null, bool useTexture1 = true)
    {
        blockPosition = blockPos;
        localPosition = localPos;
        worldPosition = worldPos;
        isActive = true;
        health = hp;
        maxHealth = hp;
        voxelType = type;
        this.useTexture1 = useTexture1;
        SetBlockData(data);
        lastModifiedTime = Time.time;
    }

    public void SetBlockData(BlockData data)
    {
        blockData = data;
        blockDataName = data != null ? data.name : string.Empty;
        resourceType = data != null ? data.resourceType : ResourceType.Stone;

        if (data != null)
        {
            maxHealth = Mathf.Max(1, data.voxelHp);
            health = Mathf.Clamp(health, 1, maxHealth);
        }
    }

    public void ApplyCellData(VoxelCellData data, BlockData resolvedBlockData)
    {
        SetBlockData(resolvedBlockData);
        blockDataName = !string.IsNullOrEmpty(data.blockDataName) ? data.blockDataName : blockDataName;
        resourceType = data.resourceType;
        isActive = data.isActive;
        maxHealth = Mathf.Max(1, data.maxHealth);
        health = Mathf.Clamp(data.health, 0, maxHealth);
        useTexture1 = data.useTexture1;
        lastModifiedTime = Time.time;
    }
}

[System.Serializable]
public struct VoxelCellData
{
    public Vector3Int blockPosition;
    public Vector3Int localVoxelPosition;
    public string blockDataName;
    public ResourceType resourceType;
    public int health;
    public int maxHealth;
    public bool isActive;
    public bool useTexture1;

    public VoxelCellData(Voxel voxel)
    {
        blockPosition = voxel.blockPosition;
        localVoxelPosition = voxel.localPosition;
        blockDataName = voxel.blockDataName;
        resourceType = voxel.resourceType;
        health = voxel.health;
        maxHealth = voxel.maxHealth;
        isActive = voxel.isActive;
        useTexture1 = voxel.useTexture1;
    }
}

[System.Serializable]
public struct SolidifiedVoxelRecord
{
    public Vector3Int blockPosition;
    public Vector3Int localVoxelPosition;
    public string blockDataName;
    public Vector3 worldPosition;
    public float solidifiedTime;
}

public enum VoxelType
{
    Standard,
    Reinforced,
    Fragile,
    Unbreakable,
    Special
}
