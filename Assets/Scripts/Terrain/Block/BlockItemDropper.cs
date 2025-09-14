using UnityEngine;

/// <summary>
/// ブロックからのアイテムドロップ処理を担当するクラス
/// Block.csから分離されたアイテムドロップ関連の機能を提供
/// </summary>
public class BlockItemDropper
{
    // 設定パラメータ
    private BlockData blockData;
    private bool enableTextureExtraction;
    private float voxelWorldSize;
    private int voxelsPerBlock;
    private VoxelTextureExtractor textureExtractor;
    private Texture2D texture1, texture2;
    private bool[,,] useTexture1Pattern;

    /// <summary>
    /// BlockItemDropperを初期化
    /// </summary>
    public void Initialize(BlockData data, bool enableTexture, float worldSize, int vPerBlock, 
        VoxelTextureExtractor extractor, Texture2D tex1, Texture2D tex2, bool[,,] pattern)
    {
        blockData = data;
        enableTextureExtraction = enableTexture;
        voxelWorldSize = worldSize;
        voxelsPerBlock = vPerBlock;
        textureExtractor = extractor;
        texture1 = tex1;
        texture2 = tex2;
        useTexture1Pattern = pattern;
    }

    /// <summary>
    /// アイテムをドロップする（座標情報なし）
    /// </summary>
    public void DropItem(Vector3 position)
    {
        DropItem(position, -1, -1, -1);
    }

    /// <summary>
    /// アイテムをドロップする（ボクセル座標付き）
    /// </summary>
    public void DropItem(Vector3 position, int voxelX, int voxelY, int voxelZ)
    {
        if (DroppedItemManager.Instance == null)
        {
            Debug.LogError("DroppedItemManager.Instance is null. Please ensure a DroppedItemManager exists in the scene.");
            return;
        }
        if (blockData == null)
        {
            Debug.LogError("BlockData is null. Check initialization.");
            return;
        }
        if (blockData.droppedItemPrefab == null)
        {
            Debug.LogError($"BlockData '{blockData.name}' has no droppedItemPrefab assigned.", blockData);
            return;
        }

        // DroppedItemManagerから正しいPrefabのアイテムを取得
        GameObject item = DroppedItemManager.Instance.GetItem(blockData.droppedItemPrefab);
        if (item == null) return;

        item.transform.position = position;
        item.transform.rotation = Quaternion.identity;

        // 自動スケール調整が有効な場合
        if (blockData.autoScale)
        {
            float targetScale = voxelWorldSize * blockData.scaleMultiplier;
            item.transform.localScale = Vector3.one * targetScale;
        }

        // ボクセル座標が有効な場合、テクスチャ抽出を実行
        if (enableTextureExtraction && voxelX >= 0 && voxelY >= 0 && voxelZ >= 0 && 
            voxelX < voxelsPerBlock && voxelY < voxelsPerBlock && voxelZ < voxelsPerBlock)
        {
            ApplyVoxelTextureToDroppedItem(item, voxelX, voxelY, voxelZ);
        }
        else
        {
            // テクスチャ抽出が無効または座標が無効な場合、デフォルトマテリアルを適用
            ApplyDefaultMaterial(item);
        }

        // Rigidbodyが無い場合は追加
        Rigidbody itemRigidbody = item.GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = item.AddComponent<Rigidbody>();
        }
        
        // 質量を設定
        float volume = Mathf.Pow(voxelWorldSize, 3);
        itemRigidbody.mass = volume * blockData.density;

        // 移動モードに応じてRigidbodyのConstraintを設定
        SetDroppedItemConstraints(itemRigidbody);

        // DroppedItemコンポーネントの処理
        DroppedItem droppedItemComponent = item.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            droppedItemComponent.resourceType = blockData.resourceType; // ResourceTypeを設定
            droppedItemComponent.enabled = !blockData.disableRotation;
        }

        // タグが設定されていない場合は設定
        if (!item.CompareTag("DroppedItem"))
        {
            item.tag = "DroppedItem";
        }
    }

    /// <summary>
    /// ドロップアイテムにボクセルテクスチャを適用
    /// </summary>
    private void ApplyVoxelTextureToDroppedItem(GameObject item, int voxelX, int voxelY, int voxelZ)
    {
        if (textureExtractor != null)
        {
            textureExtractor.ApplyVoxelTextureToDroppedItem(item, voxelX, voxelY, voxelZ, 
                texture1, texture2, useTexture1Pattern, voxelsPerBlock);
        }
    }

    /// <summary>
    /// デフォルトマテリアルを適用
    /// </summary>
    private void ApplyDefaultMaterial(GameObject item)
    {
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer != null)
        {
            var material = new Material(Shader.Find("Custom/UnlitBlock"));
            material.color = Color.white; // Unlitなのでテクスチャの色をそのまま出すために白に
            itemRenderer.material = material;
        }
    }

    /// <summary>
    /// ドロップアイテムのRigidbodyに移動モードに応じた制約を設定
    /// </summary>
    private void SetDroppedItemConstraints(Rigidbody itemRigidbody)
    {
        if (itemRigidbody == null) return;

        // 現在の移動モードを取得
        PlayerController.MoveMode currentMoveMode = GetCurrentMoveMode();

        // 移動モードに応じて制約を設定
        switch (currentMoveMode)
        {
            case PlayerController.MoveMode.SideScroller:
                // SideScrollerモード: XY平面のみ移動、Z軸は固定
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionZ | 
                                          RigidbodyConstraints.FreezeRotationX | 
                                          RigidbodyConstraints.FreezeRotationY;
                break;

            case PlayerController.MoveMode.TopDown:
                // TopDownモード: XZ平面のみ移動、Y軸は固定
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionY | 
                                          RigidbodyConstraints.FreezeRotationX | 
                                          RigidbodyConstraints.FreezeRotationZ;
                break;

            default:
                // デフォルトは制約なし
                itemRigidbody.constraints = RigidbodyConstraints.None;
                break;
        }
    }

    /// <summary>
    /// 現在のゲームの移動モードを取得
    /// </summary>
    private PlayerController.MoveMode GetCurrentMoveMode()
    {
        // プレイヤーオブジェクトを探してPlayerControllerから移動モードを取得
        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            return playerController.currentMoveMode;
        }

        // プレイヤーが見つからない場合はデフォルトでTopDownモードを返す
        return PlayerController.MoveMode.TopDown;
    }
}
