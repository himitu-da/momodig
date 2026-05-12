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
            if (mapping.biomeData != null)
            {
                foreach (var blockDist in mapping.biomeData.availableBlocks)
                {
                    if (blockDist.blockData != null && blockDist.blockData.name == name)
                    {
                        return blockDist.blockData;
                    }
                }
            }
        }
        return null;
    }
}
