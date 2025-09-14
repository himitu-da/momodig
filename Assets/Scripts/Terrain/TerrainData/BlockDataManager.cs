using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ResourceTypeとBlockDataのマッピングを管理するデータベース。
/// </summary>
[CreateAssetMenu(fileName = "BlockDataManager", menuName = "Momodig/Block Data Manager")]
public class BlockDataManager : ScriptableObject
{
    [System.Serializable]
    public class BlockDataMapping
    {
        public ResourceType resourceType;
        public BlockData blockData;
    }

    [SerializeField]
    private List<BlockDataMapping> blockDataMappings;

    private Dictionary<ResourceType, BlockData> _dataMap;

    public void Initialize()
    {
        _dataMap = new Dictionary<ResourceType, BlockData>();
        foreach (var mapping in blockDataMappings)
        {
            if (mapping.blockData != null && !_dataMap.ContainsKey(mapping.resourceType))
            {
                _dataMap.Add(mapping.resourceType, mapping.blockData);
            }
        }
    }

    public BlockData GetBlockData(ResourceType resourceType)
    {
        if (_dataMap == null)
        {
            Initialize();
        }

        _dataMap.TryGetValue(resourceType, out BlockData data);
        return data;
    }
}
