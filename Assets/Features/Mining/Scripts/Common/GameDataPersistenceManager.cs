using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using Unity.Profiling;

[System.Serializable]
public class ToolSlotPersistenceData
{
    public string slotId;
    public string toolId;
    public MiningTool tool;
}

[System.Serializable]
public class FacilityUpgradeProgressRecord
{
    public string upgradeId;
    public int level;
}

[System.Serializable]
public class TorchPlacementData
{
    public Vector3Int blockPosition;
    public Vector3Int localVoxelPosition;
}

/// <summary>
/// ゲームのセッション中、シーンをまたいでデータを保持するクラス。
/// シード値や破壊されたブロックの情報などを管理します。
/// </summary>
public class GameDataPersistenceManager : MonoBehaviour
{
    private const int CurrentSaveVersion = 1;
    private const string SaveFileName = "momodig_save_v1.json";

    private static readonly ProfilerMarker LoadFromDiskMarker =
        new ProfilerMarker("GameDataPersistenceManager.LoadFromDisk");
    private static readonly ProfilerMarker SaveToDiskMarker =
        new ProfilerMarker("GameDataPersistenceManager.SaveToDisk");

    // シングルトンインスタンス
    private static GameDataPersistenceManager _instance;
    public static GameDataPersistenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("GameDataPersistenceManager.Instance is not initialized. Place GameDataPersistenceManager in the persistent scene.");
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

    [Header("Storage Tool Data")]
    public List<string> ownedToolIds = new List<string>();

    [Header("Tool Inventory Data")]
    public bool hasToolInventoryData = false;
    public List<ToolSlotPersistenceData> toolSlots = new List<ToolSlotPersistenceData>();
    public string mainToolSlotId = "";
    public string subToolSlotId = "";

    [Header("Torch Placement Data")]
    public List<TorchPlacementData> torchPlacements = new List<TorchPlacementData>();

    [Header("Mining Lighting Cache")]
    public MiningLightingCacheData miningLightingCache;

    [Header("Disk Save")]
    [SerializeField] private bool loadSaveOnAwake = true;

    public bool HasLoadedSaveFromDisk { get; private set; }
    public bool LastLoadHadSaveFile { get; private set; }
    public bool CanWriteSaveFile => !LastLoadHadSaveFile || HasLoadedSaveFromDisk;
    public string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    
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
        bool copiedFromPreviousInstance = false;
        if (_instance != null && _instance != this)
        {
            GameDataPersistenceManager previousInstance = _instance;
            CopyRuntimeStateFrom(previousInstance);
            copiedFromPreviousInstance = true;

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
        EnsureRuntimeCollections();

        if (!copiedFromPreviousInstance && loadSaveOnAwake)
        {
            LoadFromDisk();
        }
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
        ownedToolIds = CopyStringList(source.ownedToolIds, "ownedToolIds");
        hasToolInventoryData = source.hasToolInventoryData;
        toolSlots = CopyToolSlots(source.toolSlots);
        mainToolSlotId = source.mainToolSlotId;
        subToolSlotId = source.subToolSlotId;
        torchPlacements = CopyTorchPlacements(source.torchPlacements);
        HasLoadedSaveFromDisk = source.HasLoadedSaveFromDisk;
        LastLoadHadSaveFile = source.LastLoadHadSaveFile;
    }

