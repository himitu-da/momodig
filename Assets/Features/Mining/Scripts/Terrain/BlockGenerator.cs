using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロチE��生�Eクラス
/// ボクセルパターンの生�Eを担彁E
/// </summary>
public class BlockGenerator : MonoBehaviour
{
    [Header("Block Generation Configuration")]
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// ブロチE��生�EチE�Eタ
    /// </summary>
    [System.Serializable]
    public class BlockGenerationData
    {
        public TerrainGenerationType generationType;
        public int voxelsPerBlock;
        public float blockSize;
        public Vector3Int blockPosition;
        public float voxelWorldSize;
        
        public BlockGenerationData(TerrainGenerationType type, int vPerBlock, float bSize, Vector3Int bPos)
        {
            generationType = type;
            voxelsPerBlock = vPerBlock;
            blockSize = bSize;
            blockPosition = bPos;
            voxelWorldSize = bSize / vPerBlock;
        }
    }
    
    /// <summary>
    /// TerrainManagerからの参�E
    /// </summary>
    private TerrainManager terrainManager;
    private System.Random random;
    
    /// <summary>
    /// 初期匁E
    /// </summary>
    public void Initialize(TerrainManager manager, int seed)
    {
        terrainManager = manager;
        random = new System.Random(seed);
        
        if (showBlockDebugInfo)
        {
            Debug.Log("BlockGenerator: Initialized with TerrainManager");
        }
    }

    public void ResetRandom(int seed)
    {
        random = new System.Random(seed);
    }
    
    /// <summary>
    /// 持E��されたチャンクのブロチE��パターンを生戁E
    /// </summary>
    public bool[,,] GenerateBlockPattern(BlockGenerationData data)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating pattern for {data.generationType} at {data.blockPosition}");
        }
        
        bool[,,] pattern = new bool[data.voxelsPerBlock, data.voxelsPerBlock, data.voxelsPerBlock];
        
        switch (data.generationType)
        {
            case TerrainGenerationType.SideScroller:
                return GenerateSideScrollerPattern(data, pattern);
                
            case TerrainGenerationType.Custom:
                return GenerateCustomPattern(data, pattern);
                
            default:
                Debug.LogWarning($"BlockGenerator: Unknown generation type {data.generationType}");
                return pattern;
        }
    }
    
    /// <summary>
    /// サイドスクローラー用パターン生�E�E�EY平面、Z軸制限！E
    /// </summary>
    private bool[,,] GenerateSideScrollerPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating SideScroller pattern, voxelWorldSize: {data.voxelWorldSize}");
        }
        
        for (int x = 0; x < data.voxelsPerBlock; x++)
        {
            for (int y = 0; y < data.voxelsPerBlock; y++)
            {
                for (int z = 0; z < data.voxelsPerBlock; z++)
                {
                    pattern[x, y, z] = IsVoxelSolid(data.generationType, data.voxelsPerBlock, data.blockSize, data.blockPosition, new Vector3Int(x, y, z));
                }
            }
        }
        
        return pattern;
    }
    
    /// <summary>
    /// カスタムパターン生�E�E�拡張用�E�E
    /// </summary>
    private bool[,,] GenerateCustomPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating Custom pattern - using full cube");
        }
        
        // チE��ォルト�E全ブロチE��を生戁E
        for (int x = 0; x < data.voxelsPerBlock; x++)
        {
            for (int y = 0; y < data.voxelsPerBlock; y++)
            {
                for (int z = 0; z < data.voxelsPerBlock; z++)
                {
                    pattern[x, y, z] = true;
                }
            }
        }
        
        return pattern;
    }
    
    /// <summary>
    /// パターン冁E�EアクチE��ブ�Eクセル数を取征E
    /// </summary>
    public bool IsVoxelSolid(TerrainGenerationType generationType, int voxelsPerBlock, float blockSize, Vector3Int blockPosition, Vector3Int localPosition)
    {
        float voxelWorldSize = blockSize / voxelsPerBlock;

        switch (generationType)
        {
            case TerrainGenerationType.SideScroller:
            {
                float zPos = (localPosition.z - (voxelsPerBlock - 1) / 2.0f) * voxelWorldSize;
                return Mathf.Abs(zPos) <= 0.5f;
            }

            case TerrainGenerationType.Custom:
                return true;

            default:
                return false;
        }
    }
    public int CountActiveVoxels(bool[,,] pattern)
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
            Debug.Log($"BlockGenerator: Pattern contains {count} active voxels");
        }
        
        return count;
    }
    
    /// <summary>
    /// パターンの寁E��を計算！E.0�E�E.0�E�E
    /// </summary>
    public float CalculatePatternDensity(bool[,,] pattern)
    {
        int activeVoxels = CountActiveVoxels(pattern);
        int totalVoxels = pattern.GetLength(0) * pattern.GetLength(1) * pattern.GetLength(2);
        
        float density = totalVoxels > 0 ? (float)activeVoxels / totalVoxels : 0f;
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Pattern density: {density:F2} ({activeVoxels}/{totalVoxels})");
        }
        
        return density;
    }
    
    /// <summary>
    /// パターンを可視化�E�デバッグ用�E�E
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
                sb.Append(pattern[x, layer, z] ? "# " : ". ");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// パターンを褁E��
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
    /// チE��チE��惁E��を取征E
    /// </summary>
    public string GetDebugInfo()
    {
        return "BlockGenerator - Ready for pattern generation";
    }

    /// <summary>
    /// 持E��された論理座標に対応するBlockDataを取征E
    /// </summary>
    public BlockData GetBlockDataForPosition(Vector3Int blockPosition)
    {
        if (terrainManager == null || terrainManager.TerrainDataManager == null)
        {
            return null;
        }

        // 論理Y座標に基づぁE��バイオームを取征E
        var biome = terrainManager.TerrainDataManager.GetBiomeForHeight(blockPosition.y);
        if (biome == null || biome.availableBlocks == null || biome.availableBlocks.Count == 0)
        {
            return null;
        }

        // 吁E��ロチE��の重みを計箁E
        List<float> weights = new List<float>();
        float totalWeight = 0f;
        foreach (var blockDist in biome.availableBlocks)
        {
            // AnimationCurveを論理Y座標で直接評価
            float weight = blockDist.distributionCurve.Evaluate(blockPosition.y);
            weights.Add(weight);
            totalWeight += weight;
        }

        // 合計�E重みぁE以下なら、何も生�EしなぁE
        if (totalWeight <= 0)
        {
            return null;
        }

        // 加重ランダム選抁E
        float randomValue = (float)(random.NextDouble() * totalWeight);
        for (int i = 0; i < biome.availableBlocks.Count; i++)
        {
            if (randomValue < weights[i])
            {
                return biome.availableBlocks[i].blockData;
            }
            randomValue -= weights[i];
        }

        // フォールバック�E�計算誤差などでここまで来た場合！E
        return biome.availableBlocks[biome.availableBlocks.Count - 1].blockData;
    }
}



