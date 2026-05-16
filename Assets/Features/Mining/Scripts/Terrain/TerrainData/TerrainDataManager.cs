using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BiomeTypeとBiomeDataのマッピングを管理するデータベース。
/// </summary>
[CreateAssetMenu(fileName = "TerrainDataManager", menuName = "Momodig/Terrain Data Manager")]
public class TerrainDataManager : ScriptableObject
{
    [Header("Default Settings")]
    public BlockData defaultBlockData;
    public Texture2D defaultBackgroundTexture;

    [Header("Generation Exclusions")]
    [SerializeField]
    private List<TerrainExclusionRegionData> terrainExclusionRegions;

    [System.Serializable]
    public class BiomeDataMapping
    {
        public BiomeType biomeType;
        public BiomeData biomeData;
    }

    [SerializeField]
    private List<BiomeDataMapping> biomeDataMappings;

    private Dictionary<BiomeType, BiomeData> _dataMap;

    public void Initialize()
    {
        _dataMap = new Dictionary<BiomeType, BiomeData>();
        foreach (var mapping in biomeDataMappings)
        {
            if (mapping.biomeData != null && !_dataMap.ContainsKey(mapping.biomeType))
            {
                _dataMap.Add(mapping.biomeType, mapping.biomeData);
            }
        }
    }

    public BiomeData GetBiomeData(BiomeType biomeType)
    {
        if (_dataMap == null)
        {
            Initialize();
        }

        _dataMap.TryGetValue(biomeType, out BiomeData data);
        return data;
    }

    public BiomeData GetBiomeForHeight(int height)
    {
        foreach (var mapping in biomeDataMappings)
        {
            if (mapping.biomeData != null && height >= mapping.biomeData.minHeight && height <= mapping.biomeData.maxHeight)
            {
                return mapping.biomeData;
            }
        }
        return null; // No suitable biome found
    }

    public bool IsBlockGenerationExcluded(Vector3Int blockPosition)
    {
        if (terrainExclusionRegions == null)
        {
            return false;
        }

        foreach (var region in terrainExclusionRegions)
        {
            if (region != null && region.ContainsBlock(blockPosition))
            {
                return true;
            }
        }

        return false;
    }

    public void AppendExcludedBlockPositions(List<Vector3Int> results)
    {
        if (results == null || terrainExclusionRegions == null)
        {
            return;
        }

        foreach (var region in terrainExclusionRegions)
        {
            if (region == null)
            {
                continue;
            }

            Vector3Int min = Vector3Int.Min(region.minBlockPosition, region.maxBlockPosition);
            Vector3Int max = Vector3Int.Max(region.minBlockPosition, region.maxBlockPosition);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                results.Add(new Vector3Int(x, y, z));
            }
        }
    }

    public BlockData GetBlockDataByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (defaultBlockData != null && defaultBlockData.name == name)
        {
            return defaultBlockData;
        }

        if (biomeDataMappings == null)
        {
            return null;
        }

        foreach (var mapping in biomeDataMappings)
        {
            if (mapping.biomeData == null || mapping.biomeData.generationRules == null)
            {
                continue;
            }

            foreach (var rule in mapping.biomeData.generationRules)
            {
                if (rule == null || rule.entries == null)
                {
                    continue;
                }

                foreach (var entry in rule.entries)
                {
                    if (entry != null &&
                        entry.resultType == TerrainGenerationResultType.Block &&
                        entry.blockData != null &&
                        entry.blockData.name == name)
                    {
                        return entry.blockData;
                    }
                }
            }
        }
        return null;
    }
}
