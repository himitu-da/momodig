using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class TorchPlacementManager : MonoBehaviour
{
    private static readonly ProfilerMarker ToggleTorchMarker =
        new ProfilerMarker("TorchPlacementManager.ToggleTorch");
    private static readonly ProfilerMarker RestoreTorchesMarker =
        new ProfilerMarker("TorchPlacementManager.RestoreTorches");

    [Header("Required References")]
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private MiningLightManager miningLightManager;
    [SerializeField] private MiningLightProfile torchLightProfile;
    [SerializeField] private GameObject torchPrefab;
    [SerializeField] private Transform torchParent;

    [Header("Facility Upgrades")]
    [SerializeField] private FacilityUpgradeCatalog facilityUpgradeCatalog;
    [SerializeField] private Stat lightRadiusCells = new Stat { BaseValue = 1f };
    [SerializeField] private Stat lightTransmission = new Stat { BaseValue = 0.3f };

    [Header("Placement")]
    [SerializeField] private Vector3 placementOffset = Vector3.zero;
    [SerializeField] private bool restorePersistedTorchesOnEnable = true;

    private readonly Dictionary<VoxelCellKey, TorchPlacedObject> torchesByPlacementAnchor =
        new Dictionary<VoxelCellKey, TorchPlacedObject>();
    private readonly Dictionary<Vector3Int, List<TorchPlacementData>> pendingTorchPlacementsByChunk =
        new Dictionary<Vector3Int, List<TorchPlacementData>>();

    private bool preparedPersistedTorchLoading;
    private MiningLightProfile runtimeTorchLightProfile;

    public bool ToggleTorchAtWorldPosition(Vector3 worldPosition)
    {
        if (!TryGetPlacementCellAtWorldPosition(worldPosition, out VoxelCellKey targetCell))
        {
            Debug.LogError(
                $"TorchPlacementManager: failed to convert world position to a voxel cell. worldPosition={worldPosition}",
                this);
            return false;
        }

        if (TryFindTorchContainingCell(targetCell, out VoxelCellKey existingPlacementAnchor))
        {
            return RemoveTorch(existingPlacementAnchor, true);
        }

        if (!TryGetPlacementAnchor(targetCell, out VoxelCellKey placementAnchor))
        {
            Debug.LogError(
                $"TorchPlacementManager: failed to resolve placement anchor. targetCell={targetCell.blockPosition}/{targetCell.localVoxelPosition}",
                this);
            return false;
        }

        return PlaceTorch(placementAnchor, true);
    }

    public bool ToggleTorch(Vector3Int blockPosition)
    {
        using (ToggleTorchMarker.Auto())
        {
            if (!TryGetPlacementAnchorFromBlockPosition(blockPosition, out VoxelCellKey placementAnchor))
            {
                return false;
            }

            if (torchesByPlacementAnchor.ContainsKey(placementAnchor))
            {
                return RemoveTorch(placementAnchor, true);
            }

            return PlaceTorch(placementAnchor, true);
        }
    }

    public bool HasTorch(Vector3Int blockPosition)
    {
        return TryGetPlacementAnchorFromBlockPosition(blockPosition, out VoxelCellKey placementAnchor) &&
               torchesByPlacementAnchor.ContainsKey(placementAnchor);
    }

    public bool TryGetPlaceableCursorSquareAtWorldPosition(
        Vector3 worldPosition,
        out Vector3 center,
        out float size)
    {
        center = Vector3.zero;
        size = 0f;

        if (!TryGetPlacementCellAtWorldPosition(worldPosition, out VoxelCellKey targetCell))
        {
            return false;
        }

        if (TryFindTorchContainingCell(targetCell, out _))
        {
            return false;
        }

        if (!TryGetPlacementAnchor(targetCell, out VoxelCellKey placementAnchor))
        {
            return false;
        }

        if (torchesByPlacementAnchor.ContainsKey(placementAnchor) || !CanPlaceTorch(placementAnchor))
        {
            return false;
        }

        center = GetPlacementWorldPosition(placementAnchor) + placementOffset;
        size = terrainManager.Settings.blockSize;
        return size > 0f;
    }

    private void OnEnable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged += ApplyEnhancements;
        ApplyEnhancements();
        SubscribeTerrainChanges();

        if (!restorePersistedTorchesOnEnable || preparedPersistedTorchLoading)
        {
            return;
        }

        PreparePersistedTorchLoading();
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged -= ApplyEnhancements;
        UnsubscribeTerrainChanges();
    }

    private void OnDestroy()
    {
        if (runtimeTorchLightProfile != null)
        {
            Destroy(runtimeTorchLightProfile);
            runtimeTorchLightProfile = null;
        }
    }

    private bool TryGetPlacementCellAtWorldPosition(Vector3 worldPosition, out VoxelCellKey key)
    {
        key = default;
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        if (!ValidateVoxelManager()) return false;
        worldPosition.z = terrainManager.Settings.center.z;
        return terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(worldPosition, out key);
    }

    private bool PlaceTorch(VoxelCellKey placementAnchor, bool syncPersistence)
    {
        if (!ValidatePlacementReferences())
        {
            return false;
        }

        if (!CanPlaceTorch(placementAnchor))
        {
            Debug.LogWarning(
                $"TorchPlacementManager: cannot place a torch because the placement volume contains active voxels. anchor={placementAnchor.blockPosition}/{placementAnchor.localVoxelPosition}",
                this);
            return false;
        }

        if (torchesByPlacementAnchor.ContainsKey(placementAnchor))
        {
            Debug.LogError(
                $"TorchPlacementManager: torch already exists at anchor={placementAnchor.blockPosition}/{placementAnchor.localVoxelPosition}.",
                this);
            return false;
        }

        Vector3 worldPosition = GetPlacementWorldPosition(placementAnchor) + placementOffset;
        GameObject torchObject = Instantiate(torchPrefab, worldPosition, Quaternion.identity, torchParent);
        torchObject.name = $"Torch_{placementAnchor.blockPosition.x}_{placementAnchor.blockPosition.y}_{placementAnchor.blockPosition.z}_{placementAnchor.localVoxelPosition.x}_{placementAnchor.localVoxelPosition.y}_{placementAnchor.localVoxelPosition.z}";

        TorchPlacedObject placedObject = torchObject.GetComponent<TorchPlacedObject>();
        if (placedObject == null)
        {
            Debug.LogError("TorchPlacementManager: torch prefab has no TorchPlacedObject component.", torchObject);
            Destroy(torchObject);
            return false;
        }

        if (!placedObject.Configure(placementAnchor, miningLightManager, runtimeTorchLightProfile))
        {
            Destroy(torchObject);
            return false;
        }

        torchesByPlacementAnchor.Add(placementAnchor, placedObject);
        if (syncPersistence)
        {
            SyncPersistence();
        }

        return true;
    }

    private bool RemoveTorch(VoxelCellKey placementAnchor, bool syncPersistence)
    {
        if (!torchesByPlacementAnchor.TryGetValue(placementAnchor, out TorchPlacedObject placedObject))
        {
            Debug.LogError(
                $"TorchPlacementManager: no torch exists at anchor={placementAnchor.blockPosition}/{placementAnchor.localVoxelPosition}.",
                this);
            return false;
        }

        torchesByPlacementAnchor.Remove(placementAnchor);
        if (placedObject != null)
        {
            Destroy(placedObject.gameObject);
        }

        if (syncPersistence)
        {
            SyncPersistence();
        }

        return true;
    }

    public void LoadTorchesInChunk(Vector3Int chunkPosition)
    {
        if (!restorePersistedTorchesOnEnable)
        {
            return;
        }

        if (!preparedPersistedTorchLoading)
        {
            PreparePersistedTorchLoading();
        }

        if (!pendingTorchPlacementsByChunk.TryGetValue(chunkPosition, out List<TorchPlacementData> placements) ||
            placements.Count == 0)
        {
            return;
        }

        using (RestoreTorchesMarker.Auto())
        {
            bool placedAnyTorch = false;
            List<TorchPlacementData> failedPlacements = null;
            for (int i = 0; i < placements.Count; i++)
            {
                TorchPlacementData placement = placements[i];
                if (placement == null)
                {
                    Debug.LogError($"TorchPlacementManager: torch placement record is null in chunk {chunkPosition} at index {i}.", this);
                    continue;
                }

                VoxelCellKey placementAnchor = new VoxelCellKey(
                    placement.blockPosition,
                    placement.localVoxelPosition);

                if (torchesByPlacementAnchor.ContainsKey(placementAnchor))
                {
                    continue;
                }

                if (PlaceTorch(placementAnchor, false))
                {
                    placedAnyTorch = true;
                    continue;
                }

                if (failedPlacements == null)
                {
                    failedPlacements = new List<TorchPlacementData>();
                }

                failedPlacements.Add(placement);
            }

            if (failedPlacements != null && failedPlacements.Count > 0)
            {
                pendingTorchPlacementsByChunk[chunkPosition] = failedPlacements;
            }
            else
            {
                pendingTorchPlacementsByChunk.Remove(chunkPosition);
            }

            if (placedAnyTorch && miningLightManager != null)
            {
                miningLightManager.MarkLightSourcesDirty();
            }
        }
    }

    private void PreparePersistedTorchLoading()
    {
        using (RestoreTorchesMarker.Auto())
        {
            pendingTorchPlacementsByChunk.Clear();

            GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
            if (persistence.torchPlacements == null)
            {
                Debug.LogError("TorchPlacementManager: persistence.torchPlacements is not configured.", this);
                return;
            }

            HashSet<VoxelCellKey> pendingAnchors = new HashSet<VoxelCellKey>();
            for (int i = 0; i < persistence.torchPlacements.Count; i++)
            {
                TorchPlacementData placement = persistence.torchPlacements[i];
                if (placement == null)
                {
                    Debug.LogError($"TorchPlacementManager: torch placement record is null at index {i}.", this);
                    continue;
                }

                VoxelCellKey placementAnchor = new VoxelCellKey(
                    placement.blockPosition,
                    placement.localVoxelPosition);

                if (torchesByPlacementAnchor.ContainsKey(placementAnchor))
                {
                    continue;
                }

                if (!pendingAnchors.Add(placementAnchor))
                {
                    Debug.LogError(
                        $"TorchPlacementManager: duplicate persisted torch at anchor={placementAnchor.blockPosition}/{placementAnchor.localVoxelPosition}.",
                        this);
                    continue;
                }

                if (!TryGetChunkPositionForPlacement(placement, out Vector3Int chunkPosition))
                {
                    continue;
                }

                if (!pendingTorchPlacementsByChunk.TryGetValue(chunkPosition, out List<TorchPlacementData> placements))
                {
                    placements = new List<TorchPlacementData>();
                    pendingTorchPlacementsByChunk.Add(chunkPosition, placements);
                }

                placements.Add(placement);
            }

            preparedPersistedTorchLoading = true;
        }
    }

    private void HandleTerrainCellsChanged(TerrainChangeBatch change)
    {
        if (change == null)
        {
            Debug.LogError("TorchPlacementManager: terrain change batch is null.", this);
            return;
        }

        if (change.addedSolidCells.Count == 0)
        {
            return;
        }

        HashSet<VoxelCellKey> torchPlacementsToRemove = null;
        for (int i = 0; i < change.addedSolidCells.Count; i++)
        {
            if (!TryFindTorchContainingCell(change.addedSolidCells[i], out VoxelCellKey placementAnchor))
            {
                continue;
            }

            if (torchPlacementsToRemove == null)
            {
                torchPlacementsToRemove = new HashSet<VoxelCellKey>();
            }

            torchPlacementsToRemove.Add(placementAnchor);
        }

        if (torchPlacementsToRemove == null || torchPlacementsToRemove.Count == 0)
        {
            return;
        }

        foreach (VoxelCellKey placementAnchor in torchPlacementsToRemove)
        {
            RemoveTorch(placementAnchor, false);
        }

        SyncPersistence();
    }

    private void ApplyEnhancements()
    {
        if (!ValidateFacilityUpgradeCatalog())
        {
            return;
        }

        lightRadiusCells.RemoveAllModifiers();
        lightTransmission.RemoveAllModifiers();

        GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
        IReadOnlyList<FacilityUpgradeDefinition> upgrades = facilityUpgradeCatalog.Upgrades;
        for (int upgradeIndex = 0; upgradeIndex < upgrades.Count; upgradeIndex++)
        {
            FacilityUpgradeDefinition upgrade = upgrades[upgradeIndex];
            int level = persistence.GetFacilityUpgradeLevel(upgrade.UpgradeId, upgrade.InitialLevel);
            int effectLevel = upgrade.GetEffectLevel(level);

            if (effectLevel == 0)
            {
                continue;
            }

            IReadOnlyList<Enhancement> enhancements = upgrade.Enhancements;
            for (int enhancementIndex = 0; enhancementIndex < enhancements.Count; enhancementIndex++)
            {
                Enhancement enhancement = enhancements[enhancementIndex];
                if (enhancement.TargetCategory != "Torch")
                {
                    continue;
                }

                Stat targetStat = GetStatByName(enhancement.TargetStatName);
                if (targetStat == null)
                {
                    Debug.LogError(
                        $"TorchPlacementManager: enhancement '{enhancement.name}' targets unsupported stat '{enhancement.TargetStatName}'.",
                        this);
                    continue;
                }

                ApplyModifier(targetStat, enhancement, effectLevel);
            }
        }

        RebuildRuntimeTorchLightProfile();
        ReconfigurePlacedTorches();
        if (miningLightManager != null)
        {
            miningLightManager.MarkLightSourcesDirty();
        }
    }

    private bool ValidateFacilityUpgradeCatalog()
    {
        if (facilityUpgradeCatalog == null)
        {
            Debug.LogError("TorchPlacementManager: facilityUpgradeCatalog is not configured.", this);
            return false;
        }

        return facilityUpgradeCatalog.ValidateConfiguration(this);
    }

    private Stat GetStatByName(string statName)
    {
        switch (statName)
        {
            case "LightRadiusCells":
                return lightRadiusCells;
            case "LightTransmission":
                return lightTransmission;
            default:
                return null;
        }
    }

    private void ApplyModifier(Stat stat, Enhancement enhancement, int level)
    {
        if (enhancement.Type == EnhancementType.Additive)
        {
            stat.AddAdditiveModifier(level * enhancement.Value);
        }
        else if (enhancement.Type == EnhancementType.Multiplicative)
        {
            stat.AddMultiplicativeModifier(Mathf.Pow(enhancement.Value, level));
        }
    }

    private void RebuildRuntimeTorchLightProfile()
    {
        if (torchLightProfile == null)
        {
            Debug.LogError("TorchPlacementManager: torchLightProfile is not configured.", this);
            return;
        }

        if (runtimeTorchLightProfile != null)
        {
            Destroy(runtimeTorchLightProfile);
            runtimeTorchLightProfile = null;
        }

        runtimeTorchLightProfile = torchLightProfile.CreateRuntimeOverride(
            "RuntimeTorchLightProfile",
            lightRadiusCells.IntValue,
            lightTransmission.Value);
    }

    private void ReconfigurePlacedTorches()
    {
        if (runtimeTorchLightProfile == null || miningLightManager == null)
        {
            return;
        }

        foreach (TorchPlacedObject placedObject in torchesByPlacementAnchor.Values)
        {
            if (placedObject == null)
            {
                Debug.LogError("TorchPlacementManager: tracked torch placement is null.", this);
                continue;
            }

            placedObject.Configure(placedObject.PlacementAnchor, miningLightManager, runtimeTorchLightProfile);
        }
    }

    private bool CanPlaceTorch(VoxelCellKey placementAnchor)
    {
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        if (!ValidateVoxelManager()) return false;

        int side = Mathf.Max(1, terrainManager.Settings.voxelsPerBlock);
        for (int x = 0; x < side; x++)
        {
            for (int y = 0; y < side; y++)
            {
                for (int z = 0; z < side; z++)
                {
                    VoxelCellKey key = OffsetCell(placementAnchor, new Vector3Int(x, y, z));
                    if (terrainManager.VoxelManager.GetVoxelAt(key.blockPosition, key.localVoxelPosition) != null)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private bool TryFindTorchContainingCell(VoxelCellKey cell, out VoxelCellKey placementAnchor)
    {
        foreach (VoxelCellKey candidateAnchor in torchesByPlacementAnchor.Keys)
        {
            if (PlacementContainsCell(candidateAnchor, cell))
            {
                placementAnchor = candidateAnchor;
                return true;
            }
        }

        placementAnchor = default;
        return false;
    }

    private bool PlacementContainsCell(VoxelCellKey placementAnchor, VoxelCellKey cell)
    {
        int side = Mathf.Max(1, terrainManager.Settings.voxelsPerBlock);
        for (int x = 0; x < side; x++)
        {
            for (int y = 0; y < side; y++)
            {
                for (int z = 0; z < side; z++)
                {
                    VoxelCellKey key = OffsetCell(placementAnchor, new Vector3Int(x, y, z));
                    if (key.Equals(cell))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void SubscribeTerrainChanges()
    {
        if (!ValidateTerrainReferences())
        {
            return;
        }

        if (!ValidateVoxelManager()) return;

        terrainManager.VoxelManager.TerrainCellsChanged -= HandleTerrainCellsChanged;
        terrainManager.VoxelManager.TerrainCellsChanged += HandleTerrainCellsChanged;
    }

    private void UnsubscribeTerrainChanges()
    {
        if (terrainManager == null || terrainManager.VoxelManager == null)
        {
            return;
        }

        terrainManager.VoxelManager.TerrainCellsChanged -= HandleTerrainCellsChanged;
    }

    private void SyncPersistence()
    {
        GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
        if (persistence.torchPlacements == null)
        {
            Debug.LogError("TorchPlacementManager: persistence.torchPlacements is not configured.", this);
            return;
        }

        persistence.torchPlacements.Clear();
        HashSet<VoxelCellKey> persistedAnchors = new HashSet<VoxelCellKey>();
        foreach (VoxelCellKey placementAnchor in torchesByPlacementAnchor.Keys)
        {
            persistedAnchors.Add(placementAnchor);
            persistence.torchPlacements.Add(new TorchPlacementData
            {
                blockPosition = placementAnchor.blockPosition,
                localVoxelPosition = placementAnchor.localVoxelPosition
            });
        }

        foreach (List<TorchPlacementData> placements in pendingTorchPlacementsByChunk.Values)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                TorchPlacementData placement = placements[i];
                if (placement == null)
                {
                    Debug.LogError($"TorchPlacementManager: pending torch placement record is null at index {i}.", this);
                    continue;
                }

                VoxelCellKey placementAnchor = new VoxelCellKey(
                    placement.blockPosition,
                    placement.localVoxelPosition);

                if (!persistedAnchors.Add(placementAnchor))
                {
                    continue;
                }

                persistence.torchPlacements.Add(new TorchPlacementData
                {
                    blockPosition = placement.blockPosition,
                    localVoxelPosition = placement.localVoxelPosition
                });
            }
        }
    }

    private bool TryGetChunkPositionForPlacement(TorchPlacementData placement, out Vector3Int chunkPosition)
    {
        chunkPosition = Vector3Int.zero;
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        if (terrainManager.ChunkManager == null)
        {
            Debug.LogError("TorchPlacementManager: TerrainManager.ChunkManager is not configured.", this);
            return false;
        }

        chunkPosition = terrainManager.ChunkManager.GetChunkPositionFromBlock(placement.blockPosition);
        return true;
    }

    private bool TryGetPlacementAnchorFromBlockPosition(Vector3Int blockPosition, out VoxelCellKey placementAnchor)
    {
        if (!ValidateTerrainReferences())
        {
            placementAnchor = default;
            return false;
        }

        Vector3 worldPosition = GetBlockWorldPosition(blockPosition);
        if (!TryGetPlacementCellAtWorldPosition(worldPosition, out VoxelCellKey targetCell))
        {
            placementAnchor = default;
            return false;
        }

        return TryGetPlacementAnchor(targetCell, out placementAnchor);
    }

    private bool TryGetPlacementAnchor(VoxelCellKey targetCell, out VoxelCellKey placementAnchor)
    {
        placementAnchor = default;
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        if (!ValidateVoxelManager())
        {
            return false;
        }

        int side = Mathf.Max(1, terrainManager.Settings.voxelsPerBlock);
        Vector3Int blockPosition = targetCell.blockPosition;
        Vector3Int localVoxelPosition = targetCell.localVoxelPosition - Vector3Int.one * (side / 2);
        if (!terrainManager.VoxelManager.NormalizeVoxelPosition(ref blockPosition, ref localVoxelPosition))
        {
            return false;
        }

        placementAnchor = new VoxelCellKey(blockPosition, localVoxelPosition);
        return true;
    }

    private VoxelCellKey OffsetCell(VoxelCellKey origin, Vector3Int localOffset)
    {
        Vector3Int blockPosition = origin.blockPosition;
        Vector3Int localVoxelPosition = origin.localVoxelPosition + localOffset;
        if (!terrainManager.VoxelManager.NormalizeVoxelPosition(ref blockPosition, ref localVoxelPosition))
        {
            Debug.LogError(
                $"TorchPlacementManager: failed to normalize voxel cell. origin={origin.blockPosition}/{origin.localVoxelPosition}, offset={localOffset}",
                this);
            return origin;
        }

        return new VoxelCellKey(blockPosition, localVoxelPosition);
    }

    private Vector3 GetPlacementWorldPosition(VoxelCellKey placementAnchor)
    {
        int side = Mathf.Max(1, terrainManager.Settings.voxelsPerBlock);
        float voxelWorldSize = terrainManager.Settings.blockSize / side;
        Vector3 anchorWorldPosition = terrainManager.VoxelManager.CalculateWorldPosition(
            placementAnchor.blockPosition,
            placementAnchor.localVoxelPosition);
        float centerOffset = (side - 1) * 0.5f * voxelWorldSize;
        return anchorWorldPosition + Vector3.one * centerOffset;
    }

    private Vector3 GetBlockWorldPosition(Vector3Int blockPosition)
    {
        TerrainSettings settings = terrainManager.Settings;
        return new Vector3(
            blockPosition.x * settings.blockSize,
            blockPosition.y * settings.blockSize,
            settings.center.z + blockPosition.z * settings.blockSize);
    }

    private bool ValidatePlacementReferences()
    {
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        if (miningLightManager == null)
        {
            Debug.LogError("TorchPlacementManager: MiningLightManager is not configured.", this);
            return false;
        }

        if (torchLightProfile == null)
        {
            Debug.LogError("TorchPlacementManager: torchLightProfile is not configured.", this);
            return false;
        }

        if (facilityUpgradeCatalog == null)
        {
            Debug.LogError("TorchPlacementManager: facilityUpgradeCatalog is not configured.", this);
            return false;
        }

        if (runtimeTorchLightProfile == null)
        {
            Debug.LogError("TorchPlacementManager: runtimeTorchLightProfile is not configured.", this);
            return false;
        }

        if (torchPrefab == null)
        {
            Debug.LogError("TorchPlacementManager: torchPrefab is not configured.", this);
            return false;
        }

        if (torchParent == null)
        {
            Debug.LogError("TorchPlacementManager: torchParent is not configured.", this);
            return false;
        }

        return true;
    }

    private bool ValidateTerrainReferences()
    {
        if (terrainManager == null)
        {
            Debug.LogError("TorchPlacementManager: TerrainManager is not configured.", this);
            return false;
        }

        return true;
    }

    private bool ValidateVoxelManager()
    {
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        if (terrainManager.VoxelManager == null)
        {
            Debug.LogError("TorchPlacementManager: TerrainManager.VoxelManager is not configured.", this);
            return false;
        }

        return true;
    }
}
