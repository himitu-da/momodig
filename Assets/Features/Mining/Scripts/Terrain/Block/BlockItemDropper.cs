using UnityEngine;

public class BlockItemDropper
{
    private BlockData blockData;
    private bool enableTextureExtraction;
    private float voxelWorldSize;
    private int voxelsPerBlock;
    private VoxelTextureExtractor textureExtractor;
    private bool[,,] useTexture1Pattern;
    private bool[,,] defaultTexturePattern;

    public void Initialize(BlockData data, bool enableTexture, float worldSize, int vPerBlock,
        VoxelTextureExtractor extractor, Texture2D tex1, Texture2D tex2, bool[,,] pattern)
    {
        blockData = data;
        enableTextureExtraction = enableTexture;
        voxelWorldSize = worldSize;
        voxelsPerBlock = vPerBlock;
        textureExtractor = extractor;
        useTexture1Pattern = pattern;
    }

    public void DropItem(Vector3 position)
    {
        DropItem(position, blockData, -1, -1, -1);
    }

    public void DropItem(Vector3 position, int voxelX, int voxelY, int voxelZ)
    {
        DropItem(position, blockData, voxelX, voxelY, voxelZ);
    }

    public void DropItem(Vector3 position, BlockData sourceBlockData, int voxelX, int voxelY, int voxelZ)
    {
        if (DroppedItemManager.Instance == null)
        {
            Debug.LogError("DroppedItemManager.Instance is null. Please ensure a DroppedItemManager exists in the scene.");
            return;
        }

        if (sourceBlockData == null)
        {
            Debug.LogError("BlockData is null. Check initialization.");
            return;
        }

        if (sourceBlockData.droppedItemPrefab == null)
        {
            Debug.LogError($"BlockData '{sourceBlockData.name}' has no droppedItemPrefab assigned.", sourceBlockData);
            return;
        }

        GameObject item = DroppedItemManager.Instance.GetItem(sourceBlockData.droppedItemPrefab);
        if (item == null) return;

        item.transform.position = position;
        item.transform.rotation = Quaternion.identity;

        SetupDroppedItem(item, sourceBlockData, voxelX, voxelY, voxelZ);
    }

    public void SetupDroppedItem(GameObject item, BlockData data, int voxelX = -1, int voxelY = -1, int voxelZ = -1)
    {
        if (data.autoScale)
        {
            float targetScale = voxelWorldSize * data.scaleMultiplier;
            item.transform.localScale = Vector3.one * targetScale;
        }

        if (enableTextureExtraction && voxelX >= 0 && voxelY >= 0 && voxelZ >= 0 &&
            voxelX < voxelsPerBlock && voxelY < voxelsPerBlock && voxelZ < voxelsPerBlock)
        {
            ApplyVoxelTextureToDroppedItem(item, data, voxelX, voxelY, voxelZ);
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

    private void ApplyVoxelTextureToDroppedItem(GameObject item, BlockData data, int voxelX, int voxelY, int voxelZ)
    {
        if (textureExtractor == null) return;

        Texture2D sourceTexture1 = (data.textures != null && data.textures.Count > 0) ? data.textures[0] : null;
        Texture2D sourceTexture2 = (data.textures != null && data.textures.Count > 1) ? data.textures[1] : null;
        bool[,,] texturePattern = data == blockData ? useTexture1Pattern : GetDefaultTexturePattern();

        textureExtractor.ApplyVoxelTextureToDroppedItem(item, voxelX, voxelY, voxelZ,
            sourceTexture1, sourceTexture2, texturePattern, voxelsPerBlock);
    }

    private bool[,,] GetDefaultTexturePattern()
    {
        if (defaultTexturePattern != null) return defaultTexturePattern;

        defaultTexturePattern = new bool[voxelsPerBlock, voxelsPerBlock, voxelsPerBlock];
        for (int x = 0; x < voxelsPerBlock; x++)
        {
            for (int y = 0; y < voxelsPerBlock; y++)
            {
                for (int z = 0; z < voxelsPerBlock; z++)
                {
                    defaultTexturePattern[x, y, z] = true;
                }
            }
        }

        return defaultTexturePattern;
    }

    private void ApplyDefaultMaterial(GameObject item, BlockData data)
    {
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer == null) return;

        var material = new Material(Shader.Find("Custom/Default"));
        material.renderQueue = RenderQueue.Geometry;
        material.color = data != null ? ResourceTypeUtility.GetResourceColor(data.resourceType) : Color.white;
        itemRenderer.material = material;
    }

    private void SetDroppedItemConstraints(Rigidbody itemRigidbody)
    {
        if (itemRigidbody == null) return;

        PlayerController.MoveMode currentMoveMode = GetCurrentMoveMode();

        switch (currentMoveMode)
        {
            case PlayerController.MoveMode.SideScroller:
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionZ |
                                            RigidbodyConstraints.FreezeRotationX |
                                            RigidbodyConstraints.FreezeRotationY;
                break;

            case PlayerController.MoveMode.TopDown:
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                            RigidbodyConstraints.FreezeRotationX |
                                            RigidbodyConstraints.FreezeRotationZ;
                break;

            default:
                itemRigidbody.constraints = RigidbodyConstraints.None;
                break;
        }
    }

    private PlayerController.MoveMode GetCurrentMoveMode()
    {
        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        return playerController != null ? playerController.currentMoveMode : PlayerController.MoveMode.TopDown;
    }
}
