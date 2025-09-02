using UnityEngine;

/// <summary>
/// ブロック生成クラス
/// ボクセルパターンの生成を担当
/// </summary>
public class BlockGenerator : MonoBehaviour
{
    [Header("Block Generation Configuration")]
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// ブロック生成データ
    /// </summary>
    [System.Serializable]
    public class BlockGenerationData
    {
        public TerrainGenerationType generationType;
        public int voxelSize;
        public float chunkSize;
        public Vector3Int chunkPosition;
        public float cubeSize;
        
        public BlockGenerationData(TerrainGenerationType type, int vSize, float cSize, Vector3Int cPos)
        {
            generationType = type;
            voxelSize = vSize;
            chunkSize = cSize;
            chunkPosition = cPos;
            cubeSize = cSize / vSize;
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
            Debug.Log("BlockGenerator: Initialized with TerrainManager");
        }
    }
    
    /// <summary>
    /// 指定されたチャンクのブロックパターンを生成
    /// </summary>
    public bool[,,] GenerateBlockPattern(BlockGenerationData data)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating pattern for {data.generationType} at {data.chunkPosition}");
        }
        
        bool[,,] pattern = new bool[data.voxelSize, data.voxelSize, data.voxelSize];
        
        switch (data.generationType)
        {
            case TerrainGenerationType.SideScroller:
                return GenerateSideScrollerPattern(data, pattern);
                
            case TerrainGenerationType.TopDown:
                return GenerateTopDownPattern(data, pattern);
                
            case TerrainGenerationType.Custom:
                return GenerateCustomPattern(data, pattern);
                
            default:
                Debug.LogWarning($"BlockGenerator: Unknown generation type {data.generationType}");
                return pattern;
        }
    }
    
    /// <summary>
    /// サイドスクローラー用パターン生成（XY平面、Z軸制限）
    /// </summary>
    private bool[,,] GenerateSideScrollerPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating SideScroller pattern, cubeSize: {data.cubeSize}");
        }
        
        for (int x = 0; x < data.voxelSize; x++)
        {
            for (int y = 0; y < data.voxelSize; y++)
            {
                for (int z = 0; z < data.voxelSize; z++)
                {
                    // Z軸の絶対値が0.5以下のボクセルのみ生成
                    float zPos = (z - (data.voxelSize - 1) / 2.0f) * data.cubeSize;
                    pattern[x, y, z] = Mathf.Abs(zPos) <= 0.5f;
                }
            }
        }
        
        return pattern;
    }
    
    /// <summary>
    /// トップダウン用パターン生成（XZ平面、Y軸制限）
    /// </summary>
    private bool[,,] GenerateTopDownPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating TopDown pattern, cubeSize: {data.cubeSize}");
        }
        
        for (int x = 0; x < data.voxelSize; x++)
        {
            for (int y = 0; y < data.voxelSize; y++)
            {
                for (int z = 0; z < data.voxelSize; z++)
                {
                    // Y軸の絶対値が0.5以下のボクセルのみ生成
                    float yPos = (y - (data.voxelSize - 1) / 2.0f) * data.cubeSize;
                    pattern[x, y, z] = Mathf.Abs(yPos) <= 0.5f;
                }
            }
        }
        
        return pattern;
    }
    
    /// <summary>
    /// カスタムパターン生成（拡張用）
    /// </summary>
    private bool[,,] GenerateCustomPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating Custom pattern - using full cube");
        }
        
        // デフォルトは全ブロックを生成
        for (int x = 0; x < data.voxelSize; x++)
        {
            for (int y = 0; y < data.voxelSize; y++)
            {
                for (int z = 0; z < data.voxelSize; z++)
                {
                    pattern[x, y, z] = true;
                }
            }
        }
        
        return pattern;
    }
    
    /// <summary>
    /// パターン内のアクティブブロック数を取得
    /// </summary>
    public int CountActiveBlocks(bool[,,] pattern)
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
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Pattern contains {count} active blocks");
        }
        
        return count;
    }
    
    /// <summary>
    /// パターンの密度を計算（0.0～1.0）
    /// </summary>
    public float CalculatePatternDensity(bool[,,] pattern)
    {
        int activeBlocks = CountActiveBlocks(pattern);
        int totalBlocks = pattern.GetLength(0) * pattern.GetLength(1) * pattern.GetLength(2);
        
        float density = totalBlocks > 0 ? (float)activeBlocks / totalBlocks : 0f;
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Pattern density: {density:F2} ({activeBlocks}/{totalBlocks})");
        }
        
        return density;
    }
    
    /// <summary>
    /// パターンを可視化（デバッグ用）
    /// </summary>
    public string VisualizePattern(bool[,,] pattern, int layer = 0)
    {
        if (layer >= pattern.GetLength(1)) return "Invalid layer";
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Pattern Layer {layer}:");
        
        for (int z = pattern.GetLength(2) - 1; z >= 0; z--)
        {
            for (int x = 0; x < pattern.GetLength(0); x++)
            {
                sb.Append(pattern[x, layer, z] ? "█" : "·");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// パターンを複製
    /// </summary>
    public bool[,,] ClonePattern(bool[,,] source)
    {
        int sizeX = source.GetLength(0);
        int sizeY = source.GetLength(1);
        int sizeZ = source.GetLength(2);
        
        bool[,,] clone = new bool[sizeX, sizeY, sizeZ];
        
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    clone[x, y, z] = source[x, y, z];
                }
            }
        }
        
        return clone;
    }
    
    /// <summary>
    /// デバッグ情報を取得
    /// </summary>
    public string GetDebugInfo()
    {
        return "BlockGenerator - Ready for pattern generation";
    }
}
