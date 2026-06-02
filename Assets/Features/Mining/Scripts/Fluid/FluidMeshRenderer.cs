using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FluidMeshRenderer : MonoBehaviour
{
    private const float MinFillRatio = 0.01f;
    private const int VerticesPerFace = 4;
    private const int IndicesPerFace = 6;

    private static readonly ProfilerMarker LateUpdateMarker =
        new ProfilerMarker("FluidMeshRenderer.LateUpdate");
    private static readonly ProfilerMarker RebuildMeshMarker =
        new ProfilerMarker("FluidMeshRenderer.RebuildMesh");
    private static readonly ProfilerMarker GetSnapshotsMarker =
        new ProfilerMarker("FluidMeshRenderer.GetSnapshots");
    private static readonly ProfilerMarker BuildAggregatesMarker =
        new ProfilerMarker("FluidMeshRenderer.BuildAggregates");
    private static readonly ProfilerMarker AllocateBuffersMarker =
        new ProfilerMarker("FluidMeshRenderer.AllocateBuffers");
    private static readonly ProfilerMarker AppendRenderCellsMarker =
        new ProfilerMarker("FluidMeshRenderer.AppendRenderCells");
    private static readonly ProfilerMarker AppendRenderCellMarker =
        new ProfilerMarker("FluidMeshRenderer.AppendRenderCell");
    private static readonly ProfilerMarker MeshApplyMarker =
        new ProfilerMarker("FluidMeshRenderer.MeshApply");
    private static readonly ProfilerMarker MeshRecalculateMarker =
        new ProfilerMarker("FluidMeshRenderer.MeshRecalculate");

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

    private readonly List<FluidCellSnapshot> snapshots = new List<FluidCellSnapshot>();
    private readonly Dictionary<Vector3Int, RenderCellAggregate> aggregates = new Dictionary<Vector3Int, RenderCellAggregate>();
    private readonly List<Vector3> vertexBuffer = new List<Vector3>();
    private readonly List<int> triangleBuffer = new List<int>();
    private readonly List<Color> colorBuffer = new List<Color>();
    private readonly Vector3[] faceCornerBuffer = new Vector3[4];
    private readonly Vector3[] localCornerBuffer = new Vector3[4];

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

        using (GetSnapshotsMarker.Auto())
        {
            fluidManager.GetFluidCellSnapshots(snapshots);
        }

        BuildAggregates();

        using (AllocateBuffersMarker.Auto())
        {
            int maxFacesPerRenderCell = GetLateralDirections().Length + 1;
            int estimatedFaceCount = aggregates.Count * maxFacesPerRenderCell;
            EnsureListCapacity(vertexBuffer, estimatedFaceCount * VerticesPerFace);
            EnsureListCapacity(triangleBuffer, estimatedFaceCount * IndicesPerFace);
            EnsureListCapacity(colorBuffer, estimatedFaceCount * VerticesPerFace);
            vertexBuffer.Clear();
            triangleBuffer.Clear();
            colorBuffer.Clear();
        }

        using (AppendRenderCellsMarker.Auto())
        {
            foreach (var pair in aggregates)
            {
                Vector3Int renderCell = pair.Key;
                RenderCellAggregate aggregate = pair.Value;
                float fillRatio = GetDisplayFillRatio(aggregate.Liters);
                if (fillRatio <= 0f || aggregate.Definition == null)
                {
                    continue;
                }

                AppendRenderCell(renderCell, fillRatio, aggregate.Definition.tint, vertexBuffer, triangleBuffer, colorBuffer);
            }
        }

        using (MeshApplyMarker.Auto())
        {
            mesh.Clear();
            mesh.SetVertices(vertexBuffer);
            mesh.SetTriangles(triangleBuffer, 0);
            mesh.SetColors(colorBuffer);
        }

        using (MeshRecalculateMarker.Auto())
        {
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        if (showDebugLogs)
        {
            Debug.Log($"FluidMeshRenderer: snapshots={snapshots.Count}, renderCells={aggregates.Count}, vertices={vertexBuffer.Count}, triangles={triangleBuffer.Count / 3}, materialQueue={(meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.renderQueue : -1)}");
        }
    }

    private void BuildAggregates()
    {
        using var aggregateScope = BuildAggregatesMarker.Auto();
        aggregates.Clear();

        foreach (FluidCellSnapshot snapshot in snapshots)
        {
            Vector3Int renderCell = fluidManager.InternalToRenderCell(snapshot.CellPosition);
            if (!aggregates.TryGetValue(renderCell, out RenderCellAggregate aggregate))
            {
                aggregate = new RenderCellAggregate();
            }

            aggregate.Liters += snapshot.Liters;
            if (snapshot.Liters >= aggregate.DominantLiters)
            {
                aggregate.Definition = snapshot.Definition;
                aggregate.DominantLiters = snapshot.Liters;
            }

            aggregates[renderCell] = aggregate;
        }
    }

    private void AppendRenderCell(
        Vector3Int renderCell,
        float fillRatio,
        Color tint,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color> colors)
    {
        using var appendScope = AppendRenderCellMarker.Auto();
        Vector3 min = fluidManager.GetRenderCellWorldMin(renderCell);
        Vector3 max = min + Vector3.one * fluidManager.RenderVoxelSize;
        int verticalAxis = GetVerticalAxisIndex();
        SetAxis(ref max, verticalAxis, Mathf.Lerp(GetAxis(min, verticalAxis), GetAxis(max, verticalAxis), fillRatio));

        float currentTop = GetAxis(max, verticalAxis);
        if (currentTop <= GetAxis(min, verticalAxis) + 0.0001f)
        {
            return;
        }

        Vector3Int upDirection = -GetDownDirection();
        float upperFill = GetNeighborFill(renderCell + upDirection);
        bool hideTop = fillRatio >= 0.999f && (upperFill > MinFillRatio || fluidManager.IsRenderNeighborSolid(renderCell, upDirection, fillRatio));
        if (!hideTop)
        {
            AddFace(ToLocalCorners(GetFace(min, max, upDirection)), tint, vertices, triangles, colors);
        }

        foreach (Vector3Int lateralDirection in GetLateralDirections())
        {
            float neighborFill = GetNeighborFill(renderCell + lateralDirection);
            float visibleFrom = Mathf.Lerp(GetAxis(min, verticalAxis), GetAxis(min, verticalAxis) + fluidManager.RenderVoxelSize, Mathf.Clamp01(neighborFill));

            if (neighborFill <= MinFillRatio && hideFacesAgainstSolid && fluidManager.IsRenderNeighborSolid(renderCell, lateralDirection, fillRatio))
            {
                continue;
            }

            Vector3 faceMin = min;
            Vector3 faceMax = max;
            SetAxis(ref faceMin, verticalAxis, Mathf.Max(GetAxis(faceMin, verticalAxis), visibleFrom));
            if (GetAxis(faceMin, verticalAxis) >= GetAxis(faceMax, verticalAxis) - 0.0001f)
            {
                continue;
            }

            AddFace(ToLocalCorners(GetFace(faceMin, faceMax, lateralDirection)), tint, vertices, triangles, colors);
        }
    }

    private float GetNeighborFill(Vector3Int renderCell)
    {
        if (!aggregates.TryGetValue(renderCell, out RenderCellAggregate aggregate))
        {
            return 0f;
        }

        return GetDisplayFillRatio(aggregate.Liters);
    }

    private float GetDisplayFillRatio(float liters)
    {
        if (fluidManager == null)
        {
            return 0f;
        }

        float rawFillRatio = Mathf.Clamp01(liters / Mathf.Max(0.0001f, fluidManager.RenderCellCapacityLiters));
        if (rawFillRatio <= 0.0001f)
        {
            return 0f;
        }

        if (displayFillLevels <= 1)
        {
            return 1f;
        }

        float quantizedFillRatio = Mathf.Clamp01(Mathf.Ceil(rawFillRatio * displayFillLevels) / displayFillLevels);
        if (showNonEmptyCellAtLeastOneStep)
        {
            float minimumVisibleFillRatio = 1f / displayFillLevels;
            return Mathf.Max(minimumVisibleFillRatio, quantizedFillRatio);
        }

        if (rawFillRatio <= MinFillRatio)
        {
            return 0f;
        }

        return quantizedFillRatio;
    }


    private void EnsureMaterial()
    {
        if (overrideMaterial != null)
        {
            runtimeMaterial = overrideMaterial;
            meshRenderer.sharedMaterial = runtimeMaterial;
            return;
        }

        Shader shader = Shader.Find("Custom/FluidUnlit");
        if (shader == null)
        {
            Debug.LogWarning("FluidMeshRenderer: Shader 'Custom/FluidUnlit' was not found.");
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

    private int GetVerticalAxisIndex()
    {
        switch (fluidManager.GravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return 0;
            case FluidGravityAxis.NegativeZ:
                return 2;
            default:
                return 1;
        }
    }

    private Vector3Int GetDownDirection()
    {
        switch (fluidManager.GravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return Vector3Int.left;
            case FluidGravityAxis.NegativeZ:
                return new Vector3Int(0, 0, -1);
            default:
                return Vector3Int.down;
        }
    }

    private static readonly Vector3Int[] RenderLateralNegX =
    {
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };
    private static readonly Vector3Int[] RenderLateralNegZ =
    {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down
    };
    private static readonly Vector3Int[] RenderLateralNegY =
    {
        Vector3Int.right, Vector3Int.left,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    private Vector3Int[] GetLateralDirections()
    {
        switch (fluidManager.GravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return RenderLateralNegX;
            case FluidGravityAxis.NegativeZ:
                return RenderLateralNegZ;
            default:
                return RenderLateralNegY;
        }
    }

    private Vector3[] GetFace(Vector3 min, Vector3 max, Vector3Int direction)
    {
        if (direction == Vector3Int.right)
        {
            faceCornerBuffer[0] = new Vector3(max.x, min.y, min.z);
            faceCornerBuffer[1] = new Vector3(max.x, max.y, min.z);
            faceCornerBuffer[2] = new Vector3(max.x, max.y, max.z);
            faceCornerBuffer[3] = new Vector3(max.x, min.y, max.z);
        }
        else if (direction == Vector3Int.left)
        {
            faceCornerBuffer[0] = new Vector3(min.x, min.y, max.z);
            faceCornerBuffer[1] = new Vector3(min.x, max.y, max.z);
            faceCornerBuffer[2] = new Vector3(min.x, max.y, min.z);
            faceCornerBuffer[3] = new Vector3(min.x, min.y, min.z);
        }
        else if (direction == Vector3Int.up)
        {
            faceCornerBuffer[0] = new Vector3(min.x, max.y, min.z);
            faceCornerBuffer[1] = new Vector3(min.x, max.y, max.z);
            faceCornerBuffer[2] = new Vector3(max.x, max.y, max.z);
            faceCornerBuffer[3] = new Vector3(max.x, max.y, min.z);
        }
        else if (direction == Vector3Int.down)
        {
            faceCornerBuffer[0] = new Vector3(min.x, min.y, max.z);
            faceCornerBuffer[1] = new Vector3(min.x, min.y, min.z);
            faceCornerBuffer[2] = new Vector3(max.x, min.y, min.z);
            faceCornerBuffer[3] = new Vector3(max.x, min.y, max.z);
        }
        else if (direction.z > 0)
        {
            faceCornerBuffer[0] = new Vector3(min.x, min.y, max.z);
            faceCornerBuffer[1] = new Vector3(min.x, max.y, max.z);
            faceCornerBuffer[2] = new Vector3(max.x, max.y, max.z);
            faceCornerBuffer[3] = new Vector3(max.x, min.y, max.z);
        }
        else
        {
            faceCornerBuffer[0] = new Vector3(max.x, min.y, min.z);
            faceCornerBuffer[1] = new Vector3(max.x, max.y, min.z);
            faceCornerBuffer[2] = new Vector3(min.x, max.y, min.z);
            faceCornerBuffer[3] = new Vector3(min.x, min.y, min.z);
        }

        return faceCornerBuffer;
    }

    private Vector3[] ToLocalCorners(Vector3[] worldCorners)
    {
        for (int i = 0; i < worldCorners.Length; i++)
        {
            localCornerBuffer[i] = transform.InverseTransformPoint(worldCorners[i]);
        }

        return localCornerBuffer;
    }

    private static void AddFace(
        Vector3[] corners,
        Color color,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color> colors)
    {
        int startIndex = vertices.Count;
        vertices.Add(corners[0]);
        vertices.Add(corners[1]);
        vertices.Add(corners[2]);
        vertices.Add(corners[3]);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        switch (axis)
        {
            case 0:
                return value.x;
            case 2:
                return value.z;
            default:
                return value.y;
        }
    }

    private static void SetAxis(ref Vector3 value, int axis, float axisValue)
    {
        switch (axis)
        {
            case 0:
                value.x = axisValue;
                break;
            case 2:
                value.z = axisValue;
                break;
            default:
                value.y = axisValue;
                break;
        }
    }

    private static void EnsureListCapacity<T>(List<T> list, int capacity)
    {
        if (list.Capacity < capacity)
        {
            list.Capacity = capacity;
        }
    }

    private struct RenderCellAggregate
    {
        public float Liters;
        public FluidDefinition Definition;
        public float DominantLiters;
    }
}












