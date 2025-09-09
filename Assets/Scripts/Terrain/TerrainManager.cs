using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI; // UIコンポーネントを使用するために追加

/// <summary>
/// 地形生成タイプ列挙型
/// </summary>
public enum TerrainGenerationType
{
    SideScroller,    // XY平面（旧CubeSideScrollerPlacer置き換え）
    TopDown,         // XZ平面（旧CubeTopDownPlacer置き換え）
    Custom          // カスタム（将来の拡張用）
}

/// <summary>
/// 地形設定データ構造
/// 旧BaseCubePlacer + CubeSideScrollerPlacerの全設定を統合
/// </summary>
[System.Serializable]
public class TerrainSettings
{
    [Header("Basic Settings")]
    public Vector3Int center = Vector3Int.zero;
    public Vector2Int initialChunkCount = new Vector2Int(2, 5);
    public Vector2Int blocksPerChunk = new Vector2Int(5, 5);
    public float blockSize = 1.0f; // ブロックのサイズ
    public int voxelsPerBlock = 4;

    [Header("Generation Type")]
    public TerrainGenerationType generationType = TerrainGenerationType.SideScroller;
    
    [Header("Performance")]
    public int blocksPerFrame = 16; // 1フレームあたりのブロック生成数
}

/// <summary>
/// 地形全体を管理するマネージャー
/// WorldGeneratorオブジェクトにアタッチして使用
/// 
/// レガシーシステム（BaseCubePlacer、CubeSideScrollerPlacer）を完全置き換え
/// 不必要な継承関係を排除し、Blockを直接使用する統合設計
/// </summary>
public class TerrainManager : MonoBehaviour
{
    [Header("Data Managers")]
    [SerializeField] private BlockData blockData;

    [Header("Terrain Configuration")]
    [SerializeField] private TerrainSettings settings = new TerrainSettings();

    [Header("Dynamic Generation")]
    [SerializeField] private Transform playerTransform; // プレイヤーのTransform
    [SerializeField] private int renderDistanceInChunks = 5; // チャンクの描画距離
    private float chunkUpdateInterval = 0.3f; // チャンクの更新間隔
    private float timeSinceLastChunkUpdate = 0f;
    private Vector3Int currentPlayerChunk;
    private Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    private readonly List<Vector3Int> _blockGenerationList = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> _processedBlocks = new HashSet<Vector3Int>(); // 生成済み or キュー済みのブロック
    
    [Header("Hierarchical Managers")]
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private BlockGenerator blockGenerator;
    [SerializeField] private VoxelManager voxelManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private Text voxelCountText; // ボクセル数を表示するUIテキスト

    /// <summary>
    /// 地形設定の取得
    /// </summary>
    public TerrainSettings Settings => settings;
    
    /// <summary>
    /// 階層マネージャーへのアクセス
    /// </summary>
    public BlockManager BlockManager => blockManager;
    public BlockGenerator BlockGenerator => blockGenerator;
    public VoxelManager VoxelManager => voxelManager;

    void Start()
    {
        InitializeHierarchicalSystem();
        StartCoroutine(ProcessBlockGenerationQueue());
        GenerateTerrain(); // 初期の一括生成をコメントアウト
    }

    void Update()
    {
        // UIテキストが設定されていれば、未回収のアイテム数を表示
        if (voxelCountText != null)
        {
            int droppedItemCount = GameObject.FindGameObjectsWithTag("DroppedItem").Length;
            voxelCountText.text = $"Dropped Items: {droppedItemCount}";
        }

        if (playerTransform == null) return;

        timeSinceLastChunkUpdate += Time.deltaTime;
        if (timeSinceLastChunkUpdate >= chunkUpdateInterval)
        {
            UpdateChunks();
            timeSinceLastChunkUpdate = 0f;
        }
    }
    
