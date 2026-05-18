using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

[System.Serializable]
public struct VoxelFaceTextureInfo
{
    public Vector3 faceNormal;
    public Vector2 uvBase;
    public Vector2 uvSize;
    public Texture2D sourceTexture;
    public bool isExposed;

    public VoxelFaceTextureInfo(Vector3 normal, Vector2 uvBase, Vector2 uvSize, Texture2D texture, bool exposed)
    {
        this.faceNormal = normal;
        this.uvBase = uvBase;
        this.uvSize = uvSize;
        this.sourceTexture = texture;
        this.isExposed = exposed;
    }
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Block : MonoBehaviour
{
    private VoxelManager voxelManager;
    private Vector3Int blockPosition;

    public int VoxelsPerBlock { get; private set; }

    [Range(0.01f, 1.0f)]
    public float diggingThreshold = 0.1f;
    [Tooltip("掘削処理の各レイヤー間の待機フレーム数。0にするとフリーズする可能性があります。")]
    [SerializeField] private int diggingFrameDelay = 1;
    private Color initialColor = Color.white;

    private float voxelWorldSize;

    private BlockMeshGenerator meshGenerator;
    private VoxelTextureExtractor textureExtractor;
    private BlockDiggingSystem diggingSystem;

    private static readonly Dictionary<BlockData, Material> sharedMaterialCache = new Dictionary<BlockData, Material>();

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private new MeshCollider collider;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        collider = GetComponent<MeshCollider>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        meshGenerator = new BlockMeshGenerator();
        textureExtractor = new VoxelTextureExtractor();
        diggingSystem = new BlockDiggingSystem();
    }

    public void Initialize(VoxelManager manager, Vector3Int position, int voxelsPerBlock, float worldBlockSize)
    {
        voxelManager = manager;
        blockPosition = position;
        VoxelsPerBlock = voxelsPerBlock;
        voxelWorldSize = worldBlockSize / VoxelsPerBlock;

        diggingSystem.Initialize(voxelManager, this, diggingThreshold,
            diggingFrameDelay, VoxelsPerBlock, blockPosition, voxelWorldSize, textureExtractor);
    }

    public BlockData GetRepresentativeBlockData()
    {
        if (voxelManager == null) return null;
        var voxels = voxelManager.GetVoxelsInBlock(blockPosition);
        foreach (var voxel in voxels)
        {
            if (voxel.isActive && voxel.blockData != null)
            {
                return voxel.blockData;
            }
        }
        return null;
    }

    public void TakeDamage(Vector3 localPos, int damage)
    {
        diggingSystem.TakeDamage(localPos, damage);
    }

    public async UniTask<int> DigVoxels(
        BoxCollider diggingArea,
        int damagePerHit,
        TerrainChangeReason changeReason = TerrainChangeReason.Digging,
        MiningInfo miningInfo = default,
        bool applyDropInitialForce = false)
    {
        return await diggingSystem.DigVoxels(diggingArea, damagePerHit, changeReason, miningInfo, applyDropInitialForce);
    }

    public void GenerateMesh()
    {
        var result = meshGenerator.GenerateMesh(this, voxelManager, blockPosition, VoxelsPerBlock, initialColor, mesh, collider);
        UpdateMaterials(result.submeshBlockData);
    }

    private void UpdateMaterials(List<BlockData> submeshBlockData)
    {
        if (submeshBlockData == null || submeshBlockData.Count == 0)
        {
            meshRenderer.sharedMaterials = new Material[0];
            return;
        }

        Material[] materials = new Material[submeshBlockData.Count];
        for (int i = 0; i < submeshBlockData.Count; i++)
        {
            materials[i] = GetOrCreateMaterial(submeshBlockData[i]);
        }
        meshRenderer.sharedMaterials = materials;
    }

    private static Material GetOrCreateMaterial(BlockData data)
    {
        if (sharedMaterialCache.TryGetValue(data, out Material existing) && existing != null)
        {
            return existing;
        }

        Material mat = new Material(Shader.Find("Custom/Default"));
        mat.renderQueue = RenderQueue.Geometry;
        if (data.textures != null && data.textures.Count > 0)
        {
            mat.mainTexture = data.textures[0];
        }
        sharedMaterialCache[data] = mat;
        return mat;
    }
}
