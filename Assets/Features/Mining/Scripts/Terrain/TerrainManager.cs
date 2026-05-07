using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI; // UIコンポ�Eネントを使用するために追加

/// <summary>
/// 地形生�Eタイプ�E挙型
/// </summary>
public enum TerrainGenerationType
{
    SideScroller,    // XY平面�E�旧CubeSideScrollerPlacer置き換え！E
    TopDown,         // XZ平面�E�旧CubeTopDownPlacer置き換え！E
    Custom          // カスタム�E�封E��の拡張用�E�E
}

/// <summary>
/// 地形設定データ構造
/// 旧BaseCubePlacer + CubeSideScrollerPlacerの全設定を統吁E
/// </summary>
[System.Serializable]
public class TerrainSettings
{
    [Header("Basic Settings")]
    public Vector3Int center = Vector3Int.zero;
    public int seed;
    public bool useRandomSeed = true;
    public Vector2Int initialChunkCount = new Vector2Int(2, 5);
    public Vector2Int blocksPerChunk = new Vector2Int(5, 5);
    public float blockSize = 1.0f; // ブロチE��のサイズ
    public int voxelsPerBlock = 4;

    [Header("Generation Type")]
    public TerrainGenerationType generationType = TerrainGenerationType.SideScroller;
    
    [Header("Performance")]
    public int blocksPerFrame = 16; // 1フレームあたり�EブロチE��生�E数
    
    [Header("Item Loading")]
    public float itemLoadDelay = 0.1f; // チャンク生�E後�EアイチE��ロード遅延
}

/// <summary>
/// 地形全体を管琁E��る�Eネ�Eジャー
/// WorldGeneratorオブジェクトにアタチE��して使用
/// 
/// レガシーシスチE���E�EaseCubePlacer、CubeSideScrollerPlacer�E�を完�E置き換ぁE
/// 不忁E��な継承関係を排除し、Blockを直接使用する統合設訁E
/// </summary>
public class TerrainManager : MonoBehaviour
{
    [Header("Data Managers")]
    public TerrainDataManager terrainDataManager;

    [Header("Terrain Configuration")]
    [SerializeField] private TerrainSettings settings = new TerrainSettings();

    [Header("Dynamic Generation")]
    public Transform playerTransform; // プレイヤーのTransform
    
    [Header("Hierarchical Managers")]
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private BlockGenerator blockGenerator;
    [SerializeField] private VoxelManager voxelManager;
    [SerializeField] private FluidManager fluidManager;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    [SerializeField] private Text voxelCountText; // ボクセル数を表示するUIチE��スチE

    /// <summary>
    /// 地形設定�E取征E
    /// </summary>
    public TerrainSettings Settings => settings;
    
    /// <summary>
    /// 階層マネージャーへのアクセス
    /// </summary>
    public ChunkManager ChunkManager => chunkManager;
    public BlockManager BlockManager => blockManager;
    public BlockGenerator BlockGenerator => blockGenerator;
    public VoxelManager VoxelManager => voxelManager;
    public FluidManager FluidManager => fluidManager;
    public TerrainDataManager TerrainDataManager => terrainDataManager;

    void Awake()
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (!persistenceManager.hasInitializedSeed)
        {
            if (settings.useRandomSeed)
            {
                persistenceManager.terrainSeed = Random.Range(int.MinValue, int.MaxValue);
            }
            else
            {
                persistenceManager.terrainSeed = settings.seed;
            }
            persistenceManager.hasInitializedSeed = true;
        }
        
        settings.seed = persistenceManager.terrainSeed;
        
        InitializeHierarchicalSystem();
    }

    void Update()
    {
        // UIチE��ストが設定されてぁE��ば、未回収のアイチE��数を表示
        if (voxelCountText != null)
        {
            int droppedItemCount = GameObject.FindGameObjectsWithTag("DroppedItem").Length;
            voxelCountText.text = $"Dropped Items: {droppedItemCount}";
        }
    }
    
    /// <summary>
    /// 階層シスチE��を�E期化
    /// </summary>
    private void InitializeHierarchicalSystem()
    {
        // 階層マネージャーがインスペクターから設定されてぁE��か検証
        if (chunkManager == null || blockManager == null || blockGenerator == null || voxelManager == null)
        {
            Debug.LogError("TerrainManager: One or more hierarchical managers are not assigned in the Inspector.");
            // 重要なコンポ�Eネントが不足してぁE��ため、ここで処琁E��中断
            // this.enabled = false; // コンポ�Eネントを無効化するなどの対策も老E��られめE
            return;
        }
        
        // 吁E�Eネ�Eジャーを�E期化
        chunkManager.Initialize(this);
        blockManager.Initialize(this);
        blockGenerator.Initialize(this, settings.seed);
        voxelManager.Initialize(this);
        fluidManager?.Initialize(this);
        
        // TerrainDataManagerを�E期化
        terrainDataManager?.Initialize();
        
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Hierarchical system initialized");
        }
    }

    /// <summary>
    /// 既存�E全地形チE�Eタを削除
    /// </summary>
    public void ClearTerrain()
    {
        chunkManager?.ClearChunks();
        blockManager?.ClearAllBlocks();
        voxelManager?.ClearAllVoxels();
        fluidManager?.ClearFluid();
    }

    /// <summary>
    /// 地形を�E生�E
    /// </summary>
    [ContextMenu("Regenerate Terrain")]
    public void RegenerateTerrain()
    {
        ClearTerrain();
        chunkManager.GenerateTerrain();
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


#if UNITY_EDITOR
    private void OnValidate()
    {
        // エチE��タでの値変更時に設定を検証
        settings.initialChunkCount.x = Mathf.Max(1, settings.initialChunkCount.x);
        settings.initialChunkCount.y = Mathf.Max(1, settings.initialChunkCount.y);
        settings.blocksPerChunk.x = Mathf.Max(1, settings.blocksPerChunk.x);
        settings.blocksPerChunk.y = Mathf.Max(1, settings.blocksPerChunk.y);
        settings.blockSize = Mathf.Max(0.1f, settings.blockSize);
        settings.voxelsPerBlock = Mathf.Max(1, settings.voxelsPerBlock);
    }
#endif
}




