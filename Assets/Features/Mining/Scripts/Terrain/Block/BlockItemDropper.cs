using UnityEngine;

public static class BlockItemDropper
{
    public static void DropItem(Vector3 position, BlockData voxelBlockData, bool useTexture1, int voxelX, int voxelY, int voxelZ, int voxelsPerBlock, float voxelWorldSize, VoxelTextureExtractor textureExtractor)
    {
        DropItem(position, voxelBlockData, useTexture1, voxelX, voxelY, voxelZ, voxelsPerBlock, voxelWorldSize, textureExtractor, default, false);
    }

    public static void DropItem(
        Vector3 position,
        BlockData voxelBlockData,
        bool useTexture1,
        int voxelX,
        int voxelY,
        int voxelZ,
        int voxelsPerBlock,
        float voxelWorldSize,
        VoxelTextureExtractor textureExtractor,
        MiningInfo miningInfo,
        bool applyInitialForce)
    {
        if (DroppedItemManager.Instance == null)
        {
            Debug.LogError("DroppedItemManager.Instance is null. Please ensure a DroppedItemManager exists in the scene.");
            return;
        }

        if (voxelBlockData == null)
        {
            Debug.LogError("BlockData is null. Cannot drop item.");
            return;
        }

        if (voxelBlockData.droppedItemPrefab == null)
        {
            Debug.LogError($"BlockData '{voxelBlockData.name}' has no droppedItemPrefab assigned.", voxelBlockData);
            return;
        }

        DroppedItemManager.Instance.EnqueueDropItem(
            position,
            voxelBlockData,
            useTexture1,
            voxelX,
            voxelY,
            voxelZ,
            voxelsPerBlock,
            voxelWorldSize,
            textureExtractor,
            miningInfo,
            applyInitialForce);
    }

    internal static void SetupDroppedItem(GameObject item, BlockData data, bool useTexture1, int voxelX, int voxelY, int voxelZ, int voxelsPerBlock, float voxelWorldSize, VoxelTextureExtractor textureExtractor)
    {
        if (data.autoScale)
        {
            float targetScale = voxelWorldSize * data.scaleMultiplier;
            item.transform.localScale = Vector3.one * targetScale;
        }

        bool hasValidVoxelCoord = voxelX >= 0 && voxelY >= 0 && voxelZ >= 0 &&
                                  voxelX < voxelsPerBlock && voxelY < voxelsPerBlock && voxelZ < voxelsPerBlock;

        if (textureExtractor != null && hasValidVoxelCoord)
        {
            ApplyVoxelTextureToDroppedItem(item, data, useTexture1, voxelX, voxelY, voxelZ, voxelsPerBlock, textureExtractor);
        }
        else
        {
            ApplyDefaultMaterial(item, data);
        }

        Rigidbody itemRigidbody = item.GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = item.AddComponent<Rigidbody>();
        }

        float volume = Mathf.Pow(voxelWorldSize, 3);
        itemRigidbody.mass = volume * data.density;
        SetDroppedItemConstraints(itemRigidbody);

        DroppedItem droppedItemComponent = item.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            droppedItemComponent.ResetSolidificationState();
            droppedItemComponent.resourceType = data.resourceType;
            droppedItemComponent.blockDataName = data.name;
            droppedItemComponent.scale = item.transform.localScale;
            droppedItemComponent.enabled = !data.disableRotation;
        }

        if (!item.CompareTag("DroppedItem"))
        {
            item.tag = "DroppedItem";
        }
    }

    private static void ApplyVoxelTextureToDroppedItem(GameObject item, BlockData data, bool useTexture1, int voxelX, int voxelY, int voxelZ, int voxelsPerBlock, VoxelTextureExtractor textureExtractor)
    {
        Texture2D sourceTexture1 = (data.textures != null && data.textures.Count > 0) ? data.textures[0] : null;
        Texture2D sourceTexture2 = (data.textures != null && data.textures.Count > 1) ? data.textures[1] : null;

        textureExtractor.ApplyVoxelTextureToDroppedItem(item, voxelX, voxelY, voxelZ,
            sourceTexture1, sourceTexture2, useTexture1, voxelsPerBlock);
    }

    private static void ApplyDefaultMaterial(GameObject item, BlockData data)
    {
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer == null) return;

        var material = new Material(Shader.Find("Custom/Default"));
        material.renderQueue = RenderQueue.Geometry;
        material.color = data != null ? ResourceTypeUtility.GetResourceColor(data.resourceType) : Color.white;
        itemRenderer.material = material;
    }

    private static void SetDroppedItemConstraints(Rigidbody itemRigidbody)
    {
        if (itemRigidbody == null) return;

        itemRigidbody.constraints = RigidbodyConstraints.FreezePositionZ |
                                    RigidbodyConstraints.FreezeRotationX |
                                    RigidbodyConstraints.FreezeRotationY;
    }

}
