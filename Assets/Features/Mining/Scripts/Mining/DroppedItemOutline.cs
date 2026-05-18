using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Rigidbody))]
public class DroppedItemOutline : MonoBehaviour
{
    private static readonly int OutlineIntensityID = Shader.PropertyToID("_OutlineIntensity");
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
    private static readonly int SurfaceOffsetID = Shader.PropertyToID("_SurfaceOffset");
    private const float IntensityVisibilityThreshold = 0.001f;
    private const float IntensityChangeEpsilon = 0.0001f;

    [Header("Voxel Speed Gate")]
    [SerializeField, InspectorName("Voxel Hide Velocity"), Tooltip("Hide the outline while this voxel moves at or above this speed (m/s).")]
    private float voxelHideVelocityThreshold = 0.15f;

    [Header("Outline Appearance")]
    [SerializeField, ColorUsage(false, true), InspectorName("Outline Color")]
    private Color outlineColor = new Color(1f, 0.92f, 0.78f, 1f);
    [SerializeField, InspectorName("Outline Width"), Tooltip("Visible outline width in world units.")]
    private float outlineWidth = 0.025f;
    [SerializeField, InspectorName("Surface Offset"), Tooltip("Tiny world-space offset that keeps surface edge lines above the voxel faces.")]
    private float surfaceOffset = 0.002f;

    [Header("Outline Strength")]
    [SerializeField, Range(0f, 1f), InspectorName("Full Intensity"), Tooltip("Outline intensity before the player speed multiplier is applied.")]
    private float fullIntensity = 1f;

    [Header("Player Input Multiplier")]
    [SerializeField, Range(0f, 1f), InspectorName("Stopped Input"), Tooltip("Input magnitude at or below this value uses the stopped multiplier.")]
    private float playerStoppedInputThreshold = 0.01f;
    [SerializeField, Range(0f, 1f), InspectorName("Moving Input"), Tooltip("Input magnitude at or above this value uses the moving multiplier.")]
    private float playerMovingInputThreshold = 0.2f;
    [SerializeField, Range(0f, 1f), InspectorName("Moving Multiplier"), Tooltip("Multiplier while the player is giving movement input.")]
    private float playerMovingMultiplier = 0.2f;
    [SerializeField, Range(0f, 1f), InspectorName("Stopped Multiplier"), Tooltip("Multiplier while the player is not giving movement input.")]
    private float playerStoppedMultiplier = 1f;

    [Header("Timing")]
    [SerializeField, InspectorName("Fade In Duration"), Tooltip("Seconds to reach a stronger outline after movement input stops.")]
    private float fadeInDuration = 0.8f;
    [SerializeField, InspectorName("Fade Out Duration"), Tooltip("Seconds to reach a weaker outline after movement input starts.")]
    private float fadeOutDuration = 0.25f;

    private static Material sharedOutlineMaterial;
    private static bool missingManagerLogged;

    private Rigidbody rb;
    private MeshFilter parentMeshFilter;
    private MeshRenderer outlineRenderer;
    private MeshFilter outlineMeshFilter;
    private MaterialPropertyBlock mpb;
    private float currentIntensity;
    private float appliedIntensity = float.NaN;
    private Color appliedOutlineColor;
    private float appliedOutlineWidth = float.NaN;
    private float appliedSurfaceOffset = float.NaN;
    private bool appliedRendererEnabled;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        parentMeshFilter = GetComponent<MeshFilter>();
        mpb = new MaterialPropertyBlock();

