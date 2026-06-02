using UnityEngine;

public class TorchPlacedObject : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private MiningLightSource lightSource;
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private Renderer[] materialRenderers;

    public VoxelCellKey PlacementAnchor { get; private set; }

    public bool Configure(
        VoxelCellKey placementAnchor,
        MiningLightManager miningLightManager,
        MiningLightProfile lightProfile)
    {
        if (!ValidateConfiguration(miningLightManager, lightProfile))
        {
            return false;
        }

        PlacementAnchor = placementAnchor;
        lightSource.Configure(miningLightManager, lightProfile, sourceTransform, true);
        return true;
    }

    public bool HasCollider()
    {
        return GetComponentInChildren<Collider>(true) != null;
    }

    private bool ValidateConfiguration(
        MiningLightManager miningLightManager,
        MiningLightProfile lightProfile)
    {
        if (lightSource == null)
        {
            Debug.LogError("TorchPlacedObject: MiningLightSource is not configured.", this);
            return false;
        }

        if (sourceTransform == null)
        {
            Debug.LogError("TorchPlacedObject: sourceTransform is not configured.", this);
            return false;
        }

        if (materialRenderers == null || materialRenderers.Length == 0)
        {
            Debug.LogError("TorchPlacedObject: materialRenderers is not configured.", this);
            return false;
        }

        for (int i = 0; i < materialRenderers.Length; i++)
        {
            Renderer targetRenderer = materialRenderers[i];
            if (targetRenderer == null)
            {
                Debug.LogError($"TorchPlacedObject: materialRenderers contains a null renderer at index {i}.", this);
                return false;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                Debug.LogError($"TorchPlacedObject: renderer '{targetRenderer.name}' has no material.", targetRenderer);
                return false;
            }

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (materials[materialIndex] == null)
                {
                    Debug.LogError(
                        $"TorchPlacedObject: renderer '{targetRenderer.name}' has a null material at index {materialIndex}.",
                        targetRenderer);
                    return false;
                }
            }
        }

        if (miningLightManager == null)
        {
            Debug.LogError("TorchPlacedObject: MiningLightManager is not configured.", this);
            return false;
        }

        if (lightProfile == null)
        {
            Debug.LogError("TorchPlacedObject: MiningLightProfile is not configured.", this);
            return false;
        }

        if (HasCollider())
        {
            Debug.LogError("TorchPlacedObject: torch prefab must not contain colliders.", this);
            return false;
        }

        return true;
    }
}
