using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 繝悶Ο繝・・ｽ・ｽ逕滂ｿｽE繧ｯ繝ｩ繧ｹ
/// 繝懊け繧ｻ繝ｫ繝代ち繝ｼ繝ｳ縺ｮ逕滂ｿｽE繧呈球蠖・
/// </summary>
public class BlockGenerator : MonoBehaviour
{
    [Header("Block Generation Configuration")]
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// 繝悶Ο繝・・ｽ・ｽ逕滂ｿｽE繝・・ｽE繧ｿ
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
    /// TerrainManager縺九ｉ縺ｮ蜿ゑｿｽE
    /// </summary>
    private TerrainManager terrainManager;
    private int terrainSeed;
    
    /// <summary>
    /// 蛻晄悄蛹・
    /// </summary>
    public void Initialize(TerrainManager manager, int seed)
    {
        terrainManager = manager;
        terrainSeed = seed;
        
        if (showBlockDebugInfo)
        {
            Debug.Log("BlockGenerator: Initialized with TerrainManager");
        }
    }

    public void ResetRandom(int seed)
    {
        terrainSeed = seed;
    }
    
    /// <summary>
    /// 謖・・ｽ・ｽ縺輔ｌ縺溘メ繝｣繝ｳ繧ｯ縺ｮ繝悶Ο繝・・ｽ・ｽ繝代ち繝ｼ繝ｳ繧堤函謌・
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
            case TerrainGenerationType.PlayPlane:
                return GeneratePlayPlanePattern(data, pattern);
                
            case TerrainGenerationType.Custom:
                return GenerateCustomPattern(data, pattern);
                
            default:
                Debug.LogWarning($"BlockGenerator: Unknown generation type {data.generationType}");
                return pattern;
        }
    }
    
    /// <summary>
    /// Play plane pattern generation (XY plane with Z-axis constraint).
    /// </summary>
    private bool[,,] GeneratePlayPlanePattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating PlayPlane pattern, voxelWorldSize: {data.voxelWorldSize}");
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
    /// 繧ｫ繧ｹ繧ｿ繝繝代ち繝ｼ繝ｳ逕滂ｿｽE・ｽE・ｽ諡｡蠑ｵ逕ｨ・ｽE・ｽE
    /// </summary>
    private bool[,,] GenerateCustomPattern(BlockGenerationData data, bool[,,] pattern)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockGenerator: Generating Custom pattern - using full cube");
        }
        
        // 繝・・ｽ・ｽ繧ｩ繝ｫ繝茨ｿｽE蜈ｨ繝悶Ο繝・・ｽ・ｽ繧堤函謌・
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
    /// 繝代ち繝ｼ繝ｳ蜀・・ｽE繧｢繧ｯ繝・・ｽ・ｽ繝厄ｿｽE繧ｯ繧ｻ繝ｫ謨ｰ繧貞叙蠕・
    /// </summary>
    public bool IsVoxelSolid(TerrainGenerationType generationType, int voxelsPerBlock, float blockSize, Vector3Int blockPosition, Vector3Int localPosition)
    {
        float voxelWorldSize = blockSize / voxelsPerBlock;

        switch (generationType)
        {
            case TerrainGenerationType.PlayPlane:
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
    /// 繝代ち繝ｼ繝ｳ縺ｮ蟇・・ｽ・ｽ繧定ｨ育ｮ暦ｼ・.0・ｽE・ｽE.0・ｽE・ｽE
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
    /// 繝代ち繝ｼ繝ｳ繧貞庄隕門喧・ｽE・ｽ繝・ヰ繝・げ逕ｨ・ｽE・ｽE
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
    /// 繝代ち繝ｼ繝ｳ繧定､・・ｽ・ｽ
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
    /// 繝・・ｽ・ｽ繝・・ｽ・ｽ諠・・ｽ・ｽ繧貞叙蠕・
    /// </summary>
    public string GetDebugInfo()
    {
        return "BlockGenerator - Ready for pattern generation";
    }

    /// <summary>
    /// 謖・・ｽ・ｽ縺輔ｌ縺溯ｫ也炊蠎ｧ讓吶↓蟇ｾ蠢懊☆繧毅lockData繧貞叙蠕・
    /// </summary>
    public BlockData GetBlockDataForPosition(Vector3Int blockPosition)
    {
        if (terrainManager == null || terrainManager.TerrainDataManager == null)
        {
            return null;
        }

        BiomeData biome = terrainManager.TerrainDataManager.GetBiomeForHeight(blockPosition.y);
        if (biome == null || biome.generationRules == null || biome.generationRules.Count == 0)
        {
            return null;
        }

        BlockData currentBlockData = null;
        for (int ruleIndex = 0; ruleIndex < biome.generationRules.Count; ruleIndex++)
        {
            TerrainGenerationEntry selectedEntry = SelectEntryForRule(biome.generationRules[ruleIndex], blockPosition, ruleIndex);
            if (selectedEntry == null || selectedEntry.resultType == TerrainGenerationResultType.NoOp)
            {
                continue;
            }

            if (selectedEntry.resultType == TerrainGenerationResultType.Clear)
            {
                currentBlockData = null;
                continue;
            }

            if (selectedEntry.resultType == TerrainGenerationResultType.Block)
            {
                currentBlockData = selectedEntry.blockData;
            }
        }

        return currentBlockData;
    }

    private TerrainGenerationEntry SelectEntryForRule(TerrainGenerationRule rule, Vector3Int blockPosition, int ruleIndex)
    {
        if (rule == null || rule.entries == null || rule.entries.Count == 0)
        {
            return null;
        }

        List<float> weights = new List<float>(rule.entries.Count);
        float totalWeight = 0f;
        foreach (TerrainGenerationEntry entry in rule.entries)
        {
            float weight = 0f;
            if (entry != null && (entry.resultType != TerrainGenerationResultType.Block || entry.blockData != null))
            {
                weight = entry.EvaluateWeight(blockPosition);
            }

            weights.Add(weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float randomValue = GetDeterministicRandom01(blockPosition, ruleIndex) * totalWeight;
        for (int i = 0; i < rule.entries.Count; i++)
        {
            if (randomValue < weights[i])
            {
                return rule.entries[i];
            }
            randomValue -= weights[i];
        }

        return rule.entries[rule.entries.Count - 1];
    }

    private float GetDeterministicRandom01(Vector3Int blockPosition, int ruleIndex)
    {
        unchecked
        {
            uint hash = (uint)terrainSeed;
            hash = (hash * 16777619u) ^ (uint)blockPosition.x;
            hash = (hash * 16777619u) ^ (uint)blockPosition.y;
            hash = (hash * 16777619u) ^ (uint)blockPosition.z;
            hash = (hash * 16777619u) ^ (uint)ruleIndex;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777216f;
        }
    }
}