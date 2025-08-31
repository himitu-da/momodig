using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロック管理クラス
/// 地形のブロックレベルでの管理を担当
/// </summary>
public class BlockManager : MonoBehaviour
{
    [Header("Block Configuration")]
    [SerializeField] private List<BlockData> blocks = new List<BlockData>();
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// ブロックデータ構造
    /// </summary>
    [System.Serializable]
    public class BlockData
    {
        public Vector3Int position;          // ブロックの論理座標
        public Vector3 worldPosition;       // ワールド座標
        public Block block;                 // 実際のBlockへの参照
        public bool isActive = true;        // ブロックがアクティブかどうか
        public int voxelCount;              // このブロック内のボクセル数
        
        public BlockData(Vector3Int pos, Vector3 worldPos)
        {
            position = pos;
            worldPosition = worldPos;
        }
    }
    
    /// <summary>
    /// TerrainManagerからの参照
    /// </summary>
    private TerrainManager terrainManager;
    
    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Initialized with TerrainManager");
        }
    }
    
    /// <summary>
    /// ブロックを作成
    /// </summary>
    public BlockData CreateBlock(Vector3Int blockPos, Vector3 worldPos, bool[,,] pattern, TerrainSettings settings, Transform parent)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Creating block at {blockPos}");
        }
        
        // ブロックデータを作成
        BlockData blockData = new BlockData(blockPos, worldPos);
        
        // GameObjectとBlockを作成
        GameObject blockObj = new GameObject($"Block_{blockPos.x}_{blockPos.y}_{blockPos.z}");
        blockObj.transform.parent = parent;
        blockObj.transform.position = worldPos;
        
        // スケール調整
        float scale = settings.blockSize / settings.voxelSize;
        blockObj.transform.localScale = new Vector3(scale, scale, scale);
        
        // Blockコンポーネントを追加
        Block block = blockObj.AddComponent<Block>();
        blockData.block = block;
        
        // マテリアル設定
        CreateBlockMaterial(blockObj, settings);
        
        // Blockを初期化
        block.Initialize(
            terrainManager.VoxelManager, // VoxelManagerの参照を渡す
            blockPos,                    // ブロックの座標を渡す
            pattern, 
            settings.voxelSize, 
            settings.blockSize, 
            settings.voxelHp,
            settings.droppedItemPrefab,
            settings.disableRotation,
            settings.autoScale,
            settings.scaleMultiplier,
            settings.texture1,
            settings.texture2
        );
        
        // ボクセル数を計算
        blockData.voxelCount = CalculateVoxelCount(pattern);
        
        // リストに追加
        blocks.Add(blockData);
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Created block with {blockData.voxelCount} voxels");
        }
        
        return blockData;
    }
    
    /// <summary>
    /// ブロック用マテリアルを作成
    /// </summary>
    private void CreateBlockMaterial(GameObject blockObj, TerrainSettings settings)
    {
        var renderer = blockObj.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = blockObj.AddComponent<MeshRenderer>();
        }
        
        // URP Transparentマテリアルを作成
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_AlphaClip", 1); // Alpha Clipping
        mat.mainTexture = settings.texture1; // デフォルトテクスチャ
        
        renderer.material = mat;
    }
    
    /// <summary>
    /// パターンからボクセル数を計算
    /// </summary>
    private int CalculateVoxelCount(bool[,,] pattern)
    {
        int count = 0;
        for (int x = 0; x < pattern.GetLength(0); x++)
        {
            for (int y = 0; y < pattern.GetLength(1); y++)
            {
                for (int z = 0; z < pattern.GetLength(2); z++)
                {
                    if (pattern[x, y, z]) count++;
                }
            }
        }
        return count;
    }
    
    /// <summary>
    /// 指定座標のブロックを取得
    /// </summary>
    public BlockData GetBlockAt(Vector3Int position)
    {
        foreach (var block in blocks)
        {
            if (block.position == position)
            {
                return block;
            }
        }
        return null;
    }
    
    /// <summary>
    /// すべてのブロックを取得
    /// </summary>
    public List<BlockData> GetAllBlocks()
    {
        return new List<BlockData>(blocks);
    }
    
    /// <summary>
    /// アクティブなブロックの数を取得
    /// </summary>
    public int GetActiveBlockCount()
    {
        int count = 0;
        foreach (var block in blocks)
        {
            if (block.isActive) count++;
        }
        return count;
    }
    
    /// <summary>
    /// ブロックのアクティブ状態を設定
    /// </summary>
    public void SetBlockActive(Vector3Int position, bool active)
    {
        BlockData block = GetBlockAt(position);
        if (block != null && block.block != null)
        {
            block.isActive = active;
            block.block.gameObject.SetActive(active);
            
            if (showBlockDebugInfo)
            {
                Debug.Log($"BlockManager: Set block {position} active: {active}");
            }
        }
    }
    
    /// <summary>
    /// すべてのブロックを削除
    /// </summary>
    public void ClearAllBlocks()
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Clearing {blocks.Count} blocks");
        }
        
        foreach (var block in blocks)
        {
            if (block.block != null)
            {
                DestroyImmediate(block.block.gameObject);
            }
        }
        
        blocks.Clear();
    }
    
    /// <summary>
    /// デバッグ情報を取得
    /// </summary>
    public string GetDebugInfo()
    {
        int totalVoxels = 0;
        foreach (var block in blocks)
        {
            totalVoxels += block.voxelCount;
        }
        
        return $"BlockManager - Total: {blocks.Count}, Active: {GetActiveBlockCount()}, Voxels: {totalVoxels}";
    }
    
    void OnDestroy()
    {
        ClearAllBlocks();
    }
}
