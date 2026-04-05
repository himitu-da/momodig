using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI; // UI繧ｳ繝ｳ繝昴・繝阪Φ繝医ｒ菴ｿ逕ｨ縺吶ｋ縺溘ａ縺ｫ霑ｽ蜉

/// <summary>
/// 蝨ｰ蠖｢逕滓・繧ｿ繧､繝怜・謖吝梛
/// </summary>
public enum TerrainGenerationType
{
    SideScroller,    // XY蟷ｳ髱｢・域立CubeSideScrollerPlacer鄂ｮ縺肴鋤縺茨ｼ・
    TopDown,         // XZ蟷ｳ髱｢・域立CubeTopDownPlacer鄂ｮ縺肴鋤縺茨ｼ・
    Custom          // 繧ｫ繧ｹ繧ｿ繝・亥ｰ・擂縺ｮ諡｡蠑ｵ逕ｨ・・
}

/// <summary>
/// 蝨ｰ蠖｢險ｭ螳壹ョ繝ｼ繧ｿ讒矩
/// 譌ｧBaseCubePlacer + CubeSideScrollerPlacer縺ｮ蜈ｨ險ｭ螳壹ｒ邨ｱ蜷・
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
    public float blockSize = 1.0f; // 繝悶Ο繝・け縺ｮ繧ｵ繧､繧ｺ
    public int voxelsPerBlock = 4;

    [Header("Generation Type")]
    public TerrainGenerationType generationType = TerrainGenerationType.SideScroller;
    
    [Header("Performance")]
    public int blocksPerFrame = 16; // 1繝輔Ξ繝ｼ繝縺ゅ◆繧翫・繝悶Ο繝・け逕滓・謨ｰ
    
    [Header("Item Loading")]
    public float itemLoadDelay = 0.1f; // 繝√Ε繝ｳ繧ｯ逕滓・蠕後・繧｢繧､繝・Β繝ｭ繝ｼ繝蛾≦蟒ｶ
}

/// <summary>
/// 蝨ｰ蠖｢蜈ｨ菴薙ｒ邂｡逅・☆繧九・繝阪・繧ｸ繝｣繝ｼ
/// WorldGenerator繧ｪ繝悶ず繧ｧ繧ｯ繝医↓繧｢繧ｿ繝・メ縺励※菴ｿ逕ｨ
/// 
/// 繝ｬ繧ｬ繧ｷ繝ｼ繧ｷ繧ｹ繝・Β・・aseCubePlacer縲，ubeSideScrollerPlacer・峨ｒ螳悟・鄂ｮ縺肴鋤縺・
/// 荳榊ｿ・ｦ√↑邯呎価髢｢菫ゅｒ謗帝勁縺励。lock繧堤峩謗･菴ｿ逕ｨ縺吶ｋ邨ｱ蜷郁ｨｭ險・
/// </summary>
public class TerrainManager : MonoBehaviour
{
    [Header("Data Managers")]
    public TerrainDataManager terrainDataManager;

    [Header("Terrain Configuration")]
    [SerializeField] private TerrainSettings settings = new TerrainSettings();

    [Header("Dynamic Generation")]
    public Transform playerTransform; // 繝励Ξ繧､繝､繝ｼ縺ｮTransform
    
    [Header("Hierarchical Managers")]
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private BlockGenerator blockGenerator;
    [SerializeField] private VoxelManager voxelManager;
    [SerializeField] private FluidSimulation fluidSimulation;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    [SerializeField] private Text voxelCountText; // 繝懊け繧ｻ繝ｫ謨ｰ繧定｡ｨ遉ｺ縺吶ｋUI繝・く繧ｹ繝・

    /// <summary>
    /// 蝨ｰ蠖｢險ｭ螳壹・蜿門ｾ・
    /// </summary>
    public TerrainSettings Settings => settings;
    
