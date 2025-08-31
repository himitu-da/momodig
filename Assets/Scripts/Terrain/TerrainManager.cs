using UnityEngine;

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
    public Vector2Int chunkCount = new Vector2Int(10, 5);
    public float chunkSize = 1.0f;
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
    [SerializeField] private TerrainRegion terrainRegion;
    [SerializeField] private BlockGenerator blockGenerator;
    [SerializeField] private VoxelManager voxelManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool useHierarchicalSystem = true; // 階層システムを使用するか

    /// <summary>
    /// 地形設定の取得
    /// </summary>
    public TerrainSettings Settings => settings;
    
    /// <summary>
    /// 階層マネージャーへのアクセス
    /// </summary>
    public TerrainRegion TerrainRegion => terrainRegion;
    public BlockGenerator BlockGenerator => blockGenerator;
    public VoxelManager VoxelManager => voxelManager;

    void Start()
    {
        InitializeHierarchicalSystem();
        GenerateTerrain();
    }
    
    /// <summary>
    /// 階層システムを初期化
    /// </summary>
    private void InitializeHierarchicalSystem()
    {
        if (!useHierarchicalSystem)
        {
            if (showDebugInfo)
            {
                Debug.Log("TerrainManager: Using legacy direct Block system");
            }
            return;
        }
        
        // 階層マネージャーを自動作成または取得
        if (terrainRegion == null)
        {
            GameObject terrainRegionObj = new GameObject("TerrainRegion");
            terrainRegionObj.transform.parent = transform;
            terrainRegion = terrainRegionObj.AddComponent<TerrainRegion>();
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
        terrainRegion.Initialize(this);
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
        
        // 既存のチャンクをクリア
        ClearExistingTerrain();
        
        if (useHierarchicalSystem && terrainRegion != null && blockGenerator != null && voxelManager != null)
        {
            GenerateTerrainHierarchical();
        }
        else
        {
            GenerateTerrainLegacy();
        }
    }
    
    /// <summary>
    /// 階層システムを使用した地形生成
    /// </summary>
    private void GenerateTerrainHierarchical()
    {
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Using hierarchical generation system");
        }
        
        switch (settings.generationType)
        {
            case TerrainGenerationType.SideScroller:
                GenerateSideScrollerTerrainHierarchical();
                break;
            case TerrainGenerationType.TopDown:
                GenerateTopDownTerrainHierarchical();
                break;
            case TerrainGenerationType.Custom:
                GenerateCustomTerrainHierarchical();
                break;
        }
    }
    
    /// <summary>
    /// レガシーシステムを使用した地形生成（後方互換性）
    /// </summary>
    private void GenerateTerrainLegacy()
    {
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Using legacy generation system");
        }
        
        switch (settings.generationType)
        {
            case TerrainGenerationType.SideScroller:
                GenerateSideScrollerTerrain();
                break;
            case TerrainGenerationType.TopDown:
                GenerateTopDownTerrain();
                break;
            case TerrainGenerationType.Custom:
                GenerateCustomTerrain();
                break;
        }
    }
    
    /// <summary>
    /// 既存の地形をクリア
    /// </summary>
    private void ClearExistingTerrain()
    {
        if (useHierarchicalSystem)
        {
            terrainRegion?.ClearAllChunks();
            voxelManager?.ClearAllVoxels();
        }
        else
        {
            // レガシーシステムのクリア処理
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Chunk_"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
    
    /// <summary>
    /// 階層システム：サイドスクローラー地形生成
    /// </summary>
    private void GenerateSideScrollerTerrainHierarchical()
    {
        float totalWorldSizeX = settings.chunkCount.x * settings.chunkSize;
        float totalWorldSizeY = settings.chunkCount.y * settings.chunkSize;

        Vector3 startPosition = new Vector3(
            settings.center.x - totalWorldSizeX / 2.0f + settings.chunkSize / 2.0f,
            settings.center.y - totalWorldSizeY / 2.0f + settings.chunkSize / 2.0f,
            settings.center.z
        );

        transform.position = startPosition;

        for (int x = 0; x < settings.chunkCount.x; x++)
        {
            for (int y = 0; y < settings.chunkCount.y; y++)
            {
                Vector3Int chunkPos = new Vector3Int(x, y, 0);
                Vector3 worldPos = new Vector3(x * settings.chunkSize, y * settings.chunkSize, 0);
                
                // BlockGeneratorでパターンを生成
                var blockData = new BlockGenerator.BlockGenerationData(
                    TerrainGenerationType.SideScroller,
                    settings.voxelSize,
                    settings.chunkSize,
                    chunkPos
                );
                bool[,,] pattern = blockGenerator.GenerateBlockPattern(blockData);
                
                // TerrainRegionでチャンクを作成
                var chunkData = terrainRegion.CreateChunk(chunkPos, worldPos, pattern, settings);
                
                // VoxelManagerにボクセルデータを登録
                voxelManager.RegisterVoxelsFromPattern(pattern, chunkPos, settings);
            }
        }
    }
    
    /// <summary>
    /// 階層システム：トップダウン地形生成
    /// </summary>
    private void GenerateTopDownTerrainHierarchical()
    {
        float totalWorldSizeX = settings.chunkCount.x * settings.chunkSize;
        float totalWorldSizeZ = settings.chunkCount.y * settings.chunkSize;

        Vector3 startPosition = new Vector3(
            settings.center.x - totalWorldSizeX / 2.0f + settings.chunkSize / 2.0f,
            settings.center.y,
            settings.center.z - totalWorldSizeZ / 2.0f + settings.chunkSize / 2.0f
        );

        transform.position = startPosition;

        for (int x = 0; x < settings.chunkCount.x; x++)
        {
            for (int z = 0; z < settings.chunkCount.y; z++)
            {
                Vector3Int chunkPos = new Vector3Int(x, 0, z);
                Vector3 worldPos = new Vector3(x * settings.chunkSize, 0, z * settings.chunkSize);
                
                var blockData = new BlockGenerator.BlockGenerationData(
                    TerrainGenerationType.TopDown,
                    settings.voxelSize,
                    settings.chunkSize,
                    chunkPos
                );
                bool[,,] pattern = blockGenerator.GenerateBlockPattern(blockData);
                
                var chunkData = terrainRegion.CreateChunk(chunkPos, worldPos, pattern, settings);
                voxelManager.RegisterVoxelsFromPattern(pattern, chunkPos, settings);
            }
        }
    }
    
    /// <summary>
    /// 階層システム：カスタム地形生成
    /// </summary>
    private void GenerateCustomTerrainHierarchical()
    {
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Generating custom terrain with hierarchical system");
        }
        
        // カスタム地形生成の実装（デフォルトはサイドスクローラーと同じ）
        GenerateSideScrollerTerrainHierarchical();
    }

    /// <summary>
    /// サイドスクローラータイプの地形生成（XY平面）
    /// </summary>
    private void GenerateSideScrollerTerrain()
    {
        // チャンク全体のワールドサイズを計算
        float totalWorldSizeX = settings.chunkCount.x * settings.chunkSize;
        float totalWorldSizeY = settings.chunkCount.y * settings.chunkSize;

        // チャンク群の左下奥にあるチャンクの中心座標を計算
        Vector3 startPosition = new Vector3(
            settings.center.x - totalWorldSizeX / 2.0f + settings.chunkSize / 2.0f,
            settings.center.y - totalWorldSizeY / 2.0f + settings.chunkSize / 2.0f,
            settings.center.z
        );

        transform.position = startPosition;

        // chunkCountに基づいてチャンクを生成
        for (int x = 0; x < settings.chunkCount.x; x++)
        {
            for (int y = 0; y < settings.chunkCount.y; y++)
            {
                Vector3Int chunkPos = new Vector3Int(x, y, 0);
                bool[,,] pattern = GenerateSideScrollerPattern();
                CreateChunk(chunkPos, pattern);
            }
        }
    }

    /// <summary>
    /// トップダウンタイプの地形生成（XZ平面）
    /// </summary>
    private void GenerateTopDownTerrain()
    {
        // チャンク全体のワールドサイズを計算
        float totalWorldSizeX = settings.chunkCount.x * settings.chunkSize;
        float totalWorldSizeZ = settings.chunkCount.y * settings.chunkSize;

        // チャンク群の左下にあるチャンクの中心座標を計算
        Vector3 startPosition = new Vector3(
            settings.center.x - totalWorldSizeX / 2.0f + settings.chunkSize / 2.0f,
            settings.center.y,
            settings.center.z - totalWorldSizeZ / 2.0f + settings.chunkSize / 2.0f
        );

        transform.position = startPosition;

        // chunkCountに基づいてチャンクを生成
        for (int x = 0; x < settings.chunkCount.x; x++)
        {
            for (int z = 0; z < settings.chunkCount.y; z++)
            {
                Vector3Int chunkPos = new Vector3Int(x, 0, z);
                bool[,,] pattern = GenerateTopDownPattern();
                CreateChunk(chunkPos, pattern);
            }
        }
    }

    /// <summary>
    /// カスタムタイプの地形生成
    /// </summary>
    private void GenerateCustomTerrain()
    {
        // 将来の拡張用
        Debug.LogWarning("Custom terrain generation is not implemented yet.");
    }

    /// <summary>
    /// サイドスクローラー用のパターンを生成
    /// </summary>
    private bool[,,] GenerateSideScrollerPattern()
    {
        bool[,,] pattern = new bool[settings.voxelSize, settings.voxelSize, settings.voxelSize];
        float cubeSize = settings.chunkSize / settings.voxelSize;

        for (int lx = 0; lx < settings.voxelSize; lx++)
        {
            for (int ly = 0; ly < settings.voxelSize; ly++)
            {
                for (int lz = 0; lz < settings.voxelSize; lz++)
                {
                    // Z軸の絶対値が0.5以下のボクセルのみ生成
                    float zPos = (lz - (settings.voxelSize - 1) / 2.0f) * cubeSize;
                    pattern[lx, ly, lz] = Mathf.Abs(zPos) <= 0.5f;
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// トップダウン用のパターンを生成
    /// </summary>
    private bool[,,] GenerateTopDownPattern()
    {
        bool[,,] pattern = new bool[settings.voxelSize, settings.voxelSize, settings.voxelSize];
        float cubeSize = settings.chunkSize / settings.voxelSize;

        for (int lx = 0; lx < settings.voxelSize; lx++)
        {
            for (int ly = 0; ly < settings.voxelSize; ly++)
            {
                for (int lz = 0; lz < settings.voxelSize; lz++)
                {
                    // Y軸の絶対値が0.5以下のボクセルのみ生成
                    float yPos = (ly - (settings.voxelSize - 1) / 2.0f) * cubeSize;
                    pattern[lx, ly, lz] = Mathf.Abs(yPos) <= 0.5f;
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// チャンクを作成
    /// </summary>
    private void CreateChunk(Vector3Int chunkPos, bool[,,] pattern)
    {
        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}");
        chunkObj.transform.parent = transform;
        chunkObj.transform.localPosition = (Vector3)chunkPos * settings.chunkSize;

        // チャンクの表示サイズがchunkSizeになるようにスケールを調整
        float scale = settings.chunkSize / settings.voxelSize;
        chunkObj.transform.localScale = new Vector3(scale, scale, scale);

        Block chunk = chunkObj.AddComponent<Block>();
        var renderer = chunkObj.GetComponent<MeshRenderer>();

        // Material設定（URP Transparent）
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_AlphaClip", 1); // Alpha Clipping
        mat.mainTexture = settings.texture1;
        renderer.material = mat;

        // Blockを初期化
        chunk.Initialize(
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

        if (showDebugInfo)
        {
            Debug.Log($"Created chunk at position {chunkPos} with {CountActiveVoxels(pattern)} active voxels");
        }
    }

    /// <summary>
    /// アクティブなボクセル数をカウント（デバッグ用）
    /// </summary>
    private int CountActiveVoxels(bool[,,] pattern)
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

    [ContextMenu("Switch to Hierarchical System")]
    public void SwitchToHierarchical()
    {
        useHierarchicalSystem = true;
        GenerateTerrain();
    }
    
    [ContextMenu("Switch to Legacy System")]
    public void SwitchToLegacy()
    {
        useHierarchicalSystem = false;
        GenerateTerrain();
    }
    
    [ContextMenu("Show Debug Info")]
    public void ShowDebugInfo()
    {
        Debug.Log("=== TerrainManager Debug Info ===");
        Debug.Log($"System: {(useHierarchicalSystem ? "Hierarchical" : "Legacy")}");
        Debug.Log($"Generation Type: {settings.generationType}");
        Debug.Log($"Chunk Count: {settings.chunkCount}");
        
        if (useHierarchicalSystem && terrainRegion != null && voxelManager != null)
        {
            Debug.Log(terrainRegion.GetDebugInfo());
            Debug.Log(voxelManager.GetDebugInfo());
            if (blockGenerator != null)
            {
                Debug.Log(blockGenerator.GetDebugInfo());
            }
        }
        else
        {
            int legacyChunks = 0;
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Chunk_")) legacyChunks++;
            }
            Debug.Log($"Legacy Chunks: {legacyChunks}");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // エディタでの値変更時に設定を検証
        settings.chunkCount.x = Mathf.Max(1, settings.chunkCount.x);
        settings.chunkCount.y = Mathf.Max(1, settings.chunkCount.y);
        settings.chunkSize = Mathf.Max(0.1f, settings.chunkSize);
        settings.voxelSize = Mathf.Max(1, settings.voxelSize);
        settings.voxelHp = Mathf.Max(1, settings.voxelHp);
        settings.scaleMultiplier = Mathf.Max(0.1f, settings.scaleMultiplier);
    }
#endif
}
