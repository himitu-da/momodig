using UnityEngine;
using System.Collections.Generic;

public class DroppedItem : MonoBehaviour
{
    public static readonly Vector3[] FaceNormals =
    {
        Vector3.right,
        Vector3.left,
        Vector3.up,
        Vector3.down,
        Vector3.forward,
        Vector3.back
    };

    public Rigidbody rb { get; private set; }
    public Collider ItemCollider => obstacleCollider;
    public Bounds ItemBounds => obstacleCollider != null ? obstacleCollider.bounds : new Bounds(transform.position, transform.localScale);
    public ResourceType resourceType = ResourceType.Stone;
    private static Mesh droppedItemMeshTemplate;

    [Header("Solidification")]
    public bool canSolidify = true;
    public bool hasSolidificationTarget;
    public Vector3Int solidifiedBlockPosition;
    public Vector3Int solidifiedLocalVoxelPosition;

    private const float FluidNotifyInterval = 0.1f;
    private const float FluidVelocityEpsilon = 0.0001f;

    [Header("流体シミュレーション設定")]
    [SerializeField, InspectorName("流体抵抗を有効化"), Tooltip("流体からの抵抗をシミュレーションに適用するかどうか")]
    private bool enableFluidResistance = true;
    [SerializeField, InspectorName("線形抵抗の強さ"), Tooltip("移動に対する流体抵抗の強さ")]
    private float fluidLinearResistanceStrength = 6f;
    [SerializeField, InspectorName("回転抵抗の強さ"), Tooltip("回転に対する流体抵抗の強さ")]
    private float fluidAngularResistanceStrength = 4f;
    [SerializeField, InspectorName("水平サンプル数"), Tooltip("流体抵抗を計算するための水平方向のサンプリング数")]
    private int fluidHorizontalSampleCount = 2;
    [SerializeField, InspectorName("垂直サンプル数"), Tooltip("流体抵抗を計算するための垂直方向のサンプリング数")]
    private int fluidVerticalSampleCount = 2;
    [SerializeField, InspectorName("深度サンプル数"), Tooltip("流体抵抗を計算するための深度(Z軸)方向のサンプリング数")]
    private int fluidDepthSampleCount = 2;
    [SerializeField, InspectorName("サンプルインセット"), Tooltip("サンプリング位置をバウンディングボックスより内側にオフセットする量")]
    private float fluidSampleInset = 0.01f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh instanceMesh;
    private FluidManager fluidManager;
    private Collider obstacleCollider;
    private Vector3 lastFluidNotifyPosition;
    private bool hasFluidNotifyPosition;
    private float nextFluidNotifyTime;
    private bool anchoredPhysicsMode;

    // --- For Persistence ---
    public Vector3 scale;
    public string blockDataName;
    public Vector2 uvBase;
    public Vector2 uvSize;
    public bool useTexture1;
    public DroppedItemFaceTextureData[] faceTextureData = new DroppedItemFaceTextureData[FaceNormals.Length];

