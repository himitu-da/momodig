using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 繝懊け繧ｻ繝ｫ邂｡逅・け繝ｩ繧ｹ
/// 蛟九・・繝懊け繧ｻ繝ｫ繝ｬ繝吶Ν縺ｧ縺ｮ隧ｳ邏ｰ邂｡逅・ｒ諡・ｽ・
/// </summary>
public class VoxelManager : MonoBehaviour
{
    [Header("Voxel Management Configuration")]
    [SerializeField] private bool showVoxelDebugInfo = false;
    
    // 繝・・繧ｿ讒矩繧鱈ist縺九ｉDictionary縺ｫ螟画峩縺励√ヶ繝ｭ繝・け蠎ｧ讓吶→繝ｭ繝ｼ繧ｫ繝ｫ蠎ｧ讓吶〒繝懊け繧ｻ繝ｫ繝・・繧ｿ繧堤ｮ｡逅・
    // 縺薙ｌ縺ｫ繧医ｊ縲＾(n)縺ｮ讀懃ｴ｢縺薫(1)縺ｫ縺ｪ繧翫√ヱ繝輔か繝ｼ繝槭Φ繧ｹ縺悟､ｧ蟷・↓蜷台ｸ・
    private Dictionary<Vector3Int, Dictionary<Vector3Int, Voxel>> trackedVoxels = new Dictionary<Vector3Int, Dictionary<Vector3Int, Voxel>>();
    
    /// <summary>
    /// TerrainManager縺九ｉ縺ｮ蜿ら・
    /// </summary>
    private TerrainManager terrainManager;
    
    /// <summary>
    /// 蛻晄悄蛹・
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
    /// 繝懊け繧ｻ繝ｫ繝代ち繝ｼ繝ｳ縺九ｉ繝懊け繧ｻ繝ｫ繝・・繧ｿ繧剃ｽ懈・
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
        
        // 螢翫ｌ縺九￠縺ｮ繝悶Ο繝・け諠・ｱ繧帝←逕ｨ
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
    /// 繝ｯ繝ｼ繝ｫ繝牙ｺｧ讓吶ｒ險育ｮ・
    /// </summary>
    private Vector3 CalculateWorldPosition(Vector3 blockWorldPos, Vector3Int localPos, float blockSize, int voxelsPerBlock)
    {
        float voxelUnit = blockSize / voxelsPerBlock;
        // 繝悶Ο繝・け縺ｮ荳ｭ蠢・°繧峨・繧ｪ繝輔そ繝・ヨ縺ｨ縺励※繝懊け繧ｻ繝ｫ縺ｮ繝ｭ繝ｼ繧ｫ繝ｫ蠎ｧ讓吶ｒ險育ｮ・
        Vector3 localOffset = new Vector3(
            (localPos.x - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.y - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.z - voxelsPerBlock / 2f + 0.5f) * voxelUnit
        );
        
        return blockWorldPos + localOffset;
    }
    
    /// <summary>
    /// 繝懊け繧ｻ繝ｫ繧ｿ繧､繝励ｒ豎ｺ螳・
    /// </summary>
    private VoxelType DetermineVoxelType(Vector3Int blockPos, Vector3Int localPos)
    {
        // 繝・ヵ繧ｩ繝ｫ繝医・讓呎ｺ悶ち繧､繝・
        // 蟆・擂逧・↓菴咲ｽｮ繧・Λ繝ｳ繝繝隕∫ｴ縺ｫ蝓ｺ縺･縺・※繧ｿ繧､繝励ｒ豎ｺ螳・
        return VoxelType.Standard;
    }
    
    /// <summary>
    /// 謖・ｮ壼ｺｧ讓吶・繝懊け繧ｻ繝ｫ繝・・繧ｿ繧貞叙蠕・
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
    /// 謖・ｮ壹ヶ繝ｭ繝・け蜀・・繝懊け繧ｻ繝ｫ謨ｰ繧貞叙蠕・
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
    /// 繝懊け繧ｻ繝ｫ縺ｫ繝繝｡繝ｼ繧ｸ繧剃ｸ弱∴繧・
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
    /// 繝懊け繧ｻ繝ｫ繧堤ｴ螢・
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

            // 豌ｸ邯壼喧繝槭ロ繝ｼ繧ｸ繝｣繝ｼ縺ｫ迥ｶ諷九ｒ險倬鹸
            var persistenceManager = GameDataPersistenceManager.Instance;

            // 繝悶Ο繝・け蜀・・谿九ｊ縺ｮ繝懊け繧ｻ繝ｫ謨ｰ繧堤｢ｺ隱・
            if (CountVoxelsInBlock(blockPos) == 0)
            {
                // 螳悟・縺ｫ遐ｴ螢翫＆繧後◆
                terrainManager.BlockManager.DestroyBlock(blockPos);
                // 螢翫ｌ縺九￠繝ｪ繧ｹ繝医°繧峨・蜑企勁
                if (persistenceManager.partiallyDestroyedBlocks.ContainsKey(blockPos))
                {
                    persistenceManager.partiallyDestroyedBlocks.Remove(blockPos);
                }
            }
            else
            {
                // 驛ｨ蛻・噪縺ｫ遐ｴ螢翫＆繧後◆
                if (!persistenceManager.partiallyDestroyedBlocks.ContainsKey(blockPos))
                {
                    persistenceManager.partiallyDestroyedBlocks[blockPos] = new HashSet<Vector3Int>();
                }
                persistenceManager.partiallyDestroyedBlocks[blockPos].Add(localPos);
            }

            terrainManager?.FluidSimulation?.NotifySolidVoxelRemoved(voxel.worldPosition);
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 繝懊け繧ｻ繝ｫ繧貞ｾｩ蜈・
    /// </summary>
    public bool RestoreVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        Voxel voxel = GetVoxelAt(blockPos, localPos);
        if (voxel != null && !voxel.isActive)
        {
            voxel.isActive = true;
            voxel.health = voxel.maxHealth; // 譛螟ｧ菴灘鴨縺ｫ蠕ｩ蜈・
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
    /// 謖・ｮ壹ヶ繝ｭ繝・け縺ｮ繝懊け繧ｻ繝ｫ繧偵☆縺ｹ縺ｦ蜿門ｾ・
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
    /// 繧｢繧ｯ繝・ぅ繝悶↑繝懊け繧ｻ繝ｫ謨ｰ繧貞叙蠕・
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
    /// 繝懊け繧ｻ繝ｫ繧ｿ繧､繝怜挨縺ｮ邨ｱ險医ｒ蜿門ｾ・
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
    /// 謖・ｮ壹ヶ繝ｭ繝・け縺ｮ繝懊け繧ｻ繝ｫ繧偵け繝ｪ繧｢
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
    /// 縺吶∋縺ｦ縺ｮ繝懊け繧ｻ繝ｫ繝・・繧ｿ繧偵け繝ｪ繧｢
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
    /// 繝・ヰ繝・げ諠・ｱ繧貞叙蠕・
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

