using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FluidMeshRenderer : MonoBehaviour
{
    private static readonly ProfilerMarker LateUpdateMarker =
        new ProfilerMarker("FluidMeshRenderer.LateUpdate");
    private static readonly ProfilerMarker RebuildMeshMarker =
        new ProfilerMarker("FluidMeshRenderer.RebuildMesh");

    [Header("References")]
    [SerializeField] private FluidManager fluidManager;
    [SerializeField] private Material overrideMaterial;

    [Header("Rendering")]
    [SerializeField] private bool hideFacesAgainstSolid = true;
    [SerializeField] private float rebuildInterval = 0.05f;
    [SerializeField] private int renderQueueOffset = 0;
    [SerializeField] private int displayFillLevels = 6;
    [SerializeField] private bool showNonEmptyCellAtLeastOneStep = true;
    [SerializeField] private bool showDebugLogs = false;

    private readonly FluidRenderMeshBuilder meshBuilder = new FluidRenderMeshBuilder();

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;
    private float lastRebuildTime = float.MinValue;
    private int lastVersion = -1;

    void OnValidate()
    {
        rebuildInterval = Mathf.Max(0.01f, rebuildInterval);
        displayFillLevels = Mathf.Max(1, displayFillLevels);
    }

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh
        {
            name = "FluidSurfaceMesh",
            indexFormat = IndexFormat.UInt32
        };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        EnsureMaterial();
        ConfigureRenderer();
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null && runtimeMaterial != overrideMaterial)
        {
            Destroy(runtimeMaterial);
        }

        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    void LateUpdate()
    {
        using var lateUpdateScope = LateUpdateMarker.Auto();
        if (meshRenderer != null && meshRenderer.sharedMaterial == null)
        {
            EnsureMaterial();
        }

        if (fluidManager == null)
        {
            return;
        }

        if (fluidManager.Version == lastVersion)
        {
            return;
        }

        if (Time.unscaledTime - lastRebuildTime < rebuildInterval)
        {
            return;
        }

        RebuildMesh();
    }

    [ContextMenu("Rebuild Fluid Mesh")]
    public void RebuildMesh()
    {
        using var rebuildScope = RebuildMeshMarker.Auto();
        if (fluidManager == null || mesh == null)
        {
            return;
        }

        lastRebuildTime = Time.unscaledTime;
        lastVersion = fluidManager.Version;

        FluidRenderMeshBuildStats stats = meshBuilder.RebuildMesh(
            fluidManager,
            transform,
            mesh,
            hideFacesAgainstSolid,
            displayFillLevels,
            showNonEmptyCellAtLeastOneStep);

        if (showDebugLogs)
        {
            Debug.Log($"FluidMeshRenderer: snapshots={stats.SnapshotCount}, renderCells={stats.RenderCellCount}, vertices={stats.VertexCount}, triangles={stats.TriangleCount}, materialQueue={(meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.renderQueue : -1)}");
        }
    }

    private void EnsureMaterial()
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (overrideMaterial != null)
        {
            runtimeMaterial = overrideMaterial;
            meshRenderer.sharedMaterial = runtimeMaterial;
            return;
        }

        Shader shader = Shader.Find("Custom/FluidUnlit");
        if (shader == null)
        {
            Debug.LogError("FluidMeshRenderer: Shader 'Custom/FluidUnlit' was not found.", this);
            return;
        }

        runtimeMaterial = new Material(shader)
        {
            renderQueue = global::RenderQueue.Geometry + 50 + renderQueueOffset
        };
        meshRenderer.sharedMaterial = runtimeMaterial;
    }

    private void ConfigureRenderer()
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
    }
}
