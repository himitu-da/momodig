using UnityEngine;
using UnityEngine.Rendering;

public sealed class PassageStencilMaskSession
{
    private const string MaskWriterMaterialPath = "Materials/PassageStencilMaskWriter";
    private const string MaskedPlayerMaterialPath = "Materials/PassageMaskedPlayer";
    private const int PassageStencilReference = 11;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int PassageStencilRefId = Shader.PropertyToID("_PassageStencilRef");

    private MeshRenderer passageRenderer;
    private GameObject maskObject;
    private MeshRenderer maskRenderer;
    private Material maskWriterMaterialInstance;
    private SpriteRenderer[] spriteRenderers;
    private Material[] originalSpriteMaterials;
    private Material[] maskedSpriteMaterials;
    private bool isActive;

    public void Begin(
        MeshRenderer sourcePassageRenderer,
        Transform playerRoot,
        Material maskWriterMaterial,
        Material maskedPlayerMaterial)
    {
        End();

        if (sourcePassageRenderer == null || playerRoot == null)
        {
            return;
        }

        MeshFilter passageMeshFilter = sourcePassageRenderer.GetComponent<MeshFilter>();
        if (passageMeshFilter == null || passageMeshFilter.sharedMesh == null)
        {
            return;
        }

        Material resolvedMaskWriterMaterial = maskWriterMaterial != null
            ? maskWriterMaterial
            : Resources.Load<Material>(MaskWriterMaterialPath);
        Material resolvedMaskedPlayerMaterial = maskedPlayerMaterial != null
            ? maskedPlayerMaterial
            : Resources.Load<Material>(MaskedPlayerMaterialPath);

        if (resolvedMaskWriterMaterial == null || resolvedMaskedPlayerMaterial == null)
        {
            Debug.LogWarning("PassageStencilMaskSession: Mask materials were not found.");
            return;
        }

        spriteRenderers = playerRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        passageRenderer = sourcePassageRenderer;
        CreateMaskObject(sourcePassageRenderer, passageMeshFilter.sharedMesh, resolvedMaskWriterMaterial);
        ApplyMaskedSpriteMaterials(resolvedMaskedPlayerMaterial);

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

        UpdateMaskedSpriteMaterialProperties();
    }

    public void End()
    {
        isActive = false;
        RestoreSpriteMaterials();

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

    private void ApplyMaskedSpriteMaterials(Material sourceMaskedPlayerMaterial)
    {
        originalSpriteMaterials = new Material[spriteRenderers.Length];
        maskedSpriteMaterials = new Material[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Material originalMaterial = spriteRenderer.sharedMaterial;
            originalSpriteMaterials[i] = originalMaterial;

            Material materialInstance = new Material(sourceMaskedPlayerMaterial)
            {
                name = $"{sourceMaskedPlayerMaterial.name}_{spriteRenderer.name}"
            };
            CopySpriteMaterialProperties(originalMaterial, materialInstance);
            SetStencilReference(materialInstance);
            maskedSpriteMaterials[i] = materialInstance;
            spriteRenderer.sharedMaterial = materialInstance;
        }
    }

    private void UpdateMaskedSpriteMaterialProperties()
    {
        if (maskedSpriteMaterials == null)
        {
            return;
        }

        for (int i = 0; i < maskedSpriteMaterials.Length; i++)
        {
            if (maskedSpriteMaterials[i] != null)
            {
                SetStencilReference(maskedSpriteMaterials[i]);
            }
        }
    }

    private void RestoreSpriteMaterials()
    {
        if (spriteRenderers != null && originalSpriteMaterials != null)
        {
            int count = Mathf.Min(spriteRenderers.Length, originalSpriteMaterials.Length);
            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].sharedMaterial = originalSpriteMaterials[i];
                }
            }
        }

        if (maskedSpriteMaterials != null)
        {
            for (int i = 0; i < maskedSpriteMaterials.Length; i++)
            {
                if (maskedSpriteMaterials[i] != null)
                {
                    Object.Destroy(maskedSpriteMaterials[i]);
                }
            }
        }

        spriteRenderers = null;
        originalSpriteMaterials = null;
        maskedSpriteMaterials = null;
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

    private static void CopySpriteMaterialProperties(Material source, Material target)
    {
        if (source == null || target == null)
        {
            return;
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

    private static void SetStencilReference(Material material)
    {
        if (material != null && material.HasProperty(PassageStencilRefId))
        {
            material.SetFloat(PassageStencilRefId, PassageStencilReference);
        }
    }
}
