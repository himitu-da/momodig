using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロック管理クラス
/// 地形のブロックレベルでの管理を担当
/// </summary>
public class BlockManager : MonoBehaviour
{
    [Header("Block Configuration")]
    [SerializeField] private List<BlockInstanceData> blocks = new List<BlockInstanceData>();
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// ブロックのインスタンスデータ構造
    /// </summary>
    [System.Serializable]
    public class BlockInstanceData
    {
        public Vector3Int position;          // ブロックの論理座標
        public Vector3 worldPosition;       // ワールド座標
        public Block block;                 // 実際のBlockへの参照
        public bool isActive = true;        // ブロックがアクティブかどうか
        public int voxelCount;              // このブロック内のボクセル数
        
        public BlockInstanceData(Vector3Int pos, Vector3 worldPos)
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
    public BlockInstanceData CreateBlock(Vector3Int blockPos, Vector3 worldPos, bool[,,] pattern, BlockData data, float blockSize, int voxelsPerBlock, Transform parent)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Creating block at {blockPos} with type {data.resourceType}");
        }
        
        // ブロックデータを作成
        BlockInstanceData blockInstance = new BlockInstanceData(blockPos, worldPos);
        
        // GameObjectとBlockを作成
        GameObject blockObj = new GameObject($"Block_{data.resourceType}_{blockPos.x}_{blockPos.y}");
        blockObj.transform.parent = parent;
        blockObj.transform.position = worldPos;
        
        // スケール調整
        float scale = blockSize / voxelsPerBlock;
        blockObj.transform.localScale = new Vector3(scale, scale, scale);
        
        // Blockコンポーネントを追加
        Block block = blockObj.AddComponent<Block>();
        blockInstance.block = block;
        
        // マテリアル設定
        CreateBlockMaterial(blockObj, data);
        
        // Blockを初期化
        block.Initialize(
            terrainManager.VoxelManager,
            blockPos,
            pattern,
            voxelsPerBlock, // voxelCountPerSide
            blockSize, // worldBlockSize
            data
        );
        
        // ボクセル数を計算
        blockInstance.voxelCount = CalculateVoxelCount(pattern);
        
        // リストに追加
        blocks.Add(blockInstance);

        // 永続化データに基づいてアクティブ状態を設定
        if (GameDataPersistenceManager.Instance.destroyedBlockPositions.Contains(blockPos))
        {
            SetBlockActive(blockPos, false);
        }
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Created block with {blockInstance.voxelCount} voxels");
        }
        
        return blockInstance;
    }
    
    /// <summary>
    /// ブロック用マテリアルを作成
    /// </summary>
    private void CreateBlockMaterial(GameObject blockObj, BlockData data)
    {
        var renderer = blockObj.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = blockObj.AddComponent<MeshRenderer>();
        }
        
        // Custom Unlitマテリアルを作成
        Material mat = new Material(Shader.Find("Custom/UnlitBlock"));
        
        // BlockDataにテクスチャが設定されていれば使用
        if (data.textures != null && data.textures.Count > 0)
        {
            mat.mainTexture = data.textures[0]; // 最初のテクスチャをデフォルトとして使用
        }
        
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
    public BlockInstanceData GetBlockAt(Vector3Int position)
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
    public List<BlockInstanceData> GetAllBlocks()
    {
        return new List<BlockInstanceData>(blocks);
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
    /// ブロックを破壊する
    /// </summary>
    public void DestroyBlock(Vector3Int position)
    {
        SetBlockActive(position, false);
        GameDataPersistenceManager.Instance.destroyedBlockPositions.Add(position);
    }
    
    /// <summary>
    /// ブロックのアクティブ状態を設定
    /// </summary>
    public void SetBlockActive(Vector3Int position, bool active)
    {
        BlockInstanceData block = GetBlockAt(position);
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
                if (Application.isPlaying)
                {
                    Destroy(block.block.gameObject);
                }
                else
                {
                    DestroyImmediate(block.block.gameObject);
                }
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