    public bool LoadFromDisk()
    {
        using (LoadFromDiskMarker.Auto())
        {
            string path = SaveFilePath;
            LastLoadHadSaveFile = File.Exists(path);
            if (!LastLoadHadSaveFile)
            {
                HasLoadedSaveFromDisk = false;
                EnsureRuntimeCollections();
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, new UTF8Encoding(false));
                GameDataSaveFile saveFile = JsonUtility.FromJson<GameDataSaveFile>(json);
                if (saveFile == null || saveFile.data == null)
                {
                    Debug.LogError($"GameDataPersistenceManager: Save file is invalid. path={path}", this);
                    return false;
                }

                if (saveFile.version != CurrentSaveVersion)
                {
                    Debug.LogError(
                        $"GameDataPersistenceManager: Unsupported save version. version={saveFile.version}, expected={CurrentSaveVersion}",
                        this);
                    return false;
                }

                ApplySaveData(saveFile.data);
                HasLoadedSaveFromDisk = true;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"GameDataPersistenceManager: Failed to load save file. path={path}\n{exception}", this);
                return false;
            }
        }
    }

    public bool SaveToDisk()
    {
        using (SaveToDiskMarker.Auto())
        {
            string path = SaveFilePath;
            if (!CanWriteSaveFile)
            {
                Debug.LogError(
                    $"GameDataPersistenceManager: Refused to overwrite save file because the existing save was not loaded successfully. path={path}",
                    this);
                return false;
            }

            if (!HasPersistentProgress())
            {
                return DeleteSaveFiles();
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                Debug.LogError($"GameDataPersistenceManager: Save directory is invalid. path={path}", this);
                return false;
            }

            string tempPath = Path.Combine(directory, $"{SaveFileName}.tmp");
            string backupPath = Path.Combine(directory, $"{SaveFileName}.bak");

            try
            {
                Directory.CreateDirectory(directory);
                GameDataSaveFile saveFile = new GameDataSaveFile
                {
                    version = CurrentSaveVersion,
                    savedAtUtc = DateTime.UtcNow.ToString("O"),
                    data = CaptureSaveData()
                };

                string json = JsonUtility.ToJson(saveFile, true);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"GameDataPersistenceManager: Failed to save game data. path={path}\n{exception}", this);
                return false;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }

    public bool DeleteSaveAndResetRuntimeState()
    {
        bool deleted = DeleteSaveFiles();
        ResetRuntimeState();
        return deleted;
    }

    public void ResetRuntimeState()
    {
        terrainSeed = 0;
        hasInitializedSeed = false;
        destroyedBlockPositions = new HashSet<Vector3Int>();
        partiallyDestroyedBlocks = new Dictionary<Vector3Int, HashSet<Vector3Int>>();
        storedResources = new Dictionary<ResourceType, int>();
        droppedItems = new List<DroppedItemData>();
        voxelCellOverrides = new Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>>();
        solidifiedVoxelHistory = new List<SolidifiedVoxelRecord>();
        facilityUpgradeProgress = new List<FacilityUpgradeProgressRecord>();
        ownedToolIds = new List<string>();
        hasToolInventoryData = false;
        toolSlots = new List<ToolSlotPersistenceData>();
        mainToolSlotId = string.Empty;
        subToolSlotId = string.Empty;
        torchPlacements = new List<TorchPlacementData>();
        miningLightingCache = null;
        HasLoadedSaveFromDisk = false;
        LastLoadHadSaveFile = false;
        NotifyFacilityUpgradesChanged();
    }

    private bool DeleteSaveFiles()
    {
        string path = SaveFilePath;
        string directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            Debug.LogError($"GameDataPersistenceManager: Save directory is invalid. path={path}", this);
            return false;
        }

        string tempPath = Path.Combine(directory, $"{SaveFileName}.tmp");
        string backupPath = Path.Combine(directory, $"{SaveFileName}.bak");
        bool succeeded = true;
        succeeded &= DeleteFileIfExists(path);
        succeeded &= DeleteFileIfExists(tempPath);
        succeeded &= DeleteFileIfExists(backupPath);
        return succeeded;
    }

    private bool DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"GameDataPersistenceManager: Failed to delete save file. path={path}\n{exception}", this);
            return false;
        }
    }

    private bool HasPersistentProgress()
    {
        EnsureRuntimeCollections();
        if (hasInitializedSeed ||
            destroyedBlockPositions.Count > 0 ||
            partiallyDestroyedBlocks.Count > 0 ||
            droppedItems.Count > 0 ||
            voxelCellOverrides.Count > 0 ||
            solidifiedVoxelHistory.Count > 0 ||
            facilityUpgradeProgress.Count > 0 ||
            ownedToolIds.Count > 0 ||
            hasToolInventoryData ||
            torchPlacements.Count > 0)
        {
            return true;
        }

        foreach (KeyValuePair<ResourceType, int> entry in storedResources)
        {
            if (entry.Value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private GameDataSaveData CaptureSaveData()
    {
        EnsureRuntimeCollections();

        GameDataSaveData saveData = new GameDataSaveData
        {
            terrainSeed = terrainSeed,
            hasInitializedSeed = hasInitializedSeed,
            hasToolInventoryData = hasToolInventoryData,
            mainToolSlotId = mainToolSlotId,
            subToolSlotId = subToolSlotId
        };

        saveData.destroyedBlockPositions.AddRange(destroyedBlockPositions);

        foreach (KeyValuePair<Vector3Int, HashSet<Vector3Int>> entry in partiallyDestroyedBlocks)
        {
            PartiallyDestroyedBlockSaveRecord record = new PartiallyDestroyedBlockSaveRecord
            {
                blockPosition = entry.Key
            };
            if (entry.Value != null)
            {
                record.localVoxelPositions.AddRange(entry.Value);
            }

            saveData.partiallyDestroyedBlocks.Add(record);
        }

        foreach (KeyValuePair<ResourceType, int> entry in storedResources)
        {
            saveData.storedResources.Add(new ResourceAmountSaveRecord
            {
                resourceType = entry.Key,
                amount = entry.Value
            });
        }

        saveData.droppedItems.AddRange(droppedItems);

        foreach (KeyValuePair<Vector3Int, Dictionary<Vector3Int, VoxelCellData>> blockEntry in voxelCellOverrides)
        {
            VoxelCellOverrideBlockSaveRecord blockRecord = new VoxelCellOverrideBlockSaveRecord
            {
                blockPosition = blockEntry.Key
            };
            if (blockEntry.Value != null)
            {
                foreach (KeyValuePair<Vector3Int, VoxelCellData> cellEntry in blockEntry.Value)
                {
                    blockRecord.cells.Add(cellEntry.Value);
                }
            }

            saveData.voxelCellOverrides.Add(blockRecord);
        }

        saveData.solidifiedVoxelHistory.AddRange(solidifiedVoxelHistory);
        saveData.facilityUpgradeProgress = CopyFacilityUpgradeProgress(facilityUpgradeProgress);
        saveData.ownedToolIds = CopyStringList(ownedToolIds, "ownedToolIds");

        for (int i = 0; i < toolSlots.Count; i++)
        {
            ToolSlotPersistenceData slot = toolSlots[i];
            if (slot == null)
            {
                Debug.LogError($"GameDataPersistenceManager: toolSlots contains a null record at index {i}.", this);
                continue;
            }

            saveData.toolSlots.Add(new ToolSlotSaveRecord
            {
                slotId = slot.slotId,
                toolId = !string.IsNullOrEmpty(slot.toolId) ? slot.toolId : GetToolId(slot.tool)
            });
        }

        saveData.torchPlacements = CopyTorchPlacements(torchPlacements);
        saveData.miningLightingCache = CopyMiningLightingCache(miningLightingCache);
        return saveData;
    }

    private void ApplySaveData(GameDataSaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogError("GameDataPersistenceManager: saveData is not configured.", this);
            return;
        }

        terrainSeed = saveData.terrainSeed;
        hasInitializedSeed = saveData.hasInitializedSeed;

        destroyedBlockPositions = saveData.destroyedBlockPositions != null
            ? new HashSet<Vector3Int>(saveData.destroyedBlockPositions)
            : new HashSet<Vector3Int>();

        partiallyDestroyedBlocks = new Dictionary<Vector3Int, HashSet<Vector3Int>>();
        if (saveData.partiallyDestroyedBlocks != null)
        {
            for (int i = 0; i < saveData.partiallyDestroyedBlocks.Count; i++)
            {
                PartiallyDestroyedBlockSaveRecord record = saveData.partiallyDestroyedBlocks[i];
                if (record == null)
                {
                    Debug.LogError($"GameDataPersistenceManager: partiallyDestroyedBlocks contains a null record at index {i}.", this);
                    continue;
                }

                partiallyDestroyedBlocks[record.blockPosition] = record.localVoxelPositions != null
                    ? new HashSet<Vector3Int>(record.localVoxelPositions)
                    : new HashSet<Vector3Int>();
            }
        }

        storedResources = new Dictionary<ResourceType, int>();
        if (saveData.storedResources != null)
        {
            for (int i = 0; i < saveData.storedResources.Count; i++)
            {
                ResourceAmountSaveRecord record = saveData.storedResources[i];
                if (record == null)
                {
                    Debug.LogError($"GameDataPersistenceManager: storedResources contains a null record at index {i}.", this);
                    continue;
                }

                storedResources[record.resourceType] = record.amount;
            }
        }

        droppedItems = saveData.droppedItems != null
            ? new List<DroppedItemData>(saveData.droppedItems)
            : new List<DroppedItemData>();

        voxelCellOverrides = new Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>>();
        if (saveData.voxelCellOverrides != null)
        {
            for (int i = 0; i < saveData.voxelCellOverrides.Count; i++)
            {
                VoxelCellOverrideBlockSaveRecord blockRecord = saveData.voxelCellOverrides[i];
                if (blockRecord == null)
                {
                    Debug.LogError($"GameDataPersistenceManager: voxelCellOverrides contains a null record at index {i}.", this);
                    continue;
                }

                Dictionary<Vector3Int, VoxelCellData> cells = new Dictionary<Vector3Int, VoxelCellData>();
                if (blockRecord.cells != null)
                {
                    for (int j = 0; j < blockRecord.cells.Count; j++)
                    {
                        VoxelCellData cellData = blockRecord.cells[j];
                        cellData = RepairLegacyNegativeVoxelOverrideHealth(blockRecord.blockPosition, cellData);
                        cells[cellData.localVoxelPosition] = cellData;
                    }
                }

                voxelCellOverrides[blockRecord.blockPosition] = cells;
            }
        }

        solidifiedVoxelHistory = saveData.solidifiedVoxelHistory != null
            ? new List<SolidifiedVoxelRecord>(saveData.solidifiedVoxelHistory)
            : new List<SolidifiedVoxelRecord>();
        facilityUpgradeProgress = CopyFacilityUpgradeProgress(saveData.facilityUpgradeProgress);
        ownedToolIds = CopyStringList(saveData.ownedToolIds, "ownedToolIds");
        hasToolInventoryData = saveData.hasToolInventoryData;
        toolSlots = new List<ToolSlotPersistenceData>();
        if (saveData.toolSlots != null)
        {
            for (int i = 0; i < saveData.toolSlots.Count; i++)
            {
                ToolSlotSaveRecord slot = saveData.toolSlots[i];
                if (slot == null)
                {
                    Debug.LogError($"GameDataPersistenceManager: toolSlots contains a null save record at index {i}.", this);
                    continue;
                }

                toolSlots.Add(new ToolSlotPersistenceData
                {
                    slotId = slot.slotId,
                    toolId = slot.toolId,
                    tool = null
                });
            }
        }

        mainToolSlotId = saveData.mainToolSlotId;
        subToolSlotId = saveData.subToolSlotId;
        torchPlacements = CopyTorchPlacements(saveData.torchPlacements);
        miningLightingCache = CopyMiningLightingCache(saveData.miningLightingCache);
        EnsureRuntimeCollections();
        NotifyFacilityUpgradesChanged();
    }

    private VoxelCellData RepairLegacyNegativeVoxelOverrideHealth(Vector3Int blockPosition, VoxelCellData cellData)
    {
        if (cellData.health >= 0)
        {
            return cellData;
        }

        int oldHealth = cellData.health;
        cellData.health = 0;
        Debug.LogWarning(
            $"GameDataPersistenceManager: repaired legacy negative voxel override health. block={blockPosition}, local={cellData.localVoxelPosition}, oldHealth={oldHealth}, maxHealth={cellData.maxHealth}.",
            this);
        return cellData;
    }

    private void EnsureRuntimeCollections()
    {
        destroyedBlockPositions ??= new HashSet<Vector3Int>();
        partiallyDestroyedBlocks ??= new Dictionary<Vector3Int, HashSet<Vector3Int>>();
        storedResources ??= new Dictionary<ResourceType, int>();
        droppedItems ??= new List<DroppedItemData>();
        voxelCellOverrides ??= new Dictionary<Vector3Int, Dictionary<Vector3Int, VoxelCellData>>();
        solidifiedVoxelHistory ??= new List<SolidifiedVoxelRecord>();
        facilityUpgradeProgress ??= new List<FacilityUpgradeProgressRecord>();
        ownedToolIds ??= new List<string>();
        toolSlots ??= new List<ToolSlotPersistenceData>();
        torchPlacements ??= new List<TorchPlacementData>();
    }

    public int CalculateLightingTerrainStateHash()
    {
        EnsureRuntimeCollections();

        int hash = 17;
        AddHashValue(ref hash, terrainSeed);
        AddHashValue(ref hash, hasInitializedSeed ? 1 : 0);

        List<Vector3Int> destroyedBlocks = new List<Vector3Int>(destroyedBlockPositions);
        destroyedBlocks.Sort(CompareVector3Int);
        AddHashValue(ref hash, destroyedBlocks.Count);
        for (int i = 0; i < destroyedBlocks.Count; i++)
        {
            AddVectorHash(ref hash, destroyedBlocks[i]);
        }

        List<Vector3Int> partialBlockKeys = new List<Vector3Int>(partiallyDestroyedBlocks.Keys);
        partialBlockKeys.Sort(CompareVector3Int);
        AddHashValue(ref hash, partialBlockKeys.Count);
        for (int i = 0; i < partialBlockKeys.Count; i++)
        {
            Vector3Int blockPosition = partialBlockKeys[i];
            AddVectorHash(ref hash, blockPosition);

            HashSet<Vector3Int> localCells = partiallyDestroyedBlocks[blockPosition];
            List<Vector3Int> sortedLocalCells = localCells != null
                ? new List<Vector3Int>(localCells)
                : new List<Vector3Int>();
            sortedLocalCells.Sort(CompareVector3Int);
            AddHashValue(ref hash, sortedLocalCells.Count);
            for (int j = 0; j < sortedLocalCells.Count; j++)
            {
                AddVectorHash(ref hash, sortedLocalCells[j]);
            }
        }

        List<Vector3Int> overrideBlockKeys = new List<Vector3Int>(voxelCellOverrides.Keys);
        overrideBlockKeys.Sort(CompareVector3Int);
        AddHashValue(ref hash, overrideBlockKeys.Count);
        for (int i = 0; i < overrideBlockKeys.Count; i++)
        {
            Vector3Int blockPosition = overrideBlockKeys[i];
            AddVectorHash(ref hash, blockPosition);

            Dictionary<Vector3Int, VoxelCellData> cellOverrides = voxelCellOverrides[blockPosition];
            List<Vector3Int> localKeys = cellOverrides != null
                ? new List<Vector3Int>(cellOverrides.Keys)
                : new List<Vector3Int>();
            localKeys.Sort(CompareVector3Int);
            AddHashValue(ref hash, localKeys.Count);
            for (int j = 0; j < localKeys.Count; j++)
            {
                Vector3Int localPosition = localKeys[j];
                VoxelCellData cellData = cellOverrides[localPosition];
                AddVectorHash(ref hash, localPosition);
                AddStableStringHash(ref hash, cellData.blockDataName);
                AddHashValue(ref hash, (int)cellData.resourceType);
                AddHashValue(ref hash, cellData.health);
                AddHashValue(ref hash, cellData.maxHealth);
                AddHashValue(ref hash, cellData.isActive ? 1 : 0);
                AddHashValue(ref hash, cellData.useTexture1 ? 1 : 0);
            }
        }

        List<SolidifiedVoxelRecord> solidifiedRecords = new List<SolidifiedVoxelRecord>(solidifiedVoxelHistory);
        solidifiedRecords.Sort(CompareSolidifiedVoxelRecord);
        AddHashValue(ref hash, solidifiedRecords.Count);
        for (int i = 0; i < solidifiedRecords.Count; i++)
        {
            SolidifiedVoxelRecord record = solidifiedRecords[i];
            AddVectorHash(ref hash, record.blockPosition);
            AddVectorHash(ref hash, record.localVoxelPosition);
            AddStableStringHash(ref hash, record.blockDataName);
        }

        return hash;
    }

    public static string GetToolId(MiningTool tool)
    {
        return tool != null ? tool.name : string.Empty;
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
                toolId = !string.IsNullOrEmpty(record.toolId) ? record.toolId : GetToolId(record.tool),
                tool = record.tool
            });
        }

        return copy;
    }

    private List<string> CopyStringList(List<string> source, string fieldName)
    {
        List<string> copy = new List<string>();
        if (source == null)
        {
            return copy;
        }

        for (int i = 0; i < source.Count; i++)
        {
            string value = source[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                Debug.LogError($"GameDataPersistenceManager: {fieldName} contains an empty value at index {i}.", this);
                continue;
            }

            if (copy.Contains(value))
            {
                Debug.LogError($"GameDataPersistenceManager: {fieldName} contains duplicate value '{value}'.", this);
                continue;
            }

            copy.Add(value);
        }

        return copy;
    }

    private List<TorchPlacementData> CopyTorchPlacements(List<TorchPlacementData> source)
    {
        List<TorchPlacementData> copy = new List<TorchPlacementData>();
        if (source == null)
        {
            return copy;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TorchPlacementData record = source[i];
            if (record == null)
            {
                Debug.LogError($"GameDataPersistenceManager: torchPlacements contains a null record at index {i}.", this);
                continue;
            }

            copy.Add(new TorchPlacementData
            {
                blockPosition = record.blockPosition,
                localVoxelPosition = record.localVoxelPosition
            });
        }

        return copy;
    }

    private MiningLightingCacheData CopyMiningLightingCache(MiningLightingCacheData source)
    {
        if (source == null)
        {
            return null;
        }

        MiningLightingCacheData copy = new MiningLightingCacheData
        {
            cacheVersion = source.cacheVersion,
            terrainStateHash = source.terrainStateHash
        };

        if (source.sourceCaches == null)
        {
            return copy;
        }

        for (int i = 0; i < source.sourceCaches.Count; i++)
        {
            MiningLightingSourceCacheRecord sourceRecord = source.sourceCaches[i];
            if (sourceRecord == null)
            {
                Debug.LogError($"GameDataPersistenceManager: miningLightingCache.sourceCaches contains a null record at index {i}.", this);
                continue;
            }

            MiningLightingSourceCacheRecord sourceCopy = new MiningLightingSourceCacheRecord
            {
                sourceSignature = sourceRecord.sourceSignature,
                sourceBlockPosition = sourceRecord.sourceBlockPosition,
                sourceLocalVoxelPosition = sourceRecord.sourceLocalVoxelPosition,
                profileSignature = sourceRecord.profileSignature
            };

            if (sourceRecord.cells != null)
            {
                for (int j = 0; j < sourceRecord.cells.Count; j++)
                {
                    MiningLightingCellCacheRecord cell = sourceRecord.cells[j];
                    if (cell == null)
                    {
                        Debug.LogError($"GameDataPersistenceManager: miningLightingCache source '{sourceRecord.sourceSignature}' contains a null cell at index {j}.", this);
                        continue;
                    }

                    sourceCopy.cells.Add(new MiningLightingCellCacheRecord
                    {
                        blockPosition = cell.blockPosition,
                        localVoxelPosition = cell.localVoxelPosition,
                        brightness = cell.brightness,
                        distanceFromSourceCells = cell.distanceFromSourceCells,
                        hasPredecessor = cell.hasPredecessor,
                        predecessorBlockPosition = cell.predecessorBlockPosition,
                        predecessorLocalVoxelPosition = cell.predecessorLocalVoxelPosition,
                        revision = cell.revision
                    });
                }
            }

            copy.sourceCaches.Add(sourceCopy);
        }

        return copy;
    }

    private static int CompareVector3Int(Vector3Int left, Vector3Int right)
    {
        int x = left.x.CompareTo(right.x);
        if (x != 0) return x;

        int y = left.y.CompareTo(right.y);
        if (y != 0) return y;

        return left.z.CompareTo(right.z);
    }

    private static int CompareSolidifiedVoxelRecord(SolidifiedVoxelRecord left, SolidifiedVoxelRecord right)
    {
        int block = CompareVector3Int(left.blockPosition, right.blockPosition);
        if (block != 0) return block;

        int local = CompareVector3Int(left.localVoxelPosition, right.localVoxelPosition);
        if (local != 0) return local;

        return string.CompareOrdinal(left.blockDataName, right.blockDataName);
    }

    private static void AddVectorHash(ref int hash, Vector3Int value)
    {
        AddHashValue(ref hash, value.x);
        AddHashValue(ref hash, value.y);
        AddHashValue(ref hash, value.z);
    }

    private static void AddStableStringHash(ref int hash, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            AddHashValue(ref hash, 0);
            return;
        }

        AddHashValue(ref hash, value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            AddHashValue(ref hash, value[i]);
        }
    }

    private static void AddHashValue(ref int hash, int value)
    {
        unchecked
        {
            hash = (hash * 31) + value;
        }
    }
}
