using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// チャンク管理クラス
/// 地形のチャンクレベルでの管理を担当
/// </summary>
public class ChunkManager : MonoBehaviour
{
    [Header("Chunk Configuration")]
    [SerializeField] private List<ChunkData> chunks = new List<ChunkData>();
    [SerializeField] private bool showChunkDebugInfo = false;
    
    /// <summary>
    /// チャンクデータ構造
    /// </summary>
    [System.Serializable]
    public class ChunkData
    {
        public Vector3Int position;          // チャンクの論理座標
        public Vector3 worldPosition;       // ワールド座標
        public Block block;                 // 実際のBlockへの参照
        public bool isActive = true;        // チャンクがアクティブかどうか
        public int blockCount;              // このチャンク内のブロック数
        
        public ChunkData(Vector3Int pos, Vector3 worldPos)
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
        
        if (showChunkDebugInfo)
        {
            Debug.Log($"ChunkManager: Initialized with TerrainManager");
        }
    }
    
    /// <summary>
    /// チャンクを作成
    /// </summary>
    public ChunkData CreateChunk(Vector3Int chunkPos, Vector3 worldPos, bool[,,] pattern, TerrainSettings settings)
    {
        if (showChunkDebugInfo)
        {
            Debug.Log($"ChunkManager: Creating chunk at {chunkPos}");
        }
        
        // チャンクデータを作成
        ChunkData chunkData = new ChunkData(chunkPos, worldPos);
        
        // GameObjectとBlockを作成
        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}");
        chunkObj.transform.parent = transform;
        chunkObj.transform.position = worldPos;
        
        // スケール調整
        float scale = settings.chunkSize / settings.voxelSize;
        chunkObj.transform.localScale = new Vector3(scale, scale, scale);
        
        // Blockコンポーネントを追加
        Block block = chunkObj.AddComponent<Block>();
        chunkData.block = block;
        
        // マテリアル設定
        CreateChunkMaterial(chunkObj, settings);
        
        // Blockを初期化
        block.Initialize(
            pattern, 
            settings.voxelSize, 
            settings.chunkSize, 
            settings.voxelHp,
            settings.droppedItemPrefab,
            settings.disableRotation,
            settings.autoScale,
            settings.scaleMultiplier,
            settings.texture1,
            settings.texture2
        );
        
        // ブロック数を計算
        chunkData.blockCount = CalculateBlockCount(pattern);
        
        // リストに追加
        chunks.Add(chunkData);
        
        if (showChunkDebugInfo)
        {
            Debug.Log($"ChunkManager: Created chunk with {chunkData.blockCount} blocks");
        }
        
        return chunkData;
    }
    
    /// <summary>
    /// チャンク用マテリアルを作成
    /// </summary>
    private void CreateChunkMaterial(GameObject chunkObj, TerrainSettings settings)
    {
        var renderer = chunkObj.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = chunkObj.AddComponent<MeshRenderer>();
        }
        
        // URP Transparentマテリアルを作成
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_AlphaClip", 1); // Alpha Clipping
        mat.mainTexture = settings.texture1; // デフォルトテクスチャ
        
        renderer.material = mat;
    }
    
    /// <summary>
    /// パターンからブロック数を計算
    /// </summary>
    private int CalculateBlockCount(bool[,,] pattern)
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
    /// 指定座標のチャンクを取得
    /// </summary>
    public ChunkData GetChunkAt(Vector3Int position)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.position == position)
            {
                return chunk;
            }
        }
        return null;
    }
    
    /// <summary>
    /// すべてのチャンクを取得
    /// </summary>
    public List<ChunkData> GetAllChunks()
    {
        return new List<ChunkData>(chunks);
    }
    
    /// <summary>
    /// アクティブなチャンクの数を取得
    /// </summary>
    public int GetActiveChunkCount()
    {
        int count = 0;
        foreach (var chunk in chunks)
        {
            if (chunk.isActive) count++;
        }
        return count;
    }
    
    /// <summary>
    /// チャンクのアクティブ状態を設定
    /// </summary>
    public void SetChunkActive(Vector3Int position, bool active)
    {
        ChunkData chunk = GetChunkAt(position);
        if (chunk != null && chunk.block != null)
        {
            chunk.isActive = active;
            chunk.block.gameObject.SetActive(active);
            
            if (showChunkDebugInfo)
            {
                Debug.Log($"ChunkManager: Set chunk {position} active: {active}");
            }
        }
    }
    
    /// <summary>
    /// すべてのチャンクを削除
    /// </summary>
    public void ClearAllChunks()
    {
        if (showChunkDebugInfo)
        {
            Debug.Log($"ChunkManager: Clearing {chunks.Count} chunks");
        }
        
        foreach (var chunk in chunks)
        {
            if (chunk.block != null)
            {
                DestroyImmediate(chunk.block.gameObject);
            }
        }
        
        chunks.Clear();
    }
    
    /// <summary>
    /// デバッグ情報を取得
    /// </summary>
    public string GetDebugInfo()
    {
        int totalBlocks = 0;
        foreach (var chunk in chunks)
        {
            totalBlocks += chunk.blockCount;
        }
        
        return $"ChunkManager - Total: {chunks.Count}, Active: {GetActiveChunkCount()}, Blocks: {totalBlocks}";
    }
    
    void OnDestroy()
    {
        ClearAllChunks();
    }
}
