using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ボクセル管琁E��ラス
/// 個、E�Eボクセルレベルでの詳細管琁E��拁E��E
/// </summary>
public class VoxelManager : MonoBehaviour
{
    [Header("Voxel Management Configuration")]
    [SerializeField] private bool showVoxelDebugInfo = false;
    
    // チE�Eタ構造をListからDictionaryに変更し、ブロチE��座標とローカル座標でボクセルチE�Eタを管琁E
    // これにより、O(n)の検索がO(1)になり、パフォーマンスが大幁E��向丁E
    private Dictionary<Vector3Int, Dictionary<Vector3Int, Voxel>> trackedVoxels = new Dictionary<Vector3Int, Dictionary<Vector3Int, Voxel>>();
    
    /// <summary>
    /// TerrainManagerからの参�E
    /// </summary>
    private TerrainManager terrainManager;
    
    /// <summary>
    /// 初期匁E
    /// </summary>
    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;
        
        if (showVoxelDebugInfo)
        {
            Debug.Log("VoxelManager: Initialized with TerrainManager");
        }
    }
    
    /// <summary>
    /// ボクセルパターンからボクセルチE�Eタを作�E
    /// </summary>
    public void RegisterVoxelsFromPattern(bool[,,] pattern, Vector3Int blockPos, Vector3 blockWorldPos, BlockData data, float blockSize, int voxelsPerBlock)
    {
        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Registering voxels for block {blockPos}");
        }
        
        for (int x = 0; x < pattern.GetLength(0); x++)
        {
            for (int y = 0; y < pattern.GetLength(1); y++)
            {
                for (int z = 0; z < pattern.GetLength(2); z++)
                {
                    if (pattern[x, y, z])
                    {
                        Vector3Int localPos = new Vector3Int(x, y, z);
                        Vector3 worldPos = CalculateWorldPosition(blockWorldPos, localPos, blockSize, voxelsPerBlock);
                        
                        Voxel voxelData = new Voxel(
                            blockPos, 
                            localPos, 
                            worldPos, 
                            data.voxelHp, 
                            DetermineVoxelType(blockPos, localPos)
                        );
                        if (!trackedVoxels.ContainsKey(blockPos))
                        {
                            trackedVoxels[blockPos] = new Dictionary<Vector3Int, Voxel>();
                        }
                        trackedVoxels[blockPos][localPos] = voxelData;
                    }
                }
            }
        }
        
        // 壊れかけのブロチE��惁E��を適用
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager.partiallyDestroyedBlocks.TryGetValue(blockPos, out var destroyedVoxels))
        {
            foreach (var localVoxelPos in destroyedVoxels)
            {
                if (trackedVoxels.ContainsKey(blockPos) && trackedVoxels[blockPos].ContainsKey(localVoxelPos))
                {
                    trackedVoxels[blockPos][localVoxelPos].isActive = false;
                }
            }
        }

        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Registered {CountVoxelsInBlock(blockPos)} voxels for block {blockPos}");
        }
    }
    
    /// <summary>
    /// ワールド座標を計箁E
    /// </summary>
    private Vector3 CalculateWorldPosition(Vector3 blockWorldPos, Vector3Int localPos, float blockSize, int voxelsPerBlock)
    {
        float voxelUnit = blockSize / voxelsPerBlock;
        // ブロチE��の中忁E��ら�EオフセチE��としてボクセルのローカル座標を計箁E
        Vector3 localOffset = new Vector3(
            (localPos.x - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.y - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.z - voxelsPerBlock / 2f + 0.5f) * voxelUnit
        );
        
        return blockWorldPos + localOffset;
    }
    
    /// <summary>
    /// ボクセルタイプを決宁E
    /// </summary>
    private VoxelType DetermineVoxelType(Vector3Int blockPos, Vector3Int localPos)
    {
        // チE��ォルト�E標準タイチE
        // 封E��皁E��位置めE��ンダム要素に基づぁE��タイプを決宁E
        return VoxelType.Standard;
    }
    
    /// <summary>
    /// 持E��座標�EボクセルチE�Eタを取征E
    /// </summary>
    public Voxel GetVoxelAt(Vector3Int blockPos, Vector3Int localPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block) && block.TryGetValue(localPos, out var voxel))
        {
            return voxel.isActive ? voxel : null;
        }
        return null;
    }
    
    /// <summary>
    /// 持E��ブロチE��冁E�Eボクセル数を取征E
    /// </summary>
    public int CountVoxelsInBlock(Vector3Int blockPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block))
        {
            int count = 0;
            foreach (var voxel in block.Values)
            {
                if (voxel.isActive)
                {
                    count++;
                }
            }
            return count;
        }
        return 0;
    }
    
    /// <summary>
    /// ボクセルにダメージを与えめE
    /// </summary>
    public bool DamageVoxel(Vector3Int blockPos, Vector3Int localPos, int damage = 1)
    {
        Voxel voxel = GetVoxelAt(blockPos, localPos);
        if (voxel != null)
        {
            voxel.health -= damage;
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Damaged voxel at {blockPos},{localPos} - Health: {voxel.health}");
            }
            
            if (voxel.health <= 0)
            {
                return DestroyVoxel(blockPos, localPos);
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// ボクセルを破壁E
    /// </summary>
    public bool DestroyVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        Voxel voxel = GetVoxelAt(blockPos, localPos);
        if (voxel != null && voxel.voxelType != VoxelType.Unbreakable)
        {
            voxel.isActive = false;
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Destroyed voxel at {blockPos},{localPos}");
            }

            // 永続化マネージャーに状態を記録
            var persistenceManager = GameDataPersistenceManager.Instance;

            // ブロチE��冁E�E残りのボクセル数を確誁E
            if (CountVoxelsInBlock(blockPos) == 0)
            {
                // 完�Eに破壊された
                terrainManager.BlockManager.DestroyBlock(blockPos);
                // 壊れかけリストから�E削除
                if (persistenceManager.partiallyDestroyedBlocks.ContainsKey(blockPos))
                {
                    persistenceManager.partiallyDestroyedBlocks.Remove(blockPos);
                }
            }
            else
            {
                // 部刁E��に破壊された
                if (!persistenceManager.partiallyDestroyedBlocks.ContainsKey(blockPos))
                {
                    persistenceManager.partiallyDestroyedBlocks[blockPos] = new HashSet<Vector3Int>();
                }
                persistenceManager.partiallyDestroyedBlocks[blockPos].Add(localPos);
            }

            terrainManager?.FluidManager?.NotifySolidVoxelRemoved(voxel.worldPosition);
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// ボクセルを復允E
    /// </summary>
    public bool RestoreVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        Voxel voxel = GetVoxelAt(blockPos, localPos);
        if (voxel != null && !voxel.isActive)
        {
            voxel.isActive = true;
            voxel.health = voxel.maxHealth; // 最大体力に復允E
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Restored voxel at {blockPos},{localPos}");
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 持E��ブロチE��のボクセルをすべて取征E
    /// </summary>
    public List<Voxel> GetVoxelsInBlock(Vector3Int blockPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block))
        {
            return new List<Voxel>(block.Values);
        }
        return new List<Voxel>();
    }
    
    /// <summary>
    /// アクチE��ブなボクセル数を取征E
    /// </summary>
    public int GetActiveVoxelCount()
    {
        int count = 0;
        foreach (var block in trackedVoxels.Values)
        {
            foreach (var voxel in block.Values)
            {
                if (voxel.isActive) count++;
            }
        }
        return count;
    }
    
    /// <summary>
    /// ボクセルタイプ別の統計を取征E
    /// </summary>
    public Dictionary<VoxelType, int> GetVoxelTypeStatistics()
    {
        Dictionary<VoxelType, int> stats = new Dictionary<VoxelType, int>();
        
        foreach (VoxelType type in System.Enum.GetValues(typeof(VoxelType)))
        {
            stats[type] = 0;
        }
        
        foreach (var block in trackedVoxels.Values)
        {
            foreach (var voxel in block.Values)
            {
                if (voxel.isActive)
                {
                    stats[voxel.voxelType]++;
                }
            }
        }
        
        return stats;
    }
    
    /// <summary>
    /// 持E��ブロチE��のボクセルをクリア
    /// </summary>
    public void ClearVoxelsInBlock(Vector3Int blockPos)
    {
        if (trackedVoxels.ContainsKey(blockPos))
        {
            trackedVoxels.Remove(blockPos);
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Cleared voxels for block {blockPos}");
            }
        }
    }
    
    /// <summary>
    /// すべてのボクセルチE�Eタをクリア
    /// </summary>
    public void ClearAllVoxels()
    {
        int totalVoxels = 0;
        foreach (var block in trackedVoxels.Values)
        {
            totalVoxels += block.Count;
        }

        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Clearing all {totalVoxels} voxels from {trackedVoxels.Count} blocks");
        }
        
        trackedVoxels.Clear();
    }
    
    /// <summary>
    /// チE��チE��惁E��を取征E
    /// </summary>
    public string GetDebugInfo()
    {
        var stats = GetVoxelTypeStatistics();
        int activeCount = GetActiveVoxelCount();
        int totalVoxels = 0;
        foreach (var block in trackedVoxels.Values)
        {
            totalVoxels += block.Count;
        }
        
        return $"VoxelManager - Total Blocks: {trackedVoxels.Count}, Total Voxels: {totalVoxels}, Active: {activeCount}, Types: " +
               $"Standard:{stats[VoxelType.Standard]}, Reinforced:{stats[VoxelType.Reinforced]}, " +
               $"Fragile:{stats[VoxelType.Fragile]}, Unbreakable:{stats[VoxelType.Unbreakable]}, " +
               $"Special:{stats[VoxelType.Special]}";
    }
    
    void OnDestroy()
    {
        ClearAllVoxels();
    }
}


