using UnityEngine;
using System.Collections.Generic;

public class VoxelManager : MonoBehaviour
{
    [Header("Voxel Management Configuration")]
    [SerializeField] private bool showVoxelDebugInfo = false;

    private readonly Dictionary<Vector3Int, Dictionary<Vector3Int, Voxel>> trackedVoxels =
        new Dictionary<Vector3Int, Dictionary<Vector3Int, Voxel>>();

    private TerrainManager terrainManager;
    private readonly SolidificationCandidateIndex candidateIndex = new SolidificationCandidateIndex();

    public SolidificationCandidateIndex CandidateIndex => candidateIndex;

    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;
        candidateIndex.Initialize(this, manager.Settings);

        if (showVoxelDebugInfo)
        {
            Debug.Log("VoxelManager: Initialized with TerrainManager");
        }
    }

    public void RegisterVoxelsFromPattern(bool[,,] pattern, Vector3Int blockPos, Vector3 blockWorldPos, BlockData data, float blockSize, int voxelsPerBlock)
    {
        if (!trackedVoxels.ContainsKey(blockPos))
        {
            trackedVoxels[blockPos] = new Dictionary<Vector3Int, Voxel>();
        }

        var blockVoxels = trackedVoxels[blockPos];

        for (int x = 0; x < pattern.GetLength(0); x++)
        {
            for (int y = 0; y < pattern.GetLength(1); y++)
            {
                for (int z = 0; z < pattern.GetLength(2); z++)
                {
                    if (!pattern[x, y, z]) continue;

                    Vector3Int localPos = new Vector3Int(x, y, z);
                    Vector3 worldPos = CalculateWorldPosition(blockWorldPos, localPos, blockSize, voxelsPerBlock);
                    blockVoxels[localPos] = new Voxel(
                        blockPos,
                        localPos,
                        worldPos,
                        data != null ? data.voxelHp : 1,
                        DetermineVoxelType(blockPos, localPos),
                        data
                    );
                }
            }
        }

        ApplyPersistedVoxelOverrides(blockPos, blockWorldPos, data, blockSize, voxelsPerBlock);
        ApplyPersistedDestroyedState(blockPos);

        candidateIndex.RebuildBlock(blockPos);

        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Registered {CountVoxelsInBlock(blockPos)} voxels for block {blockPos}");
        }
    }

    public Vector3 CalculateWorldPosition(Vector3 blockWorldPos, Vector3Int localPos, float blockSize, int voxelsPerBlock)
    {
        float voxelUnit = blockSize / voxelsPerBlock;
        Vector3 localOffset = new Vector3(
            (localPos.x - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.y - voxelsPerBlock / 2f + 0.5f) * voxelUnit,
            (localPos.z - voxelsPerBlock / 2f + 0.5f) * voxelUnit
        );

        return blockWorldPos + localOffset;
    }

    public Vector3 CalculateWorldPosition(Vector3Int blockPos, Vector3Int localPos)
    {
        if (terrainManager == null)
        {
            return Vector3.zero;
        }

        return CalculateWorldPosition(GetBlockWorldPosition(blockPos), localPos, terrainManager.Settings.blockSize, terrainManager.Settings.voxelsPerBlock);
    }

    private Vector3 GetBlockWorldPosition(Vector3Int blockPos)
    {
        var settings = terrainManager.Settings;
        return new Vector3(
            blockPos.x * settings.blockSize,
            blockPos.y * settings.blockSize,
            settings.center.z + blockPos.z * settings.blockSize
        );
    }

    private void ApplyPersistedVoxelOverrides(Vector3Int blockPos, Vector3 blockWorldPos, BlockData defaultData, float blockSize, int voxelsPerBlock)
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (!persistenceManager.voxelCellOverrides.TryGetValue(blockPos, out var overrides))
        {
            return;
        }

        foreach (var pair in overrides)
        {
            Vector3Int localPos = pair.Key;
            if (!IsLocalPositionInBounds(localPos, voxelsPerBlock)) continue;

            Voxel voxel = GetVoxelIncludingInactive(blockPos, localPos);
            if (voxel == null)
            {
                Vector3 worldPos = CalculateWorldPosition(blockWorldPos, localPos, blockSize, voxelsPerBlock);
                voxel = new Voxel(
                    blockPos,
                    localPos,
                    worldPos,
                    Mathf.Max(1, pair.Value.maxHealth),
                    DetermineVoxelType(blockPos, localPos),
                    defaultData
                );
                trackedVoxels[blockPos][localPos] = voxel;
            }

            BlockData resolvedData = ResolveBlockData(pair.Value.blockDataName, defaultData);
            voxel.ApplyCellData(pair.Value, resolvedData);
        }
    }

    private void ApplyPersistedDestroyedState(Vector3Int blockPos)
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        bool hasActiveOverride = false;

        if (persistenceManager.voxelCellOverrides.TryGetValue(blockPos, out var overrides))
        {
            foreach (var cell in overrides.Values)
            {
                if (cell.isActive)
                {
                    hasActiveOverride = true;
                    break;
                }
            }
        }

        if (persistenceManager.destroyedBlockPositions.Contains(blockPos) && !hasActiveOverride)
        {
            if (trackedVoxels.TryGetValue(blockPos, out var block))
            {
                foreach (var voxel in block.Values)
                {
                    voxel.isActive = false;
                }
            }
            return;
        }

        if (persistenceManager.partiallyDestroyedBlocks.TryGetValue(blockPos, out var destroyedVoxels))
        {
            foreach (var localVoxelPos in destroyedVoxels)
            {
                Voxel voxel = GetVoxelIncludingInactive(blockPos, localVoxelPos);
                if (voxel != null)
                {
                    voxel.isActive = false;
                }
            }
        }
    }

    private bool IsLocalPositionInBounds(Vector3Int localPos, int voxelsPerBlock)
    {
        return localPos.x >= 0 && localPos.x < voxelsPerBlock &&
               localPos.y >= 0 && localPos.y < voxelsPerBlock &&
               localPos.z >= 0 && localPos.z < voxelsPerBlock;
    }

    private VoxelType DetermineVoxelType(Vector3Int blockPos, Vector3Int localPos)
    {
        return VoxelType.Standard;
    }

    public Voxel GetVoxelAt(Vector3Int blockPos, Vector3Int localPos)
    {
        Voxel voxel = GetVoxelIncludingInactive(blockPos, localPos);
        return voxel != null && voxel.isActive ? voxel : null;
    }

    public Voxel GetVoxelIncludingInactive(Vector3Int blockPos, Vector3Int localPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block) && block.TryGetValue(localPos, out var voxel))
        {
            return voxel;
        }
        return null;
    }

    public int CountVoxelsInBlock(Vector3Int blockPos)
    {
        if (!trackedVoxels.TryGetValue(blockPos, out var block)) return 0;

        int count = 0;
        foreach (var voxel in block.Values)
        {
            if (voxel.isActive) count++;
        }
        return count;
    }

    public bool DamageVoxel(Vector3Int blockPos, Vector3Int localPos, int damage = 1)
    {
        Voxel voxel = GetVoxelAt(blockPos, localPos);
        if (voxel == null) return false;

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

        PersistOverrideIfNeeded(voxel);
        return false;
    }

    public bool DestroyVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        Voxel voxel = GetVoxelAt(blockPos, localPos);
        if (voxel == null || voxel.voxelType == VoxelType.Unbreakable) return false;

        voxel.isActive = false;
        voxel.lastModifiedTime = Time.time;
        PersistOverrideIfNeeded(voxel);
        terrainManager?.FluidManager?.NotifySolidVoxelRemoved(voxel.worldPosition);
        candidateIndex.RefreshCellAndNeighbors(blockPos, localPos);

        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Destroyed voxel at {blockPos},{localPos}");
        }

        if (CountVoxelsInBlock(blockPos) == 0)
        {
            terrainManager.BlockManager.DestroyBlock(blockPos);
        }

        SyncPersistenceForBlock(blockPos);
        return true;
    }

    public bool RestoreVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        Voxel voxel = GetVoxelIncludingInactive(blockPos, localPos);
        if (voxel == null || voxel.isActive) return false;

        voxel.isActive = true;
        voxel.health = voxel.maxHealth;
        voxel.lastModifiedTime = Time.time;
        PersistOverrideIfNeeded(voxel);
        SyncPersistenceForBlock(blockPos);
        candidateIndex.RefreshCellAndNeighbors(blockPos, localPos);

        if (showVoxelDebugInfo)
        {
            Debug.Log($"VoxelManager: Restored voxel at {blockPos},{localPos}");
        }

        return true;
    }

    public bool SetVoxelCell(Vector3Int blockPos, Vector3Int localPos, BlockData data, bool active = true, int healthOverride = -1)
    {
        if (terrainManager == null || data == null) return false;
        if (!NormalizeVoxelPosition(ref blockPos, ref localPos)) return false;

        if (!trackedVoxels.ContainsKey(blockPos))
        {
            trackedVoxels[blockPos] = new Dictionary<Vector3Int, Voxel>();
        }

        Voxel voxel = GetVoxelIncludingInactive(blockPos, localPos);
        if (voxel == null)
        {
            voxel = new Voxel(
                blockPos,
                localPos,
                CalculateWorldPosition(blockPos, localPos),
                data.voxelHp,
                DetermineVoxelType(blockPos, localPos),
                data
            );
            trackedVoxels[blockPos][localPos] = voxel;
        }

        voxel.SetBlockData(data);
        voxel.isActive = active;
        voxel.maxHealth = Mathf.Max(1, data.voxelHp);
        voxel.health = healthOverride > 0 ? Mathf.Clamp(healthOverride, 1, voxel.maxHealth) : voxel.maxHealth;
        voxel.lastModifiedTime = Time.time;

        PersistVoxelOverride(voxel);
        SyncPersistenceForBlock(blockPos);
        candidateIndex.RefreshCellAndNeighbors(blockPos, localPos);
        return true;
    }

    public bool IsVoxelCellEmpty(Vector3Int blockPos, Vector3Int localPos)
    {
        if (!NormalizeVoxelPosition(ref blockPos, ref localPos)) return false;
        Voxel voxel = GetVoxelIncludingInactive(blockPos, localPos);
        return voxel == null || !voxel.isActive;
    }

    public bool HasActiveAdjacentVoxel(Vector3Int blockPos, Vector3Int localPos)
    {
        Vector3Int[] directions =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        foreach (Vector3Int direction in directions)
        {
            Vector3Int neighborBlock = blockPos;
            Vector3Int neighborLocal = localPos + direction;
            if (!NormalizeVoxelPosition(ref neighborBlock, ref neighborLocal)) continue;

            if (GetVoxelAt(neighborBlock, neighborLocal) != null)
            {
                return true;
            }
        }

        return false;
    }

    public bool NormalizeVoxelPosition(ref Vector3Int blockPos, ref Vector3Int localPos)
    {
        if (terrainManager == null) return false;

        int voxelsPerBlock = terrainManager.Settings.voxelsPerBlock;
        int blockX = blockPos.x;
        int blockY = blockPos.y;
        int localX = localPos.x;
        int localY = localPos.y;

        NormalizeAxis(ref blockX, ref localX, voxelsPerBlock);
        NormalizeAxis(ref blockY, ref localY, voxelsPerBlock);

        blockPos.x = blockX;
        blockPos.y = blockY;
        localPos.x = localX;
        localPos.y = localY;

        if (localPos.z < 0 || localPos.z >= voxelsPerBlock)
        {
            return false;
        }

        return true;
    }

    private void NormalizeAxis(ref int blockValue, ref int localValue, int voxelsPerBlock)
    {
        while (localValue < 0)
        {
            localValue += voxelsPerBlock;
            blockValue -= 1;
        }

        while (localValue >= voxelsPerBlock)
        {
            localValue -= voxelsPerBlock;
            blockValue += 1;
        }
    }

    public List<Voxel> GetVoxelsInBlock(Vector3Int blockPos)
    {
        if (trackedVoxels.TryGetValue(blockPos, out var block))
        {
            return new List<Voxel>(block.Values);
        }
        return new List<Voxel>();
    }

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

    public void ClearVoxelsInBlock(Vector3Int blockPos)
    {
        if (trackedVoxels.ContainsKey(blockPos))
        {
            trackedVoxels.Remove(blockPos);
            candidateIndex.RemoveBlock(blockPos);
            if (showVoxelDebugInfo)
            {
                Debug.Log($"VoxelManager: Cleared voxels for block {blockPos}");
            }
        }
    }

    public void ClearAllVoxels()
    {
        if (showVoxelDebugInfo)
        {
            int totalVoxels = 0;
            foreach (var block in trackedVoxels.Values)
            {
                totalVoxels += block.Count;
            }
            Debug.Log($"VoxelManager: Clearing all {totalVoxels} voxels from {trackedVoxels.Count} blocks");
        }

        trackedVoxels.Clear();
        candidateIndex.Clear();
    }

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

    public void SyncPersistenceForBlock(Vector3Int blockPos)
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (!trackedVoxels.TryGetValue(blockPos, out var block))
        {
            persistenceManager.destroyedBlockPositions.Remove(blockPos);
            persistenceManager.partiallyDestroyedBlocks.Remove(blockPos);
            return;
        }

        int activeCount = 0;
        HashSet<Vector3Int> inactivePositions = new HashSet<Vector3Int>();

        foreach (var pair in block)
        {
            if (pair.Value.isActive)
            {
                activeCount++;
            }
            else
            {
                inactivePositions.Add(pair.Key);
            }
        }

        if (activeCount == 0)
        {
            persistenceManager.destroyedBlockPositions.Add(blockPos);
            persistenceManager.partiallyDestroyedBlocks.Remove(blockPos);
            return;
        }

        persistenceManager.destroyedBlockPositions.Remove(blockPos);

        if (inactivePositions.Count > 0)
        {
            persistenceManager.partiallyDestroyedBlocks[blockPos] = inactivePositions;
        }
        else
        {
            persistenceManager.partiallyDestroyedBlocks.Remove(blockPos);
        }
    }

    private void PersistVoxelOverride(Voxel voxel)
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (!persistenceManager.voxelCellOverrides.ContainsKey(voxel.blockPosition))
        {
            persistenceManager.voxelCellOverrides[voxel.blockPosition] = new Dictionary<Vector3Int, VoxelCellData>();
        }

        persistenceManager.voxelCellOverrides[voxel.blockPosition][voxel.localPosition] = new VoxelCellData(voxel);
    }

    private void PersistOverrideIfNeeded(Voxel voxel)
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager.voxelCellOverrides.TryGetValue(voxel.blockPosition, out var overrides) &&
            overrides.ContainsKey(voxel.localPosition))
        {
            overrides[voxel.localPosition] = new VoxelCellData(voxel);
        }
    }

    private BlockData ResolveBlockData(string blockDataName, BlockData fallback)
    {
        if (!string.IsNullOrEmpty(blockDataName) && terrainManager != null && terrainManager.TerrainDataManager != null)
        {
            BlockData resolved = terrainManager.TerrainDataManager.GetBlockDataByName(blockDataName);
            if (resolved != null) return resolved;
        }

        return fallback;
    }

    void OnDestroy()
    {
        ClearAllVoxels();
    }
}