    /// <summary>
    /// 髫主ｱ､繝槭ロ繝ｼ繧ｸ繝｣繝ｼ縺ｸ縺ｮ繧｢繧ｯ繧ｻ繧ｹ
    /// </summary>
    public ChunkManager ChunkManager => chunkManager;
    public BlockManager BlockManager => blockManager;
    public BlockGenerator BlockGenerator => blockGenerator;
    public VoxelManager VoxelManager => voxelManager;
    public FluidSimulation FluidSimulation => fluidSimulation;
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
        // UI繝・く繧ｹ繝医′險ｭ螳壹＆繧後※縺・ｌ縺ｰ縲∵悴蝗槫庶縺ｮ繧｢繧､繝・Β謨ｰ繧定｡ｨ遉ｺ
        if (voxelCountText != null)
        {
            int droppedItemCount = GameObject.FindGameObjectsWithTag("DroppedItem").Length;
            voxelCountText.text = $"Dropped Items: {droppedItemCount}";
        }
    }
    
    /// <summary>
    /// 髫主ｱ､繧ｷ繧ｹ繝・Β繧貞・譛溷喧
    /// </summary>
    private void InitializeHierarchicalSystem()
    {
        // 髫主ｱ､繝槭ロ繝ｼ繧ｸ繝｣繝ｼ縺後う繝ｳ繧ｹ繝壹け繧ｿ繝ｼ縺九ｉ險ｭ螳壹＆繧後※縺・ｋ縺区､懆ｨｼ
        if (chunkManager == null || blockManager == null || blockGenerator == null || voxelManager == null)
        {
            Debug.LogError("TerrainManager: One or more hierarchical managers are not assigned in the Inspector.");
            // 驥崎ｦ√↑繧ｳ繝ｳ繝昴・繝阪Φ繝医′荳崎ｶｳ縺励※縺・ｋ縺溘ａ縲√％縺薙〒蜃ｦ逅・ｒ荳ｭ譁ｭ
            // this.enabled = false; // 繧ｳ繝ｳ繝昴・繝阪Φ繝医ｒ辟｡蜉ｹ蛹悶☆繧九↑縺ｩ縺ｮ蟇ｾ遲悶ｂ閠・∴繧峨ｌ繧・
            return;
        }
        
        // 蜷・・繝阪・繧ｸ繝｣繝ｼ繧貞・譛溷喧
        chunkManager.Initialize(this);
        blockManager.Initialize(this);
        blockGenerator.Initialize(this, settings.seed);
        voxelManager.Initialize(this);
        fluidSimulation?.Initialize(this);
        
        // TerrainDataManager繧貞・譛溷喧
        terrainDataManager?.Initialize();
        
        if (showDebugInfo)
        {
            Debug.Log("TerrainManager: Hierarchical system initialized");
        }
    }

    /// <summary>
    /// 譌｢蟄倥・蜈ｨ蝨ｰ蠖｢繝・・繧ｿ繧貞炎髯､
    /// </summary>
    public void ClearTerrain()
    {
        chunkManager?.ClearChunks();
        blockManager?.ClearAllBlocks();
        voxelManager?.ClearAllVoxels();
        fluidSimulation?.ClearFluid();
    }

    /// <summary>
    /// 蝨ｰ蠖｢繧貞・逕滓・
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
        // 繧ｨ繝・ぅ繧ｿ縺ｧ縺ｮ蛟､螟画峩譎ゅ↓險ｭ螳壹ｒ讀懆ｨｼ
        settings.initialChunkCount.x = Mathf.Max(1, settings.initialChunkCount.x);
        settings.initialChunkCount.y = Mathf.Max(1, settings.initialChunkCount.y);
        settings.blocksPerChunk.x = Mathf.Max(1, settings.blocksPerChunk.x);
        settings.blocksPerChunk.y = Mathf.Max(1, settings.blocksPerChunk.y);
        settings.blockSize = Mathf.Max(0.1f, settings.blockSize);
        settings.voxelsPerBlock = Mathf.Max(1, settings.voxelsPerBlock);
    }
#endif
}