        EnsureOutlineRenderer();
    }

    void OnEnable()
    {
        currentIntensity = 0f;
        SyncOutlineMesh();
        ForceApplyToMaterial();
        RegisterWithManager();
    }

    void OnDisable()
    {
        UnregisterFromManager();
    }

    void Start()
    {
        RegisterWithManager();
    }

    void OnValidate()
    {
        voxelHideVelocityThreshold = Mathf.Max(0f, voxelHideVelocityThreshold);
        outlineWidth = Mathf.Max(0f, outlineWidth);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        fullIntensity = Mathf.Clamp01(fullIntensity);
        playerStoppedInputThreshold = Mathf.Clamp01(playerStoppedInputThreshold);
        playerMovingInputThreshold = Mathf.Clamp01(Mathf.Max(playerStoppedInputThreshold, playerMovingInputThreshold));
        playerMovingMultiplier = Mathf.Clamp01(playerMovingMultiplier);
        playerStoppedMultiplier = Mathf.Clamp01(playerStoppedMultiplier);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    public void TickOutline(float playerInputMagnitude, float deltaTime)
    {
        if (IsVoxelMovingTooFast())
        {
            SetCurrentIntensity(0f);
            return;
        }

        float target = fullIntensity * ComputePlayerInputMultiplier(playerInputMagnitude);
        SetCurrentIntensity(MoveIntensityToward(currentIntensity, target, deltaTime));
    }

    private float MoveIntensityToward(float current, float target, float dt)
    {
        float duration = target > current ? fadeInDuration : fadeOutDuration;
        if (duration <= 0f)
        {
            return target;
        }

        return Mathf.MoveTowards(current, target, dt / duration);
    }

    private void EnsureOutlineRenderer()
    {
        if (outlineRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("Outline");
        GameObject outlineObject;
        if (existing != null)
        {
            outlineObject = existing.gameObject;
        }
        else
        {
            outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(transform, false);
        }

        outlineMeshFilter = outlineObject.GetComponent<MeshFilter>();
        if (outlineMeshFilter == null)
        {
            outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
        }

        outlineRenderer = outlineObject.GetComponent<MeshRenderer>();
        if (outlineRenderer == null)
        {
            outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        }

        outlineRenderer.sharedMaterial = GetSharedOutlineMaterial();
        outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        outlineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void SyncOutlineMesh()
    {
        if (outlineMeshFilter == null || parentMeshFilter == null)
        {
            return;
        }

        Mesh parentMesh = parentMeshFilter.sharedMesh;
        if (parentMesh != null && outlineMeshFilter.sharedMesh != parentMesh)
        {
            outlineMeshFilter.sharedMesh = parentMesh;
        }
    }

    private static Material GetSharedOutlineMaterial()
    {
        if (sharedOutlineMaterial != null)
        {
            return sharedOutlineMaterial;
        }

        Shader shader = Shader.Find("Custom/DroppedItemOutline");
        if (shader == null)
        {
            return null;
        }

        sharedOutlineMaterial = new Material(shader)
        {
            name = "DroppedItemOutline_Shared",
            renderQueue = RenderQueue.Geometry + 50
        };
        return sharedOutlineMaterial;
    }

    private bool IsVoxelMovingTooFast()
    {
        if (rb == null)
        {
            return false;
        }

        float speedSqr = rb.linearVelocity.sqrMagnitude;
        if (voxelHideVelocityThreshold <= 0f)
        {
            return speedSqr > 0f;
        }

        return speedSqr >= voxelHideVelocityThreshold * voxelHideVelocityThreshold;
    }

    private float ComputePlayerInputMultiplier(float playerInputMagnitude)
    {
        float inputMagnitude = Mathf.Clamp01(playerInputMagnitude);
        if (inputMagnitude <= playerStoppedInputThreshold)
        {
            return playerStoppedMultiplier;
        }

        if (inputMagnitude >= playerMovingInputThreshold)
        {
            return playerMovingMultiplier;
        }

        float t = Mathf.InverseLerp(playerStoppedInputThreshold, playerMovingInputThreshold, inputMagnitude);
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(playerStoppedMultiplier, playerMovingMultiplier, t);
    }

    private void SetCurrentIntensity(float nextIntensity)
    {
        nextIntensity = Mathf.Clamp01(nextIntensity);
        if (Mathf.Abs(currentIntensity - nextIntensity) <= IntensityChangeEpsilon)
        {
            return;
        }

        currentIntensity = nextIntensity;
        ApplyToMaterial();
    }

    private void RegisterWithManager()
    {
        DroppedItemOutlineManager manager = DroppedItemOutlineManager.Instance;
        if (manager == null)
        {
            if (!missingManagerLogged)
            {
                missingManagerLogged = true;
                Debug.LogError("DroppedItemOutline: DroppedItemOutlineManager is not configured in the scene.", this);
            }

            return;
        }

        manager.Register(this);
    }

    private void UnregisterFromManager()
    {
        DroppedItemOutlineManager manager = DroppedItemOutlineManager.Instance;
        if (manager != null)
        {
            manager.Unregister(this);
        }
    }

    private void ForceApplyToMaterial()
    {
        appliedIntensity = float.NaN;
        appliedOutlineWidth = float.NaN;
        appliedSurfaceOffset = float.NaN;
        ApplyToMaterial();
    }

    private void ApplyToMaterial()
    {
        if (outlineRenderer == null || mpb == null)
        {
            return;
        }

        bool shouldRender = currentIntensity > IntensityVisibilityThreshold;
        if (outlineRenderer.enabled != shouldRender)
        {
            outlineRenderer.enabled = shouldRender;
        }

        bool valuesUnchanged =
            appliedRendererEnabled == shouldRender &&
            Mathf.Abs(appliedIntensity - currentIntensity) <= IntensityChangeEpsilon &&
            appliedOutlineColor == outlineColor &&
            Mathf.Abs(appliedOutlineWidth - outlineWidth) <= Mathf.Epsilon &&
            Mathf.Abs(appliedSurfaceOffset - surfaceOffset) <= Mathf.Epsilon;

        if (valuesUnchanged)
        {
            return;
        }

        outlineRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(OutlineColorID, outlineColor);
        mpb.SetFloat(OutlineWidthID, outlineWidth);
        mpb.SetFloat(SurfaceOffsetID, surfaceOffset);
        mpb.SetFloat(OutlineIntensityID, currentIntensity);
        outlineRenderer.SetPropertyBlock(mpb);

        appliedRendererEnabled = shouldRender;
        appliedIntensity = currentIntensity;
        appliedOutlineColor = outlineColor;
        appliedOutlineWidth = outlineWidth;
        appliedSurfaceOffset = surfaceOffset;
    }
}
