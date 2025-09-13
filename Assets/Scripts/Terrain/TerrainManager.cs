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
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    [SerializeField] private Text voxelCountText; // ボクセル数を表示するUIテキスト

    /// <summary>
    /// 地形設定の取得
    /// </summary>
    public TerrainSettings Settings => settings;
    
    /// <summary>
    /// 階層マネージャーへのアクセス
    /// </summary>
    public ChunkManager ChunkManager => chunkManager;
    public BlockManager BlockManager => blockManager;
    public BlockGenerator BlockGenerator => blockGenerator;
    public VoxelManager VoxelManager => voxelManager;
    public TerrainDataManager TerrainDataManager => terrainDataManager;

    void Awake()
    {
        InitializeHierarchicalSystem();
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
        // 階層マネージャーがインスペクターから設定されているか検証
        if (chunkManager == null || blockManager == null || blockGenerator == null || voxelManager == null)
        {
            Debug.LogError("TerrainManager: One or more hierarchical managers are not assigned in the Inspector.");
            // 重要なコンポーネントが不足しているため、ここで処理を中断
            // this.enabled = false; // コンポーネントを無効化するなどの対策も考えられる
            return;
        }
        
        // 各マネージャーを初期化
        chunkManager.Initialize(this);
        blockManager.Initialize(this);
        blockGenerator.Initialize(this);
        voxelManager.Initialize(this);
        
        // TerrainDataManagerを初期化
        terrainDataManager?.Initialize();
        
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Hierarchical system initialized");
        }
    }

    /// <summary>
    /// 既存の全地形データを削除
    /// </summary>
    public void ClearTerrain()
    {
        chunkManager?.ClearChunks();
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
