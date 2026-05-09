using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 繝悶Ο繝・け逕滓・繧ｯ繝ｩ繧ｹ
/// 繝懊け繧ｻ繝ｫ繝代ち繝ｼ繝ｳ縺ｮ逕滓・繧呈球蠖・
/// </summary>
public class BlockGenerator : MonoBehaviour
{
    [Header("Block Generation Configuration")]
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// 繝悶Ο繝・け逕滓・繝・・繧ｿ
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
    /// TerrainManager縺九ｉ縺ｮ蜿ら・
    /// </summary>
    private TerrainManager terrainManager;
    private System.Random random;
    
    /// <summary>
    /// 蛻晄悄蛹・
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
    /// 謖・ｮ壹＆繧後◆繝√Ε繝ｳ繧ｯ縺ｮ繝悶Ο繝・け繝代ち繝ｼ繝ｳ繧堤函謌・
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
    /// 繧ｵ繧､繝峨せ繧ｯ繝ｭ繝ｼ繝ｩ繝ｼ逕ｨ繝代ち繝ｼ繝ｳ逕滓・・・Y蟷ｳ髱｢縲〇霆ｸ蛻ｶ髯撰ｼ・
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
    /// 繝医ャ繝励ム繧ｦ繝ｳ逕ｨ繝代ち繝ｼ繝ｳ逕滓・・・Z蟷ｳ髱｢縲〆霆ｸ蛻ｶ髯撰ｼ・
    /// </summary>
    private bool[,,] GenerateTopDownPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating TopDown pattern, voxelWorldSize: {data.voxelWorldSize}");
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
    /// 繧ｫ繧ｹ繧ｿ繝繝代ち繝ｼ繝ｳ逕滓・・域僑蠑ｵ逕ｨ・・
    /// </summary>
    private bool[,,] GenerateCustomPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating Custom pattern - using full cube");
        }
        
        // 繝・ヵ繧ｩ繝ｫ繝医・蜈ｨ繝悶Ο繝・け繧堤函謌・
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
    /// 繝代ち繝ｼ繝ｳ蜀・・繧｢繧ｯ繝・ぅ繝悶・繧ｯ繧ｻ繝ｫ謨ｰ繧貞叙蠕・
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

            case TerrainGenerationType.TopDown:
            {
                float yPos = (localPosition.y - (voxelsPerBlock - 1) / 2.0f) * voxelWorldSize;
                return Mathf.Abs(yPos) <= 0.5f;
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
    /// 繝代ち繝ｼ繝ｳ縺ｮ蟇・ｺｦ繧定ｨ育ｮ暦ｼ・.0・・.0・・
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
    /// 繝代ち繝ｼ繝ｳ繧貞庄隕門喧・医ョ繝舌ャ繧ｰ逕ｨ・・
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
    /// 繝代ち繝ｼ繝ｳ繧定､・｣ｽ
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
    /// 繝・ヰ繝・げ諠・ｱ繧貞叙蠕・
    /// </summary>
    public string GetDebugInfo()
    {
        return "BlockGenerator - Ready for pattern generation";
    }

    /// <summary>
    /// 謖・ｮ壹＆繧後◆隲也炊蠎ｧ讓吶↓蟇ｾ蠢懊☆繧毅lockData繧貞叙蠕・
    /// </summary>
    public BlockData GetBlockDataForPosition(Vector3Int blockPosition)
    {
        if (terrainManager == null || terrainManager.TerrainDataManager == null)
        {
            return null;
        }

        // 隲也炊Y蠎ｧ讓吶↓蝓ｺ縺･縺・※繝舌う繧ｪ繝ｼ繝繧貞叙蠕・
        var biome = terrainManager.TerrainDataManager.GetBiomeForHeight(blockPosition.y);
        if (biome == null || biome.availableBlocks == null || biome.availableBlocks.Count == 0)
        {
            return null;
        }

        // 蜷・ヶ繝ｭ繝・け縺ｮ驥阪∩繧定ｨ育ｮ・
        List<float> weights = new List<float>();
        float totalWeight = 0f;
        foreach (var blockDist in biome.availableBlocks)
        {
            // AnimationCurve繧定ｫ也炊Y蠎ｧ讓吶〒逶ｴ謗･隧穂ｾ｡
            float weight = blockDist.distributionCurve.Evaluate(blockPosition.y);
            weights.Add(weight);
            totalWeight += weight;
        }

        // 蜷郁ｨ医・驥阪∩縺・莉･荳九↑繧峨∽ｽ輔ｂ逕滓・縺励↑縺・
        if (totalWeight <= 0)
        {
            return null;
        }

        // 蜉驥阪Λ繝ｳ繝繝驕ｸ謚・
        float randomValue = (float)(random.NextDouble() * totalWeight);
        for (int i = 0; i < biome.availableBlocks.Count; i++)
        {
            if (randomValue < weights[i])
            {
                return biome.availableBlocks[i].blockData;
            }
            randomValue -= weights[i];
        }

        // 繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ・郁ｨ育ｮ苓ｪ､蟾ｮ縺ｪ縺ｩ縺ｧ縺薙％縺ｾ縺ｧ譚･縺溷ｴ蜷茨ｼ・
        return biome.availableBlocks[biome.availableBlocks.Count - 1].blockData;
    }
}



