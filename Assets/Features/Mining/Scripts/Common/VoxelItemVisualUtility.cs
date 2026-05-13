using UnityEngine;

public static class VoxelItemVisualUtility
{
    public static bool TryCreateAnimationItem(
        Vector3 position,
        Transform parent,
        VoxelItemData itemData,
        TerrainDataManager terrainDataManager,
        string context,
        out GameObject itemObject)
    {
        itemObject = null;
        if (itemData == null || !itemData.IsValid(context))
        {
            return false;
        }

        GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.name = $"VoxelTransfer_{itemData.blockDataName}";
        created.transform.SetParent(parent, true);
        created.transform.position = position;

        Collider collider = created.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        if (!TryApplyAppearance(created, itemData, terrainDataManager, context))
        {
            Object.Destroy(created);
            return false;
        }

        DroppedItem droppedItem = created.GetComponent<DroppedItem>();
        if (droppedItem != null)
        {
            droppedItem.enabled = false;
        }

        itemObject = created;
        return true;
    }

    public static bool TryApplyAppearance(
        GameObject target,
        VoxelItemData itemData,
        TerrainDataManager terrainDataManager,
        string context)
    {
        if (target == null)
        {
            Debug.LogError($"{context}: target object is null.");
            return false;
        }

        if (itemData == null || !itemData.IsValid(context))
        {
            return false;
        }

        if (terrainDataManager == null)
        {
            Debug.LogError($"{context}: TerrainDataManager is missing.");
            return false;
        }

        BlockData blockData = terrainDataManager.GetBlockDataByName(itemData.blockDataName);
        if (blockData == null)
        {
            Debug.LogError($"{context}: BlockData '{itemData.blockDataName}' could not be resolved.");
            return false;
        }

        Texture2D texture1 = blockData.textures != null && blockData.textures.Count > 0 ? blockData.textures[0] : null;
        Texture2D texture2 = blockData.textures != null && blockData.textures.Count > 1 ? blockData.textures[1] : null;
        if (!ValidateReferencedTextures(itemData, texture1, texture2, context))
        {
            return false;
        }

        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = target.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = target.AddComponent<MeshRenderer>();
        }

        DroppedItem droppedItem = target.GetComponent<DroppedItem>();
        if (droppedItem == null)
        {
            droppedItem = target.AddComponent<DroppedItem>();
        }

        SetWorldScale(target.transform, itemData.scale);
        droppedItem.resourceType = itemData.resourceType;
        droppedItem.blockDataName = itemData.blockDataName;
        droppedItem.scale = itemData.scale;
        droppedItem.ApplyFaceTextureData(itemData.GetFaceTextureDataCopy(), texture1, texture2);
        return true;
    }

    public static TerrainDataManager ResolveTerrainDataManager()
    {
        TerrainManager terrainManager = Object.FindFirstObjectByType<TerrainManager>();
        return terrainManager != null ? terrainManager.TerrainDataManager : null;
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z);
    }

    private static bool ValidateReferencedTextures(
        VoxelItemData itemData,
        Texture2D texture1,
        Texture2D texture2,
        string context)
    {
        DroppedItemFaceTextureData[] faces = itemData.faceTextureData;
        for (int i = 0; i < faces.Length; i++)
        {
            DroppedItemFaceTextureData face = faces[i];
            Texture2D requiredTexture = face.useTexture1 ? texture1 : texture2;
            if (requiredTexture == null)
            {
                string slot = face.useTexture1 ? "texture1" : "texture2";
                Debug.LogError($"{context}: voxel item '{itemData.blockDataName}' references missing {slot} on face {i}.");
                return false;
            }
        }

        return true;
    }
}
