using UnityEngine;
using System.Collections.Generic;

public class BlockManager : MonoBehaviour
{
    [Header("Block Configuration")]
    [SerializeField] private List<BlockInstanceData> blocks = new List<BlockInstanceData>();
    [SerializeField] private bool showBlockDebugInfo = false;

    [System.Serializable]
    public class BlockInstanceData
    {
        public Vector3Int position;
        public Vector3 worldPosition;
        public Block block;
        public bool isActive = true;

        public BlockInstanceData(Vector3Int pos, Vector3 worldPos)
        {
            position = pos;
            worldPosition = worldPos;
        }
    }

    private TerrainManager terrainManager;

    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;

        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Initialized with TerrainManager");
        }
    }

    public BlockInstanceData CreateBlock(Vector3Int blockPos, Vector3 worldPos, float blockSize, int voxelsPerBlock, Transform parent)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Creating block at {blockPos}");
        }

        BlockInstanceData blockInstance = new BlockInstanceData(blockPos, worldPos);

        GameObject blockObj = new GameObject($"Block_{blockPos.x}_{blockPos.y}");
        blockObj.transform.parent = parent;
        blockObj.transform.position = worldPos;

        float scale = blockSize / voxelsPerBlock;
        blockObj.transform.localScale = new Vector3(scale, scale, scale);

        Block block = blockObj.AddComponent<Block>();
        blockInstance.block = block;

        block.Initialize(terrainManager.VoxelManager, blockPos, voxelsPerBlock, blockSize);

        blocks.Add(blockInstance);

        if (GameDataPersistenceManager.Instance.destroyedBlockPositions.Contains(blockPos))
        {
            SetBlockActive(blockPos, false);
        }

        return blockInstance;
    }

    public BlockInstanceData EnsureBlockExists(Vector3Int position, Transform parent)
    {
        BlockInstanceData existing = GetBlockAt(position);
        if (existing != null) return existing;

        if (terrainManager == null || parent == null) return null;

        var settings = terrainManager.Settings;
        Vector3 worldPos = new Vector3(
            position.x * settings.blockSize,
            position.y * settings.blockSize,
            settings.center.z + position.z * settings.blockSize
        );

        return CreateBlock(position, worldPos, settings.blockSize, settings.voxelsPerBlock, parent);
    }

    public BlockInstanceData GetBlockAt(Vector3Int position)
    {
        foreach (var block in blocks)
        {
            if (block.position == position)
            {
                return block;
            }
        }
        return null;
    }

    public List<BlockInstanceData> GetAllBlocks()
    {
        return new List<BlockInstanceData>(blocks);
    }

    public int GetActiveBlockCount()
    {
        int count = 0;
        foreach (var block in blocks)
        {
            if (block.isActive) count++;
        }
        return count;
    }

    public void DestroyBlock(Vector3Int position)
    {
        SetBlockActive(position, false);
        GameDataPersistenceManager.Instance.destroyedBlockPositions.Add(position);
    }

    public void SetBlockActive(Vector3Int position, bool active)
    {
        BlockInstanceData block = GetBlockAt(position);
        if (block != null && block.block != null)
        {
            block.isActive = active;
            block.block.gameObject.SetActive(active);

            if (showBlockDebugInfo)
            {
                Debug.Log($"BlockManager: Set block {position} active: {active}");
            }
        }
    }

    public bool ActivateAndRefreshBlock(Vector3Int position)
    {
        BlockInstanceData block = GetBlockAt(position);
        if (block == null || block.block == null)
        {
            return false;
        }

        block.isActive = true;
        block.block.gameObject.SetActive(true);
        GameDataPersistenceManager.Instance.destroyedBlockPositions.Remove(position);
        block.block.GenerateMesh();
        return true;
    }

    public void ClearAllBlocks()
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Clearing {blocks.Count} blocks");
        }

        foreach (var block in blocks)
        {
            if (block.block != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(block.block.gameObject);
                }
                else
                {
                    DestroyImmediate(block.block.gameObject);
                }
            }
        }

        blocks.Clear();
    }

    public string GetDebugInfo()
    {
        return $"BlockManager - Total: {blocks.Count}, Active: {GetActiveBlockCount()}";
    }

    void OnDestroy()
    {
        ClearAllBlocks();
    }
}
