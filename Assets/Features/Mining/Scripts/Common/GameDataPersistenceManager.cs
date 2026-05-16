using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ToolSlotPersistenceData
{
    public string slotId;
    public MiningTool tool;
}

[System.Serializable]
public class FacilityUpgradeProgressRecord
{
    public string upgradeId;
    public int level;
}

/// <summary>
/// ゲームのセッション中、シーンをまたいでデータを保持するクラス。
/// シード値や破壊されたブロックの情報などを管理します。
/// </summary>
public class GameDataPersistenceManager : MonoBehaviour
{
    // シングルトンインスタンス
    private static GameDataPersistenceManager _instance;
    public static GameDataPersistenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameDataPersistenceManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameDataPersistenceManager");
                    _instance = go.AddComponent<GameDataPersistenceManager>();
                }
            }
            return _instance;
        }
    }

    // --- Events ---
    public static event Action OnFacilityUpgradesChanged;

    // --- 永続化するデータ ---

    [Header("地形データ")]
    public int terrainSeed;
    public bool hasInitializedSeed = false; // シードが初期化されたかどうか

    [Header("破壊済みブロック")]
    public HashSet<Vector3Int> destroyedBlockPositions = new HashSet<Vector3Int>();
    public Dictionary<Vector3Int, HashSet<Vector3Int>> partiallyDestroyedBlocks = new Dictionary<Vector3Int, HashSet<Vector3Int>>();

    [Header("プレイヤーデータ")]
    public Dictionary<ResourceType, int> storedResources = new Dictionary<ResourceType, int>();

    [Header("ドロップアイテムデータ")]
    public List<DroppedItemData> droppedItems = new List<DroppedItemData>();

    [Header("Voxel Cell Data")]
    public Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> voxelCellOverrides = new Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>>();
    public List<SolidifiedVoxelRecord> solidifiedVoxelHistory = new List<SolidifiedVoxelRecord>();

    [Header("Facility Upgrade Data")]
    public List<FacilityUpgradeProgressRecord> facilityUpgradeProgress = new List<FacilityUpgradeProgressRecord>();

    [Header("Tool Inventory Data")]
    public bool hasToolInventoryData = false;
    public List<ToolSlotPersistenceData> toolSlots = new List<ToolSlotPersistenceData>();
    public string mainToolSlotId = "";
    public string subToolSlotId = "";
    
    public int GetFacilityUpgradeLevel(string upgradeId, int defaultLevel)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            Debug.LogError("GameDataPersistenceManager: upgradeId is not configured.");
            return defaultLevel;
        }

        if (facilityUpgradeProgress == null)
        {
            Debug.LogError("GameDataPersistenceManager: facilityUpgradeProgress is not configured.");
            return defaultLevel;
        }

        for (int i = 0; i < facilityUpgradeProgress.Count; i++)
        {
            FacilityUpgradeProgressRecord record = facilityUpgradeProgress[i];
            if (record != null && record.upgradeId == upgradeId)
            {
                return record.level;
            }
        }

        return defaultLevel;
    }

    public bool SetFacilityUpgradeLevel(string upgradeId, int level)
    {
        if (!CanSetFacilityUpgradeLevel(upgradeId, level))
        {
            return false;
        }

        for (int i = 0; i < facilityUpgradeProgress.Count; i++)
        {
            FacilityUpgradeProgressRecord record = facilityUpgradeProgress[i];
            if (record == null)
            {
                Debug.LogError($"GameDataPersistenceManager: facilityUpgradeProgress contains a null record at index {i}.");
                return false;
            }

            if (record.upgradeId == upgradeId)
            {
                record.level = level;
                NotifyFacilityUpgradesChanged();
                return true;
            }
        }

        facilityUpgradeProgress.Add(new FacilityUpgradeProgressRecord
        {
            upgradeId = upgradeId,
            level = level
        });
        NotifyFacilityUpgradesChanged();
        return true;
    }

    public bool CanSetFacilityUpgradeLevel(string upgradeId, int level)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            Debug.LogError("GameDataPersistenceManager: upgradeId is not configured.");
            return false;
        }

        if (level < 0)
        {
            Debug.LogError($"GameDataPersistenceManager: level for '{upgradeId}' must not be negative.");
            return false;
        }

        if (facilityUpgradeProgress == null)
        {
            Debug.LogError("GameDataPersistenceManager: facilityUpgradeProgress is not configured.");
            return false;
        }

        for (int i = 0; i < facilityUpgradeProgress.Count; i++)
        {
            if (facilityUpgradeProgress[i] == null)
            {
                Debug.LogError($"GameDataPersistenceManager: facilityUpgradeProgress contains a null record at index {i}.");
                return false;
            }
        }

        return true;
    }

    public void NotifyFacilityUpgradesChanged()
    {
        OnFacilityUpgradesChanged?.Invoke();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            GameDataPersistenceManager previousInstance = _instance;
            CopyRuntimeStateFrom(previousInstance);

            if (previousInstance.gameObject == gameObject)
            {
                Destroy(previousInstance);
            }
            else
            {
                Destroy(previousInstance.gameObject);
            }
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void CopyRuntimeStateFrom(GameDataPersistenceManager source)
    {
        if (source == null)
        {
            Debug.LogError("GameDataPersistenceManager: source instance is not configured.", this);
            return;
        }

        terrainSeed = source.terrainSeed;
        hasInitializedSeed = source.hasInitializedSeed;
        destroyedBlockPositions = source.destroyedBlockPositions != null
            ? new HashSet<Vector3Int>(source.destroyedBlockPositions)
            : new HashSet<Vector3Int>();
        partiallyDestroyedBlocks = CopyPartiallyDestroyedBlocks(source.partiallyDestroyedBlocks);
        storedResources = source.storedResources != null
            ? new Dictionary<ResourceType, int>(source.storedResources)
            : new Dictionary<ResourceType, int>();
        droppedItems = source.droppedItems != null
            ? new List<DroppedItemData>(source.droppedItems)
            : new List<DroppedItemData>();
        voxelCellOverrides = CopyVoxelCellOverrides(source.voxelCellOverrides);
        solidifiedVoxelHistory = source.solidifiedVoxelHistory != null
            ? new List<SolidifiedVoxelRecord>(source.solidifiedVoxelHistory)
            : new List<SolidifiedVoxelRecord>();
        facilityUpgradeProgress = CopyFacilityUpgradeProgress(source.facilityUpgradeProgress);
        hasToolInventoryData = source.hasToolInventoryData;
        toolSlots = CopyToolSlots(source.toolSlots);
        mainToolSlotId = source.mainToolSlotId;
        subToolSlotId = source.subToolSlotId;
    }

    private Dictionary<Vector3Int, HashSet<Vector3Int>> CopyPartiallyDestroyedBlocks(
        Dictionary<Vector3Int, HashSet<Vector3Int>> source)
    {
        Dictionary<Vector3Int, HashSet<Vector3Int>> copy = new Dictionary<Vector3Int, HashSet<Vector3Int>>();
        if (source == null)
        {
            return copy;
        }

        foreach (KeyValuePair<Vector3Int, HashSet<Vector3Int>> entry in source)
        {
            copy.Add(entry.Key, entry.Value != null ? new HashSet<Vector3Int>(entry.Value) : new HashSet<Vector3Int>());
        }

        return copy;
    }

    private Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> CopyVoxelCellOverrides(
        Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> source)
    {
        Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> copy =
            new Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>>();
        if (source == null)
        {
            return copy;
        }

        foreach (KeyValuePair<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> entry in source)
        {
            copy.Add(entry.Key, entry.Value != null
                ? new Dictionary<Vector3Int, VoxelCellData>(entry.Value)
                : new Dictionary<Vector3Int, VoxelCellData>());
        }

        return copy;
    }

    private List<FacilityUpgradeProgressRecord> CopyFacilityUpgradeProgress(
        List<FacilityUpgradeProgressRecord> source)
    {
        List<FacilityUpgradeProgressRecord> copy = new List<FacilityUpgradeProgressRecord>();
        if (source == null)
        {
            return copy;
        }

        for (int i = 0; i < source.Count; i++)
        {
            FacilityUpgradeProgressRecord record = source[i];
            if (record == null)
            {
                Debug.LogError($"GameDataPersistenceManager: facilityUpgradeProgress contains a null record at index {i}.", this);
                continue;
            }

            copy.Add(new FacilityUpgradeProgressRecord
            {
                upgradeId = record.upgradeId,
                level = record.level
            });
        }

        return copy;
    }

    private List<ToolSlotPersistenceData> CopyToolSlots(List<ToolSlotPersistenceData> source)
    {
        List<ToolSlotPersistenceData> copy = new List<ToolSlotPersistenceData>();
        if (source == null)
        {
            return copy;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ToolSlotPersistenceData record = source[i];
            if (record == null)
            {
                Debug.LogError($"GameDataPersistenceManager: toolSlots contains a null record at index {i}.", this);
                continue;
            }

            copy.Add(new ToolSlotPersistenceData
            {
                slotId = record.slotId,
                tool = record.tool
            });
        }

        return copy;
    }
}