    /// <summary>
    /// 階層システムを初期化
    /// </summary>
    private void InitializeHierarchicalSystem()
    {
        // 階層マネージャーを自動作成または取得
        if (blockManager == null)
        {
            GameObject blockManagerObj = new GameObject("BlockManager");
            blockManagerObj.transform.parent = transform;
            blockManager = blockManagerObj.AddComponent<BlockManager>();
        }
        
        if (blockGenerator == null)
        {
            GameObject blockGeneratorObj = new GameObject("BlockGenerator");
            blockGeneratorObj.transform.parent = transform;
            blockGenerator = blockGeneratorObj.AddComponent<BlockGenerator>();
        }
        
        if (voxelManager == null)
        {
            GameObject voxelManagerObj = new GameObject("VoxelManager");
            voxelManagerObj.transform.parent = transform;
            voxelManager = voxelManagerObj.AddComponent<VoxelManager>();
        }
        
        // 各マネージャーを初期化
        blockManager.Initialize(this);
        blockGenerator.Initialize(this);
        voxelManager.Initialize(this);
        
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Hierarchical system initialized");
        }
    }

    /// <summary>
    /// 地形を生成
    /// </summary>
    public void GenerateTerrain()
    {
        if (showDebugInfo)
        {
            Debug.Log($"TerrainManager: Generating initial terrain with type {settings.generationType}");
        }
        
        ClearTerrain();
        
        Vector3Int playerChunkPos = GetChunkPositionFromWorld(playerTransform.position);
        currentPlayerChunk = playerChunkPos;
        Vector3 playerWorldPos = playerTransform.position;

        // プレイヤーから近いチャンク順にソート
        List<Vector3Int> sortedChunks = new List<Vector3Int>();
        
        for (int x = -renderDistanceInChunks; x <= renderDistanceInChunks; x++)
        {
            for (int y = -renderDistanceInChunks; y <= renderDistanceInChunks; y++)
            {
                Vector3Int chunkPos = new Vector3Int(currentPlayerChunk.x + x, currentPlayerChunk.y + y, 0);
                
                // 地下(y<=0)のみチャンクを生成
                if (chunkPos.y > 0) continue;
                
                sortedChunks.Add(chunkPos);
            }
        }
        
        // チャンクをプレイヤーからの距離でソート
        sortedChunks.Sort((a, b) => 
        {
            Vector3 aCenterWorld = GetChunkCenterWorldPosition(a);
            Vector3 bCenterWorld = GetChunkCenterWorldPosition(b);
            float distA = Vector3.Distance(playerWorldPos, aCenterWorld);
            float distB = Vector3.Distance(playerWorldPos, bCenterWorld);
            return distA.CompareTo(distB);
        });

        // 距離順でソートされた各ブロックを生成キューに追加
        foreach (var chunkPos in sortedChunks)
        {
            if (!activeChunks.ContainsKey(chunkPos))
            {
                List<Vector3Int> chunkBlocks = new List<Vector3Int>();
                
                for (int bx = 0; bx < settings.blocksPerChunk.x; bx++)
                {
                    for (int by = 0; by < settings.blocksPerChunk.y; by++)
                    {
                        Vector3Int blockPos = new Vector3Int(
                            chunkPos.x * settings.blocksPerChunk.x + bx,
                            chunkPos.y * settings.blocksPerChunk.y + by,
                            0
                        );
                        chunkBlocks.Add(blockPos);
                    }
                }
                
                // チャンク内のブロックもプレイヤーからの距離でソート
                chunkBlocks.Sort((a, b) => 
                {
                    Vector3 aWorldPos = GetBlockWorldPosition(a);
                    Vector3 bWorldPos = GetBlockWorldPosition(b);
                    float distA = Vector3.Distance(playerWorldPos, aWorldPos);
                    float distB = Vector3.Distance(playerWorldPos, bWorldPos);
                    return distA.CompareTo(distB);
                });
                
                // 距離順でキューに追加
                foreach (var blockPos in chunkBlocks)
                {
                    if (_processedBlocks.Add(blockPos))
                    {
                        _blockGenerationList.Add(blockPos);
                    }
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[GenerateTerrain] Queued {_blockGenerationList.Count} blocks in distance order.");
            if (_blockGenerationList.Count > 0)
            {
                for (int i = 0; i < Mathf.Min(10, _blockGenerationList.Count); i++)
                {
                    Vector3 blockWorldPos = GetBlockWorldPosition(_blockGenerationList[i]);
                    float distance = Vector3.Distance(playerWorldPos, blockWorldPos);
                    Debug.Log($"[GenerateTerrain] Block #{i}: {_blockGenerationList[i]} at world {blockWorldPos} (distance: {distance:F2})");
                }
            }
        }
    }

    /// <summary>
    /// プレイヤーの周囲のチャンクを更新
    /// </summary>
    private void UpdateChunks()
    {
        Vector3Int newPlayerChunkPos = GetChunkPositionFromWorld(playerTransform.position);

        // プレイヤーがチャンクをまたいだ場合のみ更新
        if (newPlayerChunkPos == currentPlayerChunk && activeChunks.Count > 0) return;
        
        currentPlayerChunk = newPlayerChunkPos;
        Vector3 playerWorldPos = playerTransform.position;

        // 同様に距離順でソートして追加
        List<Vector3Int> chunksToGenerate = new List<Vector3Int>();
        
        for (int x = -renderDistanceInChunks; x <= renderDistanceInChunks; x++)
        {
            for (int y = -renderDistanceInChunks; y <= renderDistanceInChunks; y++)
            {
                Vector3Int chunkPos = new Vector3Int(currentPlayerChunk.x + x, currentPlayerChunk.y + y, 0);
                
                if (chunkPos.y > 0) continue;
                if (activeChunks.ContainsKey(chunkPos)) continue;
                
                chunksToGenerate.Add(chunkPos);
            }
        }
        
        // 距離順でソート
        chunksToGenerate.Sort((a, b) => 
        {
            Vector3 aCenterWorld = GetChunkCenterWorldPosition(a);
            Vector3 bCenterWorld = GetChunkCenterWorldPosition(b);
            float distA = Vector3.Distance(playerWorldPos, aCenterWorld);
            float distB = Vector3.Distance(playerWorldPos, bCenterWorld);
            return distA.CompareTo(distB);
        });
        
        // 各チャンクのブロックを距離順で追加
        foreach (var chunkPos in chunksToGenerate)
        {
            List<Vector3Int> blocksInChunk = new List<Vector3Int>();
            
            for (int bx = 0; bx < settings.blocksPerChunk.x; bx++)
            {
                for (int by = 0; by < settings.blocksPerChunk.y; by++)
                {
                    Vector3Int blockPos = new Vector3Int(
                        chunkPos.x * settings.blocksPerChunk.x + bx,
                        chunkPos.y * settings.blocksPerChunk.y + by,
                        0
                    );
                    blocksInChunk.Add(blockPos);
                }
            }
            
            blocksInChunk.Sort((a, b) => 
            {
                Vector3 aWorldPos = GetBlockWorldPosition(a);
                Vector3 bWorldPos = GetBlockWorldPosition(b);
                float distA = Vector3.Distance(playerWorldPos, aWorldPos);
                float distB = Vector3.Distance(playerWorldPos, bWorldPos);
                return distA.CompareTo(distB);
            });
            
            foreach (var blockPos in blocksInChunk)
            {
                if (_processedBlocks.Add(blockPos))
                {
                    _blockGenerationList.Add(blockPos);
                }
            }
        }
    }

    /// <summary>
    /// ワールド座標からチャンク座標を取得
    /// </summary>
    private Vector3Int GetBlockPositionFromWorld(Vector3 worldPosition)
    {
        // このメソッドはチャンク座標ではなく、最も近いブロックの整数座標を返すようにする
        // worldPositionをblockSizeで割り、最も近い整数に丸める
        int x = Mathf.RoundToInt(worldPosition.x / settings.blockSize);
        int y = Mathf.RoundToInt(worldPosition.y / settings.blockSize);
        int z = Mathf.RoundToInt(worldPosition.z / settings.blockSize);
        return new Vector3Int(x, y, z);
    }

    /// <summary>
    /// ワールド座標からチャンク座標を取得
    /// </summary>
    private Vector3Int GetChunkPositionFromWorld(Vector3 worldPosition)
    {
        float t = settings.blockSize;
        float u_x = settings.blocksPerChunk.x;
        float u_y = settings.blocksPerChunk.y;
        float tu_y = t * u_y;

        // チャンク中心からのオフセットを考慮してx座標を計算
        float x_offset = worldPosition.x + (u_x - 1) / 2f * t;
        int chunkX = Mathf.FloorToInt(x_offset / (t * u_x));
        
        // z = tu(2y-1)/2  =>  y = (2z/tu + 1)/2
        float y_float = (2 * worldPosition.y / tu_y + 1) / 2;
        int chunkY = Mathf.FloorToInt(y_float);

        return new Vector3Int(chunkX, chunkY, 0);
    }


    private IEnumerator ProcessBlockGenerationQueue()
    {
        while (true)
        {
            // キューは既に距離順でソートされているのでソート処理を削除
            
            int blocksToProcess = Mathf.Min(_blockGenerationList.Count, settings.blocksPerFrame);
            for (int i = 0; i < blocksToProcess; i++)
            {
                if (_blockGenerationList.Count > 0)
                {
                    Vector3Int blockPos = _blockGenerationList[0];
                    _blockGenerationList.RemoveAt(0);
                    
                    if (showDebugInfo)
                    {
                        Vector3 blockWorldPos = GetBlockWorldPosition(blockPos);
                        float distance = Vector3.Distance(playerTransform.position, blockWorldPos);
                        Debug.Log($"[ProcessQueue] Generating block {blockPos} at world {blockWorldPos} (distance: {distance:F2})");
                    }
                    
                    GenerateSingleBlock(blockPos);
                }
            }
            yield return null;
        }
    }

    private void GenerateSingleBlock(Vector3Int blockPos)
    {
        Chunk chunk = GetOrCreateChunk(blockPos);

        // w=tux, z = tu(2y-1)/2
        float t = settings.blockSize;
        float u_x = settings.blocksPerChunk.x;
        float u_y = settings.blocksPerChunk.y;
        
        int chunkCoordX = chunk.chunkPosition.x;
        int chunkCoordY = chunk.chunkPosition.y;
        
        int bx = blockPos.x - chunkCoordX * (int)u_x;
        int by = blockPos.y - chunkCoordY * (int)u_y;

        // チャンクの中心がx=0になるようにオフセットを調整
        float chunkCenterX = chunkCoordX * u_x * t;
        float relativeBlockX = (bx - (u_x - 1) / 2f) * t;
        float worldX = chunkCenterX + relativeBlockX;

        float chunkCenterY = t * u_y * (2 * chunkCoordY - 1) / 2f;
        // チャンク中心からの相対座標を計算
        float relativeBlockY = (by - (u_y - 1) / 2f) * t;
        float worldY = chunkCenterY + relativeBlockY;

        Vector3 worldPos = new Vector3(worldX, worldY, settings.center.z);

        // BlockDataManager を廃止し、直接 BlockData を使用する
        BlockData blockTypeData = blockData;

        if (blockTypeData == null)
        {
            Debug.LogError("BlockData is not assigned in TerrainManager.");
            return;
        }

        // BlockGeneratorでパターンを生成
        var generationData = new BlockGenerator.BlockGenerationData(
            settings.generationType,
            settings.voxelsPerBlock,
            settings.blockSize,
            blockPos
        );
        bool[,,] pattern = blockGenerator.GenerateBlockPattern(generationData);

        // BlockManagerでブロックを作成
        var newBlockInstance = blockManager.CreateBlock(blockPos, worldPos, pattern, blockTypeData, settings.blockSize, settings.voxelsPerBlock, chunk.transform);

        // VoxelManagerにボクセルデータを登録
        voxelManager.RegisterVoxelsFromPattern(pattern, blockPos, worldPos, blockTypeData, settings.blockSize, settings.voxelsPerBlock);

        // ボクセルデータ登録後にメッシュを生成
        if (newBlockInstance != null && newBlockInstance.block != null)
        {
            newBlockInstance.block.GenerateMesh();
        }
    }

    private Chunk GetOrCreateChunk(Vector3Int blockPos)
    {
        Vector3Int chunkPos = new Vector3Int(
            Mathf.FloorToInt((float)blockPos.x / settings.blocksPerChunk.x),
            Mathf.FloorToInt((float)blockPos.y / settings.blocksPerChunk.y),
            0
        );

        if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            return chunk;
        }

        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
        chunkObj.transform.parent = transform;
        Chunk newChunk = chunkObj.AddComponent<Chunk>();
        newChunk.Initialize(chunkPos);
        activeChunks.Add(chunkPos, newChunk);
        return newChunk;
    }

    /// <summary>
    /// 既存の全チャンクを削除
    /// </summary>
    public void ClearTerrain()
    {
        StopAllCoroutines();
        StartCoroutine(ProcessBlockGenerationQueue()); // StopAllCoroutinesで止まるので再開
        
        // 子オブジェクトを全て削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
        
        activeChunks.Clear();
        _blockGenerationList.Clear();
        _processedBlocks.Clear();
        blockManager?.ClearAllBlocks();
        voxelManager?.ClearAllVoxels();
    }

    /// <summary>
    /// 地形を再生成
    /// </summary>
    [ContextMenu("Regenerate Terrain")]
    public void RegenerateTerrain()
    {
        ClearTerrain();
        GenerateTerrain();
    }
    
    [ContextMenu("Show Debug Info")]
    public void ShowDebugInfo()
    {
        Debug.Log("=== TerrainManager Debug Info ===");
        Debug.Log($"Generation Type: {settings.generationType}");
        Debug.Log($"Initial Chunk Count: {settings.initialChunkCount}");
        Debug.Log($"Blocks Per Chunk: {settings.blocksPerChunk}");
        
        if (blockManager != null && voxelManager != null && blockGenerator != null)
        {
            Debug.Log(blockManager.GetDebugInfo());
            Debug.Log(voxelManager.GetDebugInfo());
            Debug.Log(blockGenerator.GetDebugInfo());
        }
    }

    /// <summary>
    /// チャンクの中心ワールド座標を取得
    /// </summary>
    private Vector3 GetChunkCenterWorldPosition(Vector3Int chunkPos)
    {
        float t = settings.blockSize;
        float u_x = settings.blocksPerChunk.x;
        float u_y = settings.blocksPerChunk.y;
        
        float centerX = chunkPos.x * u_x * t;
        float centerY = t * u_y * (2 * chunkPos.y - 1) / 2f;
        
        return new Vector3(centerX, centerY, settings.center.z);
    }

    /// <summary>
    /// ブロックのワールド座標を取得
    /// </summary>
    private Vector3 GetBlockWorldPosition(Vector3Int blockPos)
    {
        Vector3Int chunkPos = new Vector3Int(
            Mathf.FloorToInt((float)blockPos.x / settings.blocksPerChunk.x),
            Mathf.FloorToInt((float)blockPos.y / settings.blocksPerChunk.y),
            0
        );
        
        float t = settings.blockSize;
        float u_x = settings.blocksPerChunk.x;
        float u_y = settings.blocksPerChunk.y;
        
        int bx = blockPos.x - chunkPos.x * (int)u_x;
        int by = blockPos.y - chunkPos.y * (int)u_y;

        float chunkCenterX = chunkPos.x * u_x * t;
        float relativeBlockX = (bx - (u_x - 1) / 2f) * t;
        float worldX = chunkCenterX + relativeBlockX;

        float chunkCenterY = t * u_y * (2 * chunkPos.y - 1) / 2f;
        float relativeBlockY = (by - (u_y - 1) / 2f) * t;
        float worldY = chunkCenterY + relativeBlockY;

        return new Vector3(worldX, worldY, settings.center.z);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // エディタでの値変更時に設定を検証
        settings.initialChunkCount.x = Mathf.Max(1, settings.initialChunkCount.x);
        settings.initialChunkCount.y = Mathf.Max(1, settings.initialChunkCount.y);
        settings.blocksPerChunk.x = Mathf.Max(1, settings.blocksPerChunk.x);
        settings.blocksPerChunk.y = Mathf.Max(1, settings.blocksPerChunk.y);
        settings.blockSize = Mathf.Max(0.1f, settings.blockSize);
        settings.voxelsPerBlock = Mathf.Max(1, settings.voxelsPerBlock);
    }
#endif
}