    public void ResetSolidificationState()
    {
        canSolidify = true;
        hasSolidificationTarget = false;
        solidifiedBlockPosition = Vector3Int.zero;
        solidifiedLocalVoxelPosition = Vector3Int.zero;
    }

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        EnsureDroppedItemMesh();
        rb = GetComponent<Rigidbody>();
        obstacleCollider = GetComponent<Collider>();
        ResolveFluidManager();
        lastFluidNotifyPosition = GetFluidObstacleCenter();
        hasFluidNotifyPosition = true;
    }

    void OnEnable()
    {
        anchoredPhysicsMode = false;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        RefreshFluidObstacleTracking(true);
    }

    void OnValidate()
    {
        fluidLinearResistanceStrength = Mathf.Max(0f, fluidLinearResistanceStrength);
        fluidAngularResistanceStrength = Mathf.Max(0f, fluidAngularResistanceStrength);
        fluidHorizontalSampleCount = Mathf.Max(1, fluidHorizontalSampleCount);
        fluidVerticalSampleCount = Mathf.Max(1, fluidVerticalSampleCount);
        fluidDepthSampleCount = Mathf.Max(1, fluidDepthSampleCount);
        fluidSampleInset = Mathf.Max(0f, fluidSampleInset);
    }

    void OnDisable()
    {
        RefreshFluidObstacleTracking(true);
    }

    void FixedUpdate()
    {
        if (anchoredPhysicsMode)
        {
            return;
        }

        ApplyFluidResistance();
        RefreshFluidObstacleTracking(false);
    }

    public void SetAnchoredPhysicsMode(bool anchored)
    {
        anchoredPhysicsMode = anchored;
        if (anchored)
        {
            RefreshFluidObstacleTracking(true);
        }
        else
        {
            lastFluidNotifyPosition = GetFluidObstacleCenter();
            hasFluidNotifyPosition = true;
            nextFluidNotifyTime = 0f;
            RefreshFluidObstacleTracking(true);
        }
    }

    void OnDestroy()
    {
        RefreshFluidObstacleTracking(true);

        if (instanceMesh != null)
        {
            Destroy(instanceMesh);
        }
    }

    private void ResolveFluidManager()
    {
        if (fluidManager != null)
        {
            return;
        }

        TerrainManager terrainManager = FindFirstObjectByType<TerrainManager>();
        if (terrainManager != null)
        {
            fluidManager = terrainManager.FluidManager;
        }
    }

    private void ApplyFluidResistance()
    {
        if (!enableFluidResistance || rb == null || rb.isKinematic)
        {
            return;
        }

        ResolveFluidManager();
        if (fluidManager == null)
        {
            return;
        }

        float submersion = GetFluidSubmersionRatio();
        if (submersion <= 0f)
        {
            return;
        }

        FluidDefinition fluidDef = fluidManager.GetFluidDefinitionAtWorldPosition(GetFluidObstacleCenter());
        float fluidDensity = fluidDef != null ? fluidDef.densityKgPerCubicMeter : 1000f;
        float volume = obstacleCollider != null ? (obstacleCollider.bounds.size.x * obstacleCollider.bounds.size.y * obstacleCollider.bounds.size.z) : 1f;
        
        float displacedFluidMass = volume * fluidDensity * submersion;
        float effectiveMass = Mathf.Max(0.1f, rb.mass);
        
        float resistanceFactor = Mathf.Clamp01(displacedFluidMass / effectiveMass);

        float linearDamping = 1f - Mathf.Exp(-fluidLinearResistanceStrength * resistanceFactor * Time.fixedDeltaTime);
        float angularDamping = 1f - Mathf.Exp(-fluidAngularResistanceStrength * resistanceFactor * Time.fixedDeltaTime);

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, linearDamping);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, angularDamping);
    }

    private void RefreshFluidObstacleTracking(bool force)
    {
        ResolveFluidManager();
        if (fluidManager == null)
        {
            return;
        }

        Vector3 currentPosition = GetFluidObstacleCenter();
        float movementThreshold = Mathf.Max(0.02f, fluidManager.InternalVoxelSize * 0.5f);
        bool movedEnough = !hasFluidNotifyPosition || (currentPosition - lastFluidNotifyPosition).sqrMagnitude >= movementThreshold * movementThreshold;
        bool isMoving = rb != null && (rb.linearVelocity.sqrMagnitude > FluidVelocityEpsilon || rb.angularVelocity.sqrMagnitude > FluidVelocityEpsilon);

        if (!force)
        {
            if (!movedEnough && !isMoving)
            {
                return;
            }

            if (Time.time < nextFluidNotifyTime)
            {
                return;
            }
        }

        int dirtyRadius = GetFluidDirtyRadius();
        if (hasFluidNotifyPosition)
        {
            fluidManager.MarkDirtyAroundWorldPosition(lastFluidNotifyPosition, dirtyRadius);
        }

        fluidManager.MarkDirtyAroundWorldPosition(currentPosition, dirtyRadius);
        lastFluidNotifyPosition = currentPosition;
        hasFluidNotifyPosition = true;
        nextFluidNotifyTime = Time.time + FluidNotifyInterval;
    }

    private float GetFluidSubmersionRatio()
    {
        if (fluidManager == null)
        {
            return 0f;
        }

        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<Collider>();
        }

        Bounds bounds = obstacleCollider != null ? obstacleCollider.bounds : new Bounds(transform.position, Vector3.one * 0.25f);
        Vector3 min = bounds.min + Vector3.one * fluidSampleInset;
        Vector3 max = bounds.max - Vector3.one * fluidSampleInset;

        if (min.x > max.x)
        {
            min.x = bounds.center.x;
            max.x = bounds.center.x;
        }

        if (min.y > max.y)
        {
            min.y = bounds.center.y;
            max.y = bounds.center.y;
        }

        if (min.z > max.z)
        {
            min.z = bounds.center.z;
            max.z = bounds.center.z;
        }

        float totalFillRatio = 0f;
        int sampleCount = 0;

        for (int x = 0; x < fluidHorizontalSampleCount; x++)
        {
            float sampleX = Mathf.Lerp(min.x, max.x, GetFluidSampleLerp(x, fluidHorizontalSampleCount));
            for (int y = 0; y < fluidVerticalSampleCount; y++)
            {
                float sampleY = Mathf.Lerp(min.y, max.y, GetFluidSampleLerp(y, fluidVerticalSampleCount));
                for (int z = 0; z < fluidDepthSampleCount; z++)
                {
                    float sampleZ = Mathf.Lerp(min.z, max.z, GetFluidSampleLerp(z, fluidDepthSampleCount));
                    totalFillRatio += fluidManager.GetFluidFillRatioAtWorldPosition(new Vector3(sampleX, sampleY, sampleZ));
                    sampleCount++;
                }
            }
        }

        return sampleCount > 0 ? Mathf.Clamp01(totalFillRatio / sampleCount) : 0f;
    }

    private static float GetFluidSampleLerp(int index, int sampleCount)
    {
        if (sampleCount <= 1)
        {
            return 0.5f;
        }

        return index / (float)(sampleCount - 1);
    }

    private Vector3 GetFluidObstacleCenter()
    {
        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<Collider>();
        }

        return obstacleCollider != null ? obstacleCollider.bounds.center : transform.position;
    }

    private int GetFluidDirtyRadius()
    {
        if (fluidManager == null)
        {
            return 1;
        }

        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<Collider>();
        }

        Bounds bounds = obstacleCollider != null ? obstacleCollider.bounds : new Bounds(transform.position, transform.localScale);
        float maxExtent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
        return Mathf.Max(1, Mathf.CeilToInt(maxExtent / Mathf.Max(0.001f, fluidManager.InternalVoxelSize)) + 1);
    }

    public void ApplyFaceTextureInfos(List<VoxelFaceTextureInfo> faceInfos, Texture2D texture1, Texture2D texture2)
    {
        DroppedItemFaceTextureData[] newFaceData = new DroppedItemFaceTextureData[FaceNormals.Length];

        for (int i = 0; i < FaceNormals.Length; i++)
        {
            VoxelFaceTextureInfo faceInfo = faceInfos.Find(info => info.faceNormal == FaceNormals[i]);
            bool hasTexture = faceInfo.sourceTexture != null;
            bool usesTexture1 = hasTexture && faceInfo.sourceTexture == texture1;
            newFaceData[i] = new DroppedItemFaceTextureData(faceInfo.uvBase, faceInfo.uvSize, usesTexture1, hasTexture);
        }

        ApplyFaceTextureData(newFaceData, texture1, texture2);
    }

    public void ApplyFaceTextureData(DroppedItemFaceTextureData[] newFaceData, Texture2D texture1, Texture2D texture2)
    {
        EnsureDroppedItemMesh();

        if (newFaceData == null || newFaceData.Length != FaceNormals.Length)
        {
            faceTextureData = new DroppedItemFaceTextureData[FaceNormals.Length];
        }
        else
        {
            faceTextureData = (DroppedItemFaceTextureData[])newFaceData.Clone();
        }

        UpdateLegacyFaceFields();
        UpdateMeshUvs();
        ApplyMaterial(ResolveSourceTexture(texture1, texture2));
    }

    public static DroppedItemFaceTextureData[] CreateLegacyFaceTextureData(Vector2 legacyUvBase, Vector2 legacyUvSize, bool legacyUseTexture1)
    {
        DroppedItemFaceTextureData[] legacyData = new DroppedItemFaceTextureData[FaceNormals.Length];
        for (int i = 0; i < legacyData.Length; i++)
        {
            legacyData[i] = new DroppedItemFaceTextureData(legacyUvBase, legacyUvSize, legacyUseTexture1, true);
        }

        return legacyData;
    }

    private void EnsureDroppedItemMesh()
    {
        if (meshFilter == null)
        {
            return;
        }

        if (droppedItemMeshTemplate == null)
        {
            droppedItemMeshTemplate = CreateDroppedItemMeshTemplate();
            droppedItemMeshTemplate.name = "DroppedItemCubeMeshTemplate";
        }

        if (instanceMesh == null)
        {
            instanceMesh = Instantiate(droppedItemMeshTemplate);
            instanceMesh.name = "DroppedItemCubeMesh";
        }

        if (meshFilter.sharedMesh != instanceMesh)
        {
            meshFilter.sharedMesh = instanceMesh;
        }
    }

    private void ApplyMaterial(Texture2D sourceTexture)
    {
        if (meshRenderer == null)
        {
            return;
        }

        Material material = meshRenderer.material;
        if (material == null || material.shader == null || material.shader.name != "Custom/Default")
        {
            material = new Material(Shader.Find("Custom/Default"));
            meshRenderer.material = material;
        }

        material.renderQueue = RenderQueue.Geometry;
        material.color = Color.white;
        material.mainTexture = sourceTexture;
    }

    private Texture2D ResolveSourceTexture(Texture2D texture1, Texture2D texture2)
    {
        if (faceTextureData == null)
        {
            return null;
        }

        for (int i = 0; i < faceTextureData.Length; i++)
        {
            if (!faceTextureData[i].hasTexture)
            {
                continue;
            }

            return faceTextureData[i].useTexture1 ? texture1 : texture2;
        }

        return null;
    }

    private void UpdateLegacyFaceFields()
    {
        if (faceTextureData == null || faceTextureData.Length == 0)
        {
            uvBase = Vector2.zero;
            uvSize = Vector2.zero;
            useTexture1 = false;
            return;
        }

        int representativeIndex = 4;
        if (!faceTextureData[representativeIndex].hasTexture)
        {
            representativeIndex = 0;
            for (int i = 0; i < faceTextureData.Length; i++)
            {
                if (faceTextureData[i].hasTexture)
                {
                    representativeIndex = i;
                    break;
                }
            }
        }

        uvBase = faceTextureData[representativeIndex].uvBase;
        uvSize = faceTextureData[representativeIndex].uvSize;
        useTexture1 = faceTextureData[representativeIndex].useTexture1;
    }

    private void UpdateMeshUvs()
    {
        if (instanceMesh == null)
        {
            return;
        }

        Vector2[] uvs = new Vector2[FaceNormals.Length * 4];
        for (int i = 0; i < FaceNormals.Length; i++)
        {
            Vector2[] faceUvs = BuildFaceUvs(FaceNormals[i], faceTextureData[i]);
            int offset = i * 4;
            uvs[offset + 0] = faceUvs[0];
            uvs[offset + 1] = faceUvs[1];
            uvs[offset + 2] = faceUvs[2];
            uvs[offset + 3] = faceUvs[3];
        }

        instanceMesh.uv = uvs;
    }

    private Vector2[] BuildFaceUvs(Vector3 normal, DroppedItemFaceTextureData data)
    {
        if (!data.hasTexture)
        {
            return new[]
            {
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero
            };
        }

        float uBase = data.uvBase.x;
        float vBase = data.uvBase.y;
        float uSize = data.uvSize.x;
        float vSize = data.uvSize.y;

        if (normal == Vector3.left || normal == Vector3.back)
        {
            return new[]
            {
                new Vector2(uBase + uSize, vBase),
                new Vector2(uBase + uSize, vBase + vSize),
                new Vector2(uBase, vBase + vSize),
                new Vector2(uBase, vBase)
            };
        }

        if (normal == Vector3.down)
        {
            return new[]
            {
                new Vector2(uBase, vBase + vSize),
                new Vector2(uBase, vBase),
                new Vector2(uBase + uSize, vBase),
                new Vector2(uBase + uSize, vBase + vSize)
            };
        }

        return new[]
        {
            new Vector2(uBase, vBase),
            new Vector2(uBase, vBase + vSize),
            new Vector2(uBase + uSize, vBase + vSize),
            new Vector2(uBase + uSize, vBase)
        };
    }

    private static Mesh CreateDroppedItemMeshTemplate()
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>(24);
        List<int> triangles = new List<int>(36);
        List<Vector2> uvs = new List<Vector2>(24);

        AddFace(
            vertices,
            triangles,
            uvs,
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            false);

        AddFace(
            vertices,
            triangles,
            uvs,
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            false);

        AddFace(
            vertices,
            triangles,
            uvs,
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            false);

        AddFace(
            vertices,
            triangles,
            uvs,
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            false);

        AddFace(
            vertices,
            triangles,
            uvs,
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            true);

        AddFace(
            vertices,
            triangles,
            uvs,
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            true);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddFace(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        bool reverseTriangles)
    {
        int startIndex = vertices.Count;
        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        if (reverseTriangles)
        {
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 3);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 1);
        }
        else
        {
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        uvs.Add(Vector2.zero);
        uvs.Add(Vector2.up);
        uvs.Add(Vector2.one);
        uvs.Add(Vector2.right);
    }
}

