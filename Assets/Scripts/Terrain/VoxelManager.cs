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
    [SerializeField] private List<VoxelData> trackedVoxels = new List<VoxelData>();
    
    /// <summary>
    /// ボクセルデータ構造
    /// </summary>
    [System.Serializable]
    public class VoxelData
    {
        public Vector3Int chunkPosition;     // 所属チャンク座標
        public Vector3Int localPosition;    // チャンク内でのローカル座標
        public Vector3 worldPosition;       // ワールド座標
        public bool isActive;               // ボクセルがアクティブかどうか
        public int health;                  // ボクセルの耐久値
        public VoxelType voxelType;         // ボクセルタイプ
        public float lastModifiedTime;      // 最後に変更された時間
        
        public VoxelData(Vector3Int chunkPos, Vector3Int localPos, Vector3 worldPos, int hp, VoxelType type)
        {
            chunkPosition = chunkPos;
            localPosition = localPos;
            worldPosition = worldPos;
            isActive = true;
            health = hp;
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
    public void RegisterVoxelsFromPattern(bool[,,] pattern, Vector3Int chunkPos, TerrainSettings settings)
    {
        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Registering voxels for chunk {chunkPos}");
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
                        Vector3 worldPos = CalculateWorldPosition(chunkPos, localPos, settings);
                        
                        VoxelData voxelData = new VoxelData(
                            chunkPos, 
                            localPos, 
                            worldPos, 
                            settings.voxelHp, 
                            DetermineVoxelType(chunkPos, localPos)
                        );
                        
                        trackedVoxels.Add(voxelData);
                    }
                }
            }
        }
        
        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Registered {CountVoxelsInChunk(chunkPos)} voxels for chunk {chunkPos}");
        }
    }
    
    /// <summary>
    /// ワールド座標を計算
    /// </summary>
    private Vector3 CalculateWorldPosition(Vector3Int chunkPos, Vector3Int localPos, TerrainSettings settings)
    {
        Vector3 chunkWorldPos = new Vector3(
            chunkPos.x * settings.chunkSize,
            chunkPos.y * settings.chunkSize,
            chunkPos.z * settings.chunkSize
        );
        
        float voxelUnit = settings.chunkSize / settings.voxelSize;
        Vector3 localWorldPos = new Vector3(
            localPos.x * voxelUnit,
            localPos.y * voxelUnit,
            localPos.z * voxelUnit
        );
        
        return chunkWorldPos + localWorldPos;
    }
    
    /// <summary>
    /// ボクセルタイプを決定
    /// </summary>
    private VoxelType DetermineVoxelType(Vector3Int chunkPos, Vector3Int localPos)
    {
        // デフォルトは標準タイプ
        // 将来的に位置やランダム要素に基づいてタイプを決定
        return VoxelType.Standard;
    }
    
    /// <summary>
    /// 指定座標のボクセルデータを取得
    /// </summary>
    public VoxelData GetVoxelAt(Vector3Int chunkPos, Vector3Int localPos)
    {
        foreach (var voxel in trackedVoxels)
        {
            if (voxel.chunkPosition == chunkPos && voxel.localPosition == localPos && voxel.isActive)
            {
                return voxel;
            }
        }
        return null;
    }
    
    /// <summary>
    /// 指定チャンク内のボクセル数を取得
    /// </summary>
    public int CountVoxelsInChunk(Vector3Int chunkPos)
    {
        int count = 0;
        foreach (var voxel in trackedVoxels)
        {
            if (voxel.chunkPosition == chunkPos && voxel.isActive)
            {
                count++;
            }
        }
        return count;
    }
    
    /// <summary>
    /// ボクセルにダメージを与える
    /// </summary>
    public bool DamageVoxel(Vector3Int chunkPos, Vector3Int localPos, int damage = 1)
    {
        VoxelData voxel = GetVoxelAt(chunkPos, localPos);
        if (voxel != null)
        {
            voxel.health -= damage;
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Damaged voxel at {chunkPos},{localPos} - Health: {voxel.health}");
            }
            
            if (voxel.health <= 0)
            {
                return DestroyVoxel(chunkPos, localPos);
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// ボクセルを破壊
    /// </summary>
    public bool DestroyVoxel(Vector3Int chunkPos, Vector3Int localPos)
    {
        VoxelData voxel = GetVoxelAt(chunkPos, localPos);
        if (voxel != null && voxel.voxelType != VoxelType.Unbreakable)
        {
            voxel.isActive = false;
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Destroyed voxel at {chunkPos},{localPos}");
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// ボクセルを復元
    /// </summary>
    public bool RestoreVoxel(Vector3Int chunkPos, Vector3Int localPos)
    {
        VoxelData voxel = GetVoxelAt(chunkPos, localPos);
        if (voxel != null && !voxel.isActive)
        {
            voxel.isActive = true;
            voxel.health = terrainManager.Settings.voxelHp; // デフォルト体力に復元
            voxel.lastModifiedTime = Time.time;
            
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Restored voxel at {chunkPos},{localPos}");
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 指定チャンクのボクセルをすべて取得
    /// </summary>
    public List<VoxelData> GetVoxelsInChunk(Vector3Int chunkPos)
    {
        List<VoxelData> chunkVoxels = new List<VoxelData>();
        
        foreach (var voxel in trackedVoxels)
        {
            if (voxel.chunkPosition == chunkPos)
            {
                chunkVoxels.Add(voxel);
            }
        }
        
        return chunkVoxels;
    }
    
    /// <summary>
    /// アクティブなボクセル数を取得
    /// </summary>
    public int GetActiveVoxelCount()
    {
        int count = 0;
        foreach (var voxel in trackedVoxels)
        {
            if (voxel.isActive) count++;
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
        
        foreach (var voxel in trackedVoxels)
        {
            if (voxel.isActive)
            {
                stats[voxel.voxelType]++;
            }
        }
        
        return stats;
    }
    
    /// <summary>
    /// 指定チャンクのボクセルをクリア
    /// </summary>
    public void ClearVoxelsInChunk(Vector3Int chunkPos)
    {
        trackedVoxels.RemoveAll(v => v.chunkPosition == chunkPos);
        
        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Cleared voxels for chunk {chunkPos}");
        }
    }
    
    /// <summary>
    /// すべてのボクセルデータをクリア
    /// </summary>
    public void ClearAllVoxels()
    {
        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Clearing all {trackedVoxels.Count} voxels");
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
        
        return $"VoxelManager - Total: {trackedVoxels.Count}, Active: {activeCount}, Types: " +
               $"Standard:{stats[VoxelType.Standard]}, Reinforced:{stats[VoxelType.Reinforced]}, " +
               $"Fragile:{stats[VoxelType.Fragile]}, Unbreakable:{stats[VoxelType.Unbreakable]}, " +
               $"Special:{stats[VoxelType.Special]}";
    }
    
    void OnDestroy()
    {
        ClearAllVoxels();
    }
}
