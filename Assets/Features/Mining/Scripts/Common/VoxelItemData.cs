using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VoxelItemData
{
    public ResourceType resourceType;
    public string blockDataName;
    public Vector3 scale;
    public Vector2 uvBase;
    public Vector2 uvSize;
    public bool useTexture1;
    public DroppedItemFaceTextureData[] faceTextureData;

    public VoxelItemData(
        ResourceType resourceType,
        string blockDataName,
        Vector3 scale,
        DroppedItemFaceTextureData[] faceTextureData,
        Vector2 uvBase,
        Vector2 uvSize,
        bool useTexture1)
    {
        this.resourceType = resourceType;
        this.blockDataName = blockDataName;
        this.scale = scale;
        this.faceTextureData = CloneFaceTextureData(faceTextureData);
        this.uvBase = uvBase;
        this.uvSize = uvSize;
        this.useTexture1 = useTexture1;
    }

    public VoxelItemData Clone()
    {
        return new VoxelItemData(resourceType, blockDataName, scale, faceTextureData, uvBase, uvSize, useTexture1);
    }

    public DroppedItemFaceTextureData[] GetFaceTextureDataCopy()
    {
        return CloneFaceTextureData(faceTextureData);
    }

    public bool IsValid(string context)
    {
        if (string.IsNullOrEmpty(blockDataName))
        {
            Debug.LogError($"{context}: voxel item has no blockDataName.");
            return false;
        }

        if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
        {
            Debug.LogError($"{context}: voxel item '{blockDataName}' has invalid scale {scale}.");
            return false;
        }

        if (faceTextureData == null || faceTextureData.Length != DroppedItem.FaceNormals.Length)
        {
            Debug.LogError($"{context}: voxel item '{blockDataName}' has invalid face texture data.");
            return false;
        }

        for (int i = 0; i < faceTextureData.Length; i++)
        {
            DroppedItemFaceTextureData faceData = faceTextureData[i];
            if (!faceData.hasTexture)
            {
                Debug.LogError($"{context}: voxel item '{blockDataName}' is missing texture data for face {i}.");
                return false;
            }

            if (faceData.uvSize.x <= 0f || faceData.uvSize.y <= 0f)
            {
                Debug.LogError($"{context}: voxel item '{blockDataName}' has invalid UV size on face {i}.");
                return false;
            }
        }

        return true;
    }

    public static bool TryCreateFromDroppedItem(DroppedItem droppedItem, out VoxelItemData itemData)
    {
        itemData = null;
        if (droppedItem == null)
        {
            Debug.LogError("VoxelItemData: dropped item component is missing.");
            return false;
        }

        Vector3 sourceScale = droppedItem.scale;
        if (sourceScale == Vector3.zero)
        {
            sourceScale = droppedItem.transform.localScale;
        }

        itemData = new VoxelItemData(
            droppedItem.resourceType,
            droppedItem.blockDataName,
            sourceScale,
            droppedItem.faceTextureData,
            droppedItem.uvBase,
            droppedItem.uvSize,
            droppedItem.useTexture1);

        if (!itemData.IsValid("VoxelItemData"))
        {
            itemData = null;
            return false;
        }

        return true;
    }

    public static bool TryAggregateResourceCounts(
        IList<VoxelItemData> items,
        out Dictionary<ResourceType, int> resourceCounts,
        string context)
    {
        resourceCounts = new Dictionary<ResourceType, int>();
        if (items == null)
        {
            Debug.LogError($"{context}: voxel item list is null.");
            return false;
        }

        foreach (VoxelItemData item in items)
        {
            if (item == null || !item.IsValid(context))
            {
                resourceCounts.Clear();
                return false;
            }

            if (!resourceCounts.ContainsKey(item.resourceType))
            {
                resourceCounts[item.resourceType] = 0;
            }

            resourceCounts[item.resourceType]++;
        }

        return true;
    }

    private static DroppedItemFaceTextureData[] CloneFaceTextureData(DroppedItemFaceTextureData[] source)
    {
        if (source == null)
        {
            return null;
        }

        DroppedItemFaceTextureData[] clone = new DroppedItemFaceTextureData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            clone[i] = source[i];
        }

        return clone;
    }
}
