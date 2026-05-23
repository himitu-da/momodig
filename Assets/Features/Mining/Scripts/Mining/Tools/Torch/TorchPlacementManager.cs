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

    [Header("Placement")]
    [SerializeField] private Vector3 placementOffset = Vector3.zero;
    [SerializeField] private bool restorePersistedTorchesOnEnable = true;

    private readonly Dictionary<Vector3Int, TorchPlacedObject> torchesByBlock =
        new Dictionary<Vector3Int, TorchPlacedObject>();

    private bool restoredPersistedTorches;

    public bool ToggleTorchAtWorldPosition(Vector3 worldPosition)
    {
        if (!TryGetBlockPosition(worldPosition, out Vector3Int blockPosition))
        {
            Debug.LogError(
                $"TorchPlacementManager: failed to convert world position to block position. worldPosition={worldPosition}",
                this);
            return false;
        }

        return ToggleTorch(blockPosition);
    }

    public bool ToggleTorch(Vector3Int blockPosition)
    {
        using (ToggleTorchMarker.Auto())
        {
            if (torchesByBlock.ContainsKey(blockPosition))
            {
                return RemoveTorch(blockPosition, true);
            }

            return PlaceTorch(blockPosition, true);
        }
    }

    public bool HasTorch(Vector3Int blockPosition)
    {
        return torchesByBlock.ContainsKey(blockPosition);
    }

    private void OnEnable()
    {
        if (!restorePersistedTorchesOnEnable || restoredPersistedTorches)
        {
            return;
        }

        RestorePersistedTorches();
        restoredPersistedTorches = true;
    }

    private bool TryGetBlockPosition(Vector3 worldPosition, out Vector3Int blockPosition)
    {
        blockPosition = default;
        if (!ValidateTerrainReferences())
        {
            return false;
        }

        TerrainSettings settings = terrainManager.Settings;
        float blockSize = Mathf.Max(0.001f, settings.blockSize);
        blockPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / blockSize + 0.5f),
            Mathf.FloorToInt(worldPosition.y / blockSize + 0.5f),
            0);
        return true;
    }

    private bool PlaceTorch(Vector3Int blockPosition, bool syncPersistence)
    {
        if (!ValidatePlacementReferences())
        {
            return false;
        }

        if (torchesByBlock.ContainsKey(blockPosition))
        {
            Debug.LogError($"TorchPlacementManager: torch already exists at blockPosition={blockPosition}.", this);
            return false;
        }

        Vector3 worldPosition = GetBlockWorldPosition(blockPosition) + placementOffset;
        GameObject torchObject = Instantiate(torchPrefab, worldPosition, Quaternion.identity, torchParent);
        torchObject.name = $"Torch_{blockPosition.x}_{blockPosition.y}_{blockPosition.z}";

        TorchPlacedObject placedObject = torchObject.GetComponent<TorchPlacedObject>();
        if (placedObject == null)
        {
            Debug.LogError("TorchPlacementManager: torch prefab has no TorchPlacedObject component.", torchObject);
            Destroy(torchObject);
            return false;
        }

        if (!placedObject.Configure(blockPosition, miningLightManager, torchLightProfile))
        {
            Destroy(torchObject);
            return false;
        }

        torchesByBlock.Add(blockPosition, placedObject);
        if (syncPersistence)
        {
            SyncPersistence();
        }

        return true;
    }

    private bool RemoveTorch(Vector3Int blockPosition, bool syncPersistence)
    {
        if (!torchesByBlock.TryGetValue(blockPosition, out TorchPlacedObject placedObject))
        {
            Debug.LogError($"TorchPlacementManager: no torch exists at blockPosition={blockPosition}.", this);
            return false;
        }

        torchesByBlock.Remove(blockPosition);
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

    private void RestorePersistedTorches()
    {
        using (RestoreTorchesMarker.Auto())
        {
            GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
            if (persistence.torchPlacements == null)
            {
                Debug.LogError("TorchPlacementManager: persistence.torchPlacements is not configured.", this);
                return;
            }

            for (int i = 0; i < persistence.torchPlacements.Count; i++)
            {
                TorchPlacementData placement = persistence.torchPlacements[i];
                if (placement == null)
                {
                    Debug.LogError($"TorchPlacementManager: torch placement record is null at index {i}.", this);
                    continue;
                }

                if (torchesByBlock.ContainsKey(placement.blockPosition))
                {
                    Debug.LogError(
                        $"TorchPlacementManager: duplicate persisted torch at blockPosition={placement.blockPosition}.",
                        this);
                    continue;
                }

                PlaceTorch(placement.blockPosition, false);
            }
        }
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
        foreach (Vector3Int blockPosition in torchesByBlock.Keys)
        {
            persistence.torchPlacements.Add(new TorchPlacementData
            {
                blockPosition = blockPosition
            });
        }
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
}
