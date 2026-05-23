using UnityEngine;

public class TorchPlacedObject : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private MiningLightSource lightSource;
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private Renderer[] queueRenderers;

    [Header("Rendering")]
    [SerializeField] private RenderQueueLayer renderQueueLayer = RenderQueueLayer.Scenery;
    [SerializeField] private int renderQueueOffset = 0;

    public Vector3Int BlockPosition { get; private set; }

    private Material[][] originalMaterials;
    private Material[][] runtimeMaterials;

    public bool Configure(
        Vector3Int blockPosition,
        MiningLightManager miningLightManager,
        MiningLightProfile lightProfile)
    {
        if (!ValidateConfiguration(miningLightManager, lightProfile))
        {
            return false;
        }

        BlockPosition = blockPosition;
        ApplyRenderQueues();
        lightSource.Configure(miningLightManager, lightProfile, sourceTransform);
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

        if (queueRenderers == null || queueRenderers.Length == 0)
        {
            Debug.LogError("TorchPlacedObject: queueRenderers is not configured.", this);
            return false;
        }

        for (int i = 0; i < queueRenderers.Length; i++)
        {
            if (queueRenderers[i] == null)
            {
                Debug.LogError($"TorchPlacedObject: queueRenderers contains a null renderer at index {i}.", this);
                return false;
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

    private void ApplyRenderQueues()
    {
        int renderQueue = RenderQueue.Resolve(renderQueueLayer) + renderQueueOffset;
        originalMaterials = new Material[queueRenderers.Length][];
        runtimeMaterials = new Material[queueRenderers.Length][];

        for (int rendererIndex = 0; rendererIndex < queueRenderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = queueRenderers[rendererIndex];
            Material[] sourceMaterials = targetRenderer.sharedMaterials;
            Material[] clonedMaterials = new Material[sourceMaterials.Length];

            originalMaterials[rendererIndex] = sourceMaterials;
            runtimeMaterials[rendererIndex] = clonedMaterials;

            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = sourceMaterials[materialIndex];
                if (sourceMaterial == null)
                {
                    Debug.LogError(
                        $"TorchPlacedObject: renderer '{targetRenderer.name}' has a null material at index {materialIndex}.",
                        targetRenderer);
                    continue;
                }

                Material runtimeMaterial = new Material(sourceMaterial)
                {
                    name = $"{sourceMaterial.name}_{targetRenderer.name}_{renderQueue}",
                    renderQueue = renderQueue
                };
                clonedMaterials[materialIndex] = runtimeMaterial;
            }

            targetRenderer.sharedMaterials = clonedMaterials;
        }
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterials();
        DestroyRuntimeMaterials();
    }

    private void RestoreOriginalMaterials()
    {
        if (queueRenderers == null || originalMaterials == null)
        {
            return;
        }

        for (int i = 0; i < queueRenderers.Length && i < originalMaterials.Length; i++)
        {
            if (queueRenderers[i] != null && originalMaterials[i] != null)
            {
                queueRenderers[i].sharedMaterials = originalMaterials[i];
            }
        }
    }

    private void DestroyRuntimeMaterials()
    {
        if (runtimeMaterials == null)
        {
            return;
        }

        for (int rendererIndex = 0; rendererIndex < runtimeMaterials.Length; rendererIndex++)
        {
            Material[] materials = runtimeMaterials[rendererIndex];
            if (materials == null)
            {
                continue;
            }

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
        }
    }
}
