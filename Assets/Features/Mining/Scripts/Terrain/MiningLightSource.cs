using UnityEngine;

public class MiningLightSource : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private MiningLightManager lightManager;
    [SerializeField] private MiningLightProfile profile;
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private bool includeInLightingCache;

    public MiningLightProfile Profile => profile;
    public Transform SourceTransform => sourceTransform;
    public bool IncludeInLightingCache => includeInLightingCache;

    private void OnEnable()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        lightManager.RegisterLightSource(this);
    }

    private void OnDisable()
    {
        if (lightManager != null)
        {
            lightManager.UnregisterLightSource(this);
        }
    }

    public void Configure(
        MiningLightManager assignedLightManager,
        MiningLightProfile assignedProfile,
        Transform assignedSourceTransform,
        bool cacheablePermanentSource = false)
    {
        if (assignedLightManager == null)
        {
            Debug.LogError("MiningLightSource: cannot configure with a null MiningLightManager.", this);
            return;
        }

        if (assignedProfile == null)
        {
            Debug.LogError("MiningLightSource: cannot configure with a null MiningLightProfile.", this);
            return;
        }

        if (assignedSourceTransform == null)
        {
            Debug.LogError("MiningLightSource: cannot configure with a null Source Transform.", this);
            return;
        }

        if (lightManager != null && lightManager != assignedLightManager)
        {
            lightManager.UnregisterLightSource(this);
        }

        lightManager = assignedLightManager;
        profile = assignedProfile;
        sourceTransform = assignedSourceTransform;
        includeInLightingCache = cacheablePermanentSource;

        if (isActiveAndEnabled)
        {
            lightManager.RegisterLightSource(this);
        }
    }

    public bool TryGetSourceCell(TerrainManager terrainManager, out VoxelCellKey key)
    {
        key = default;
        if (terrainManager == null)
        {
            Debug.LogError("MiningLightSource: TerrainManager is not assigned.", this);
            return false;
        }

        if (terrainManager.VoxelManager == null)
        {
            Debug.LogError("MiningLightSource: TerrainManager.VoxelManager is not assigned.", this);
            return false;
        }

        if (sourceTransform == null)
        {
            Debug.LogError("MiningLightSource: Source Transform is not assigned.", this);
            return false;
        }

        return terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(sourceTransform.position, out key);
    }

    private bool HasRequiredReferences()
    {
        return lightManager != null && profile != null && sourceTransform != null;
    }

    private bool ValidateConfiguration()
    {
        if (lightManager == null)
        {
            Debug.LogError("MiningLightSource: MiningLightManager is not assigned.", this);
            return false;
        }

        if (profile == null)
        {
            Debug.LogError("MiningLightSource: MiningLightProfile is not assigned.", this);
            return false;
        }

        if (sourceTransform == null)
        {
            Debug.LogError("MiningLightSource: Source Transform is not assigned.", this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (lightManager != null)
        {
            lightManager.MarkLightSourcesDirty();
        }
    }
#endif
}
