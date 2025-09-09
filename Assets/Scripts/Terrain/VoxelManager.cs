using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ボクセル管理クラス
/// 個々のボクセルレベルでの詳細管理を担当
/// </summary>
public class VoxelManager : MonoBehaviour
{
    [Header("Voxel Management Configuration")]
    [SerializeField] private bool showVoxelDebugInfo = false;
    
    // データ構造をListからDictionaryに変更し、ブロック座標とローカル座標でボクセルデータを管理
    // これにより、O(n)の検索がO(1)になり、パフォーマンスが大幅に向上
    private Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelData>> trackedVoxels = new Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelData>>();
    
    /// <summary>
    /// ボクセルデータ構造
    /// </summary>
    [System.Serializable]
    public class VoxelData
    {
        public Vector3Int blockPosition;     // 所属ブロック座標
        public Vector3Int localPosition;    // ブロック内でのローカル座標
        public Vector3 worldPosition;       // ワールド座標
        public bool isActive;               // ボクセルがアクティブかどうか
        public int health;                  // ボクセルの耐久値
        public int maxHealth;               // ボクセルの最大耐久値
        public VoxelType voxelType;         // ボクセルタイプ
        public float lastModifiedTime;      // 最後に変更された時間
        
        public VoxelData(Vector3Int blockPos, Vector3Int localPos, Vector3 worldPos, int hp, VoxelType type)
        {
            blockPosition = blockPos;
            localPosition = localPos;
            worldPosition = worldPos;
            isActive = true;
            health = hp;
            maxHealth = hp; // 最大HPも初期HPと同じに設定
            voxelType = type;
            lastModifiedTime = Time.time;
        }
    }
    
    /// <summary>
    /// ボクセルタイプ列挙型
    /// </summary>
    public enum VoxelType
    {
        Standard,      // 標準ボクセル
        Reinforced,    // 強化ボクセル
        Fragile,       // 脆弱ボクセル
        Unbreakable,   // 破壊不能ボクセル
        Special        // 特殊ボクセル
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
        
        if (showVoxelDebugInfo)
        {
            Debug.Log("VoxelManager: Initialized with TerrainManager");
        }
    }
    
    /// <summary>
    /// ボクセルパターンからボクセルデータを作成
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
                        
                        VoxelData voxelData = new VoxelData(
                            blockPos, 
                            localPos, 
                            worldPos, 
                            data.voxelHp, 
                            DetermineVoxelType(blockPos, localPos)
                        );
                        if (!trackedVoxels.ContainsKey(blockPos))
                        {
                            trackedVoxels[blockPos] = new Dictionary<Vector3Int, VoxelData>();
                        }
                        trackedVoxels[blockPos][localPos] = voxelData;
                    }
                }
            }
        }
        
        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Registered {CountVoxelsInBlock(blockPos)} voxels for block {blockPos}");
        }
    }
    
    /// <summary>
    /// ワールド座標を計算
    /// </summary>
    private Vector3 CalculateWorldPosition(Vector3 blockWorldPos, Vector3Int localPos, float blockSize, int voxelsPerBlock)
    {
        float voxelUnit = blockSize / voxelsPerBlock;
        // ブロックの中心からのオフセットとしてボクセルのローカル座標を計算
        Vector3 localOffset = new Vector3(
            (localPos.x - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.y - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.z - voxelsPerBlock / 2f + 0.5f) * voxelUnit
        );
        
        return blockWorldPos + localOffset;
    }
    
    /// <summary>
    /// ボクセルタイプを決定
    /// </summary>
    private VoxelType DetermineVoxelType(Vector3Int blockPos, Vector3Int localPos)
    {
        // デフォルトは標準タイプ
        // 将来的に位置やランダム要素に基づいてタイプを決定
        return VoxelType.Standard;
    }
    
    /// <summary>
    /// 指定座標のボクセルデータを取得
    /// </summary>
    public VoxelData GetVoxelAt(Vector3Int blockPos, Vector3Int localPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block) && block.TryGetValue(localPos, out var voxel))
        {
            return voxel.isActive ? voxel : null;
        }
        return null;
    }
    
    /// <summary>
    /// 指定ブロック内のボクセル数を取得
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
    /// ボクセルにダメージを与える
    /// </summary>
    public bool DamageVoxel(Vector3Int blockPos, Vector3Int localPos, int damage = 1)
    {
        VoxelData voxel = GetVoxelAt(blockPos, localPos);
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
    /// ボクセルを破壊
    /// </summary>
    public bool DestroyVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        VoxelData voxel = GetVoxelAt(blockPos, localPos);
        if (voxel != null && voxel.voxelType != VoxelType.Unbreakable)
        {
            voxel.isActive = false;
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Destroyed voxel at {blockPos},{localPos}");
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// ボクセルを復元
    /// </summary>
    public bool RestoreVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        VoxelData voxel = GetVoxelAt(blockPos, localPos);
        if (voxel != null && !voxel.isActive)
        {
            voxel.isActive = true;
            voxel.health = voxel.maxHealth; // 最大体力に復元
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
    /// 指定ブロックのボクセルをすべて取得
    /// </summary>
    public List<VoxelData> GetVoxelsInBlock(Vector3Int blockPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block))
        {
            return new List<VoxelData>(block.Values);
        }
        return new List<VoxelData>();
    }
    
    /// <summary>
    /// アクティブなボクセル数を取得
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
    /// ボクセルタイプ別の統計を取得
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
    /// 指定ブロックのボクセルをクリア
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
    /// すべてのボクセルデータをクリア
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
    /// デバッグ情報を取得
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
