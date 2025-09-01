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
    public Vector2Int worldSizeInChunks = new Vector2Int(10, 1); // ワールドのチャンク数
    public Vector2Int chunkSizeInBlocks = new Vector2Int(1, 5); // チャンクあたりのブロック数
    public float blockSize = 1.0f; // ブロックのサイズ
    public int voxelSize = 4;

    [Header("Generation Type")]
    public TerrainGenerationType generationType = TerrainGenerationType.SideScroller;
    
    [Header("Performance")]
    public int blocksPerFrame = 16; // 1フレームあたりのブロック生成数
    public float sortInterval = 0.5f; // 生成キューをソートする間隔
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
    [SerializeField] private BlockDataManager blockDataManager;

    [Header("Terrain Configuration")]
    [SerializeField] private TerrainSettings settings = new TerrainSettings();

    [Header("Dynamic Generation")]
    [SerializeField] private Transform playerTransform; // プレイヤーのTransform
    [SerializeField] private int renderDistanceInChunks = 5; // チャンクの描画距離
    private float chunkUpdateInterval = 1.0f; // チャンクの更新間隔
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
    public BlockDataManager BlockDataManager => blockDataManager;
    public BlockManager BlockManager => blockManager;
    public BlockGenerator BlockGenerator => blockGenerator;
    public VoxelManager VoxelManager => voxelManager;

    void Start()
    {
        InitializeHierarchicalSystem();
        StartCoroutine(ProcessBlockGenerationQueue());
        // GenerateTerrain(); // 初期の一括生成をコメントアウト
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
            Debug.Log($"TerrainManager: Generating terrain with type {settings.generationType}");
        }
        
        ClearTerrain();
        EnqueueInitialWorldGeneration();
    }
    
    /// <summary>
    /// プレイヤーの周囲のチャンクを更新
    /// </summary>
    private void UpdateChunks()
    {
        Vector3Int playerChunkPos = GetChunkPositionFromWorld(playerTransform.position);

        // プレイヤーがチャンクをまたいだ場合のみ更新
        if (playerChunkPos == currentPlayerChunk && activeChunks.Count > 0) return;
        
        currentPlayerChunk = playerChunkPos;

        List<Vector3Int> newBlockPositions = new List<Vector3Int>();

        for (int x = -renderDistanceInChunks; x <= renderDistanceInChunks; x++)
        {
            for (int y = -renderDistanceInChunks; y <= renderDistanceInChunks; y++)
            {
                Vector3Int chunkPos = new Vector3Int(currentPlayerChunk.x + x, currentPlayerChunk.y + y, 0);

                // 地下(y<=0)のみチャンクを生成
                if (chunkPos.y > 0) continue;
                
                // チャンクがまだ生成されていない場合、そのチャンク内のブロックをキューに追加
                if (!activeChunks.ContainsKey(chunkPos))
                {
                    for (int bx = 0; bx < settings.chunkSizeInBlocks.x; bx++)
                    {
                        for (int by = 0; by < settings.chunkSizeInBlocks.y; by++)
                        {
                            newBlockPositions.Add(new Vector3Int(
                                chunkPos.x * settings.chunkSizeInBlocks.x + bx,
                                chunkPos.y * settings.chunkSizeInBlocks.y + by,
                                0
                            ));
                        }
                    }
                }
            }
        }
        
        // プレイヤー位置に近い順にソート
        Vector3Int playerBlockPos = GetBlockPositionFromWorld(playerTransform.position);
        newBlockPositions = newBlockPositions.OrderBy(pos => (pos - playerBlockPos).sqrMagnitude).ToList();
        
        // リストの先頭に追加して優先度を上げる
        _blockGenerationList.InsertRange(0, newBlockPositions.Where(pos => _processedBlocks.Add(pos)));
        
        // TODO: 将来的には、描画範囲外のチャンクを削除する処理も追加する
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
        float u_x = settings.chunkSizeInBlocks.x;
        float u_y = settings.chunkSizeInBlocks.y;
        float tu_y = t * u_y;

        // チャンク中心からのオフセットを考慮してx座標を計算
        float x_offset = worldPosition.x + (u_x - 1) / 2f * t;
        int chunkX = Mathf.FloorToInt(x_offset / (t * u_x));
        
        // z = tu(2y-1)/2  =>  y = (2z/tu + 1)/2
        float y_float = (2 * worldPosition.y / tu_y + 1) / 2;
        int chunkY = Mathf.FloorToInt(y_float);

        return new Vector3Int(chunkX, chunkY, 0);
    }

    private void EnqueueInitialWorldGeneration()
    {
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Enqueueing initial world generation...");
        }

        transform.position = Vector3.zero;

        // 全ブロック座標をリストアップ
        List<Vector3Int> allBlockPositions = new List<Vector3Int>();
        for (int cx = 0; cx < settings.worldSizeInChunks.x; cx++)
        {
            for (int cy = 0; cy < settings.worldSizeInChunks.y; cy++)
            {
                for (int bx = 0; bx < settings.chunkSizeInBlocks.x; bx++)
                {
                    for (int by = 0; by < settings.chunkSizeInBlocks.y; by++)
                    {
                        allBlockPositions.Add(new Vector3Int(
                            cx * settings.chunkSizeInBlocks.x + bx,
                            cy * settings.chunkSizeInBlocks.y + by,
                            0
                        ));
                    }
                }
            }
        }

        // プレイヤー位置に近い順にソート
        Vector3Int playerBlockPos = GetBlockPositionFromWorld(playerTransform.position);
        allBlockPositions = allBlockPositions.OrderBy(pos => (pos - playerBlockPos).sqrMagnitude).ToList();

        // リストに追加
        foreach (var pos in allBlockPositions)
        {
            if (_processedBlocks.Add(pos))
            {
                _blockGenerationList.Add(pos);
            }
        }
    }

    private IEnumerator ProcessBlockGenerationQueue()
    {
        float timeSinceLastSort = 0f;

        while (true)
        {
            timeSinceLastSort += Time.deltaTime;

            // 一定間隔でプレイヤー位置を基準にソート
            if (timeSinceLastSort >= settings.sortInterval && _blockGenerationList.Count > 0)
            {
                Vector3Int playerBlockPos = GetBlockPositionFromWorld(playerTransform.position);
                _blockGenerationList.Sort((a, b) => (a - playerBlockPos).sqrMagnitude.CompareTo((b - playerBlockPos).sqrMagnitude));
                timeSinceLastSort = 0f;
            }
            
            int blocksToProcess = Mathf.Min(_blockGenerationList.Count, settings.blocksPerFrame);
            for (int i = 0; i < blocksToProcess; i++)
            {
                if (_blockGenerationList.Count > 0)
                {
                    Vector3Int blockPos = _blockGenerationList[0];
                    _blockGenerationList.RemoveAt(0);
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
        float u_x = settings.chunkSizeInBlocks.x;
        float u_y = settings.chunkSizeInBlocks.y;
        
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

        // TODO: 将来的にはBlockGeneratorがResourceTypeを決定するようにする
        ResourceType currentResourceType = ResourceType.Stone;
        BlockData blockTypeData = blockDataManager.GetBlockData(currentResourceType);

        if (blockTypeData == null)
        {
            Debug.LogError($"BlockData for {currentResourceType} is not assigned in BlockDataManager.");
            return;
        }

        // BlockGeneratorでパターンを生成
        var generationData = new BlockGenerator.BlockGenerationData(
            settings.generationType,
            settings.voxelSize,
            settings.blockSize,
            blockPos
        );
        bool[,,] pattern = blockGenerator.GenerateBlockPattern(generationData);

        // BlockManagerでブロックを作成
        var newBlockInstance = blockManager.CreateBlock(blockPos, worldPos, pattern, currentResourceType, blockTypeData, settings.blockSize, settings.voxelSize, chunk.transform);

        // VoxelManagerにボクセルデータを登録
        voxelManager.RegisterVoxelsFromPattern(pattern, blockPos, worldPos, blockTypeData, settings.blockSize, settings.voxelSize);

        // ボクセルデータ登録後にメッシュを生成
        if (newBlockInstance != null && newBlockInstance.block != null)
        {
            newBlockInstance.block.GenerateMesh();
        }
    }

    private Chunk GetOrCreateChunk(Vector3Int blockPos)
    {
        Vector3Int chunkPos = new Vector3Int(
            Mathf.FloorToInt((float)blockPos.x / settings.chunkSizeInBlocks.x),
            Mathf.FloorToInt((float)blockPos.y / settings.chunkSizeInBlocks.y),
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
        Debug.Log($"World Size (Chunks): {settings.worldSizeInChunks}");
        Debug.Log($"Chunk Size (Blocks): {settings.chunkSizeInBlocks}");
        
        if (blockManager != null && voxelManager != null && blockGenerator != null)
        {
            Debug.Log(blockManager.GetDebugInfo());
            Debug.Log(voxelManager.GetDebugInfo());
            Debug.Log(blockGenerator.GetDebugInfo());
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // エディタでの値変更時に設定を検証
        settings.worldSizeInChunks.x = Mathf.Max(1, settings.worldSizeInChunks.x);
        settings.worldSizeInChunks.y = Mathf.Max(1, settings.worldSizeInChunks.y);
        settings.chunkSizeInBlocks.x = Mathf.Max(1, settings.chunkSizeInBlocks.x);
        settings.chunkSizeInBlocks.y = Mathf.Max(1, settings.chunkSizeInBlocks.y);
        settings.blockSize = Mathf.Max(0.1f, settings.blockSize);
        settings.voxelSize = Mathf.Max(1, settings.voxelSize);
    }
#endif
}
