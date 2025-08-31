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
    public int voxelHp = 2;
    
    [Header("Texture Settings")]
    public Texture2D texture1;
    public Texture2D texture2;
    
    [Header("Dropped Item Settings")]
    public GameObject droppedItemPrefab;
    public bool disableRotation = true;
    public bool autoScale = true;
    public float scaleMultiplier = 1.0f;
    
    [Header("Generation Type")]
    public TerrainGenerationType generationType = TerrainGenerationType.SideScroller;
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
    [Header("Terrain Configuration")]
    [SerializeField] private TerrainSettings settings = new TerrainSettings();
    
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
        GenerateTerrain();
    }

    void Update()
    {
        // UIテキストが設定されていれば、未回収のアイテム数を表示
        if (voxelCountText != null)
        {
            int droppedItemCount = GameObject.FindGameObjectsWithTag("DroppedItem").Length;
            voxelCountText.text = $"Dropped Items: {droppedItemCount}";
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
        
        ClearExistingTerrain();
        GenerateWorld();
    }
    
    /// <summary>
    /// 既存の地形をクリア
    /// </summary>
    private void ClearExistingTerrain()
    {
        blockManager?.ClearAllBlocks();
        voxelManager?.ClearAllVoxels();
    }
    
    /// <summary>
    /// ワールド全体を生成 (チャンクの生成ループ)
    /// </summary>
    private void GenerateWorld()
    {
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Generating world...");
        }

        transform.position = Vector3.zero;

        for (int cx = 0; cx < settings.worldSizeInChunks.x; cx++)
        {
            for (int cy = 0; cy < settings.worldSizeInChunks.y; cy++)
            {
                Vector3Int chunkPos = new Vector3Int(cx, cy, 0);
                GenerateChunk(chunkPos);
            }
        }
    }

    /// <summary>
    /// 1つのチャンクを生成 (ブロックの生成ループ)
    /// </summary>
    private void GenerateChunk(Vector3Int chunkPos)
    {
        // チャンクGameObjectを作成
        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
        chunkObj.transform.parent = transform;
        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.Initialize(chunkPos);
        
        // チャンクのワールド座標オフセットを計算
        float chunkOffsetX = chunkPos.x * settings.chunkSizeInBlocks.x * settings.blockSize;
        float chunkOffsetY = chunkPos.y * settings.chunkSizeInBlocks.y * settings.blockSize;

        for (int bx = 0; bx < settings.chunkSizeInBlocks.x; bx++)
        {
            for (int by = 0; by < settings.chunkSizeInBlocks.y; by++)
            {
                Vector3Int blockPos = new Vector3Int(
                    chunkPos.x * settings.chunkSizeInBlocks.x + bx,
                    chunkPos.y * settings.chunkSizeInBlocks.y + by,
                    0
                );

                // ワールド全体の幅を計算し、中心を0にするためのオフセットを算出
                float totalBlocksX = settings.worldSizeInChunks.x * settings.chunkSizeInBlocks.x;
                float worldWidth = totalBlocksX * settings.blockSize;
                float offsetX = -worldWidth / 2f + settings.blockSize / 2f; // ブロック半個分をオフセットに追加

                // ワールド座標を計算
                float worldX = settings.center.x + offsetX + (chunkPos.x * settings.chunkSizeInBlocks.x + bx) * settings.blockSize;
                // 最も浅いブロックの中心が-blockSize/2になるように調整
                float worldY = settings.center.y - (chunkPos.y * settings.chunkSizeInBlocks.y + by) * settings.blockSize - (settings.blockSize / 2f);
                Vector3 worldPos = new Vector3(worldX, worldY, settings.center.z);

                // BlockGeneratorでパターンを生成
                var blockData = new BlockGenerator.BlockGenerationData(
                    settings.generationType,
                    settings.voxelSize,
                    settings.blockSize,
                    blockPos
                );
                bool[,,] pattern = blockGenerator.GenerateBlockPattern(blockData);

                // BlockManagerでブロックを作成
                var newBlockData = blockManager.CreateBlock(blockPos, worldPos, pattern, settings, chunkObj.transform);

                // VoxelManagerにボクセルデータを登録
                voxelManager.RegisterVoxelsFromPattern(pattern, blockPos, worldPos, settings);

                // ボクセルデータ登録後にメッシュを生成
                if (newBlockData != null && newBlockData.block != null)
                {
                    newBlockData.block.GenerateMesh();
                }
            }
        }
    }

    /// <summary>
    /// 既存の全チャンクを削除
    /// </summary>
    public void ClearTerrain()
    {
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
        settings.voxelHp = Mathf.Max(1, settings.voxelHp);
        settings.scaleMultiplier = Mathf.Max(0.1f, settings.scaleMultiplier);
    }
#endif
}
