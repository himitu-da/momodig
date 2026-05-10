using System;
using UnityEngine;

public class RenderQueueApplier : MonoBehaviour
{
    [SerializeField] private RenderQueueBinding[] bindings = Array.Empty<RenderQueueBinding>();

    private Material[][] originalMaterials;
    private Material[][] runtimeMaterials;

    private void Awake()
    {
        if (!ValidateBindings())
        {
            enabled = false;
            return;
        }

        ApplyBindings();
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterials();

        if (runtimeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material[] materials = runtimeMaterials[i];
            if (materials == null)
            {
                continue;
            }

            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] != null)
                {
                    Destroy(materials[j]);
                }
            }
        }

        runtimeMaterials = null;
        originalMaterials = null;
    }

    private void ApplyBindings()
    {
        originalMaterials = new Material[bindings.Length][];
        runtimeMaterials = new Material[bindings.Length][];

        for (int i = 0; i < bindings.Length; i++)
        {
            RenderQueueBinding binding = bindings[i];
            Material[] sourceMaterials = binding.Renderer.sharedMaterials;
            Material[] materials = new Material[sourceMaterials.Length];
            int renderQueue = RenderQueue.Resolve(binding.Layer) + binding.Offset;
            originalMaterials[i] = sourceMaterials;

            for (int j = 0; j < sourceMaterials.Length; j++)
            {
                Material material = new Material(sourceMaterials[j])
                {
                    name = $"{sourceMaterials[j].name}_{binding.Renderer.name}_{renderQueue}"
                };
                material.renderQueue = renderQueue;
                materials[j] = material;
            }

            binding.Renderer.sharedMaterials = materials;
            runtimeMaterials[i] = materials;
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (bindings == null || originalMaterials == null)
        {
            return;
        }

        int count = Math.Min(bindings.Length, originalMaterials.Length);
        for (int i = 0; i < count; i++)
        {
            if (bindings[i].Renderer != null && originalMaterials[i] != null)
            {
                bindings[i].Renderer.sharedMaterials = originalMaterials[i];
            }
        }
    }

    private bool ValidateBindings()
    {
        bool isValid = true;

        if (bindings == null || bindings.Length == 0)
        {
            Debug.LogError($"RenderQueueApplier '{name}': bindings are not configured.", this);
            return false;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            RenderQueueBinding binding = bindings[i];

            if (binding.Renderer == null)
            {
                Debug.LogError($"RenderQueueApplier '{name}': binding {i} renderer is not configured.", this);
                isValid = false;
                continue;
            }

            if (!Enum.IsDefined(typeof(RenderQueueLayer), binding.Layer))
            {
                Debug.LogError($"RenderQueueApplier '{name}': binding {i} layer is invalid.", this);
                isValid = false;
            }

            Material[] materials = binding.Renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                Debug.LogError($"RenderQueueApplier '{name}': binding {i} renderer has no material.", binding.Renderer);
                isValid = false;
                continue;
            }

            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] == null)
                {
                    Debug.LogError($"RenderQueueApplier '{name}': binding {i} material {j} is not configured.", binding.Renderer);
                    isValid = false;
                }
            }
        }

        return isValid;
    }

    [Serializable]
    private struct RenderQueueBinding
    {
        [SerializeField] private Renderer renderer;
        [SerializeField] private RenderQueueLayer layer;
        [SerializeField] private int offset;

        public Renderer Renderer => renderer;
        public RenderQueueLayer Layer => layer;
        public int Offset => offset;
    }
}
