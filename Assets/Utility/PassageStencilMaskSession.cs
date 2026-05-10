using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public sealed class PassageStencilMaskSession
{
    private const int PassageStencilReference = 11;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int PassageStencilRefId = Shader.PropertyToID("_PassageStencilRef");
    private static readonly int UseVertexColorId = Shader.PropertyToID("_UseVertexColor");

    private MeshRenderer passageRenderer;
    private GameObject maskObject;
    private MeshRenderer maskRenderer;
    private Material maskWriterMaterialInstance;
    private RendererMaterialState[] rendererMaterialStates;
    private bool isActive;

    private readonly struct RendererMaterialState
    {
        public RendererMaterialState(Renderer renderer, Material[] originalMaterials, Material[] maskedMaterials)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
            MaskedMaterials = maskedMaterials;
        }

        public readonly Renderer Renderer;
        public readonly Material[] OriginalMaterials;
        public readonly Material[] MaskedMaterials;
    }

    public void Begin(
        MeshRenderer sourcePassageRenderer,
        Transform playerRoot,
        Material maskWriterMaterial,
        Material maskedPlayerMaterial)
    {
        End();

        if (sourcePassageRenderer == null || playerRoot == null || maskWriterMaterial == null || maskedPlayerMaterial == null)
        {
            Debug.LogError("PassageStencilMaskSession: Required references are not configured.");
            return;
        }

        MeshFilter passageMeshFilter = sourcePassageRenderer.GetComponent<MeshFilter>();
        if (passageMeshFilter == null || passageMeshFilter.sharedMesh == null)
        {
            return;
        }

        Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        passageRenderer = sourcePassageRenderer;
        CreateMaskObject(sourcePassageRenderer, passageMeshFilter.sharedMesh, maskWriterMaterial);
        ApplyMaskedMaterials(renderers, maskedPlayerMaterial);

        isActive = true;
        Render();
    }

    public void Render()
    {
        if (!isActive)
        {
            return;
        }

        if (passageRenderer != null && maskRenderer != null)
        {
            maskRenderer.enabled = passageRenderer.enabled;
        }

        if (passageRenderer != null && maskWriterMaterialInstance != null)
        {
            CopyPassageMaterialProperties(passageRenderer.sharedMaterial, maskWriterMaterialInstance);
            SetStencilReference(maskWriterMaterialInstance);
        }

        UpdateMaskedMaterialProperties();
    }

    public void End()
    {
        isActive = false;
        RestoreMaskedMaterials();

        if (maskObject != null)
        {
            Object.Destroy(maskObject);
        }

        if (maskWriterMaterialInstance != null)
        {
            Object.Destroy(maskWriterMaterialInstance);
        }

        passageRenderer = null;
        maskObject = null;
        maskRenderer = null;
        maskWriterMaterialInstance = null;
    }

    private void CreateMaskObject(MeshRenderer sourcePassageRenderer, Mesh mesh, Material sourceMaskWriterMaterial)
    {
        maskObject = new GameObject("GeneratedPassageStencilMask")
        {
            hideFlags = HideFlags.DontSave,
            layer = sourcePassageRenderer.gameObject.layer
        };

        Transform maskTransform = maskObject.transform;
        maskTransform.SetParent(sourcePassageRenderer.transform, false);
        maskTransform.localPosition = Vector3.zero;
        maskTransform.localRotation = Quaternion.identity;
        maskTransform.localScale = Vector3.one;

        MeshFilter meshFilter = maskObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        maskWriterMaterialInstance = new Material(sourceMaskWriterMaterial)
        {
            name = $"{sourceMaskWriterMaterial.name}_{sourcePassageRenderer.name}"
        };
        CopyPassageMaterialProperties(sourcePassageRenderer.sharedMaterial, maskWriterMaterialInstance);
        SetStencilReference(maskWriterMaterialInstance);

        maskRenderer = maskObject.AddComponent<MeshRenderer>();
        maskRenderer.sharedMaterial = maskWriterMaterialInstance;
        maskRenderer.shadowCastingMode = ShadowCastingMode.Off;
        maskRenderer.receiveShadows = false;
        maskRenderer.lightProbeUsage = LightProbeUsage.Off;
        maskRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        maskRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        maskRenderer.sortingLayerID = sourcePassageRenderer.sortingLayerID;
        maskRenderer.sortingOrder = sourcePassageRenderer.sortingOrder;
    }

    private void ApplyMaskedMaterials(Renderer[] renderers, Material sourceMaskedPlayerMaterial)
    {
        List<RendererMaterialState> states = new List<RendererMaterialState>(renderers.Length);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
            {
                continue;
            }

            Material[] maskedMaterials = new Material[originalMaterials.Length];
            for (int j = 0; j < originalMaterials.Length; j++)
            {
                Material materialInstance = new Material(sourceMaskedPlayerMaterial)
                {
                    name = $"{sourceMaskedPlayerMaterial.name}_{renderer.name}_{j}"
                };

                CopyMaskedRendererMaterialProperties(originalMaterials[j], materialInstance, renderer);
                SetStencilReference(materialInstance);
                maskedMaterials[j] = materialInstance;
            }

            renderer.sharedMaterials = maskedMaterials;
            states.Add(new RendererMaterialState(renderer, originalMaterials, maskedMaterials));
        }

        rendererMaterialStates = states.ToArray();
    }

    private void UpdateMaskedMaterialProperties()
    {
        if (rendererMaterialStates == null)
        {
            return;
        }

        for (int i = 0; i < rendererMaterialStates.Length; i++)
        {
            RendererMaterialState state = rendererMaterialStates[i];
            if (state.MaskedMaterials == null)
            {
                continue;
            }

            for (int j = 0; j < state.MaskedMaterials.Length; j++)
            {
                if (state.MaskedMaterials[j] != null)
                {
                    SetStencilReference(state.MaskedMaterials[j]);
                    SetUseVertexColor(state.MaskedMaterials[j], state.Renderer is SpriteRenderer);
                }
            }
        }
    }

    private void RestoreMaskedMaterials()
    {
        if (rendererMaterialStates == null)
        {
            return;
        }

        for (int i = 0; i < rendererMaterialStates.Length; i++)
        {
            RendererMaterialState state = rendererMaterialStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.sharedMaterials = state.OriginalMaterials;
            }

            if (state.MaskedMaterials == null)
            {
                continue;
            }

            for (int j = 0; j < state.MaskedMaterials.Length; j++)
            {
                if (state.MaskedMaterials[j] != null)
                {
                    Object.Destroy(state.MaskedMaterials[j]);
                }
            }
        }

        rendererMaterialStates = null;
    }

    private static void CopyPassageMaterialProperties(Material source, Material target)
    {
        if (source == null || target == null)
        {
            return;
        }

        if (source.HasProperty(BaseMapId) && target.HasProperty(BaseMapId))
        {
            target.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
            target.SetTextureScale(BaseMapId, source.GetTextureScale(BaseMapId));
            target.SetTextureOffset(BaseMapId, source.GetTextureOffset(BaseMapId));
        }

        if (source.HasProperty(BaseColorId) && target.HasProperty(BaseColorId))
        {
            target.SetColor(BaseColorId, source.GetColor(BaseColorId));
        }
        else if (source.HasProperty(ColorId) && target.HasProperty(BaseColorId))
        {
            target.SetColor(BaseColorId, source.GetColor(ColorId));
        }

        if (source.HasProperty(CutoffId) && target.HasProperty(CutoffId))
        {
            target.SetFloat(CutoffId, source.GetFloat(CutoffId));
        }
    }

    private static void CopyMaskedRendererMaterialProperties(Material source, Material target, Renderer renderer)
    {
        if (target == null)
        {
            return;
        }

        SetUseVertexColor(target, renderer is SpriteRenderer);

        if (source == null)
        {
            return;
        }

        if (target.HasProperty(BaseMapId))
        {
            if (source.HasProperty(BaseMapId))
            {
                CopyTextureProperties(source, BaseMapId, target, BaseMapId);
            }
            else if (source.HasProperty(MainTexId))
            {
                CopyTextureProperties(source, MainTexId, target, BaseMapId);
            }
        }

        if (source.HasProperty(BaseColorId) && target.HasProperty(BaseColorId))
        {
            target.SetColor(BaseColorId, source.GetColor(BaseColorId));
        }
        else if (source.HasProperty(ColorId) && target.HasProperty(BaseColorId))
        {
            target.SetColor(BaseColorId, source.GetColor(ColorId));
        }

        if (source.HasProperty(CutoffId) && target.HasProperty(CutoffId))
        {
            target.SetFloat(CutoffId, source.GetFloat(CutoffId));
        }
    }

    private static void CopyTextureProperties(Material source, int sourceTextureId, Material target, int targetTextureId)
    {
        target.SetTexture(targetTextureId, source.GetTexture(sourceTextureId));
        target.SetTextureScale(targetTextureId, source.GetTextureScale(sourceTextureId));
        target.SetTextureOffset(targetTextureId, source.GetTextureOffset(sourceTextureId));
    }

    private static void SetStencilReference(Material material)
    {
        if (material != null && material.HasProperty(PassageStencilRefId))
        {
            material.SetFloat(PassageStencilRefId, PassageStencilReference);
        }
    }

    private static void SetUseVertexColor(Material material, bool useVertexColor)
    {
        if (material != null && material.HasProperty(UseVertexColorId))
        {
            material.SetFloat(UseVertexColorId, useVertexColor ? 1f : 0f);
        }
    }
}
