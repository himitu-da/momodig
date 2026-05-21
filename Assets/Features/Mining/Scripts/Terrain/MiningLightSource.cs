using UnityEngine;

public class MiningLightSource : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private MiningLightManager lightManager;
    [SerializeField] private MiningLightProfile profile;
    [SerializeField] private Transform sourceTransform;

    public MiningLightProfile Profile => profile;
    public Transform SourceTransform => sourceTransform;

    private void OnEnable()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
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
