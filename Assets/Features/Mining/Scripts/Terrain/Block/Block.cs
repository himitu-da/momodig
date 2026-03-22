using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// ボクセルの面テクスチャ情報
/// </summary>
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
    // VoxelManagerへの参照を追加
    private VoxelManager voxelManager;
    private Vector3Int blockPosition; // このブロックの座標を保持

    public int VoxelsPerBlock { get; private set; }
    private int maxHP;
    [Range(0.01f, 1.0f)]
    public float diggingThreshold = 0.1f; // 掘削判定の閾値（ボクセルとの重複率）
    [Tooltip("掘削処理の各レイヤー間の待機フレーム数。0にするとフリーズする可能性があります。")]
    [SerializeField] private int diggingFrameDelay = 1;
    private Color initialColor = Color.white;
    [SerializeField] private Texture2D texture1, texture2;
    private bool[,,] useTexture1Pattern;
    
    private float voxelWorldSize; // 1ボクセルのワールドサイズ
    
    // メッシュ生成システム
    private BlockMeshGenerator meshGenerator;
    
    // テクスチャ抽出システム
    private VoxelTextureExtractor textureExtractor;
    
    // アイテムドロップシステム
    private BlockItemDropper itemDropper;
    
    // 掘削システム
    private BlockDiggingSystem diggingSystem;
    
    [Header("Dropped Item Settings")]
    private GameObject droppedItemPrefab; // ドロップアイテムのPrefab（オプション）
    private bool disableRotation; // 回転を無効化するかどうか
    private bool autoScale; // Prefabのスケールを自動調整するかどうか
    private float scaleMultiplier; // スケール倍率（voxelSizeに対する倍率）
    
    [Header("Voxel Texture Extraction")]
    [SerializeField] private bool enableTextureExtraction = true; // ボクセルテクスチャ抽出を有効にするか
    
    private Mesh mesh;
    private MeshFilter meshFilter;
    private new MeshCollider collider; // newキーワード追加で警告解決

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        collider = GetComponent<MeshCollider>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        
        // 各システムを初期化
        meshGenerator = new BlockMeshGenerator();
        textureExtractor = new VoxelTextureExtractor();
        itemDropper = new BlockItemDropper();
        diggingSystem = new BlockDiggingSystem();
    }

    private BlockData blockData; // このブロックの種類を定義するデータ
    public BlockData BlockData => blockData; // 外部からBlockDataを参照するためのプロパティ

    // Initializeメソッドをオーバーロードではなく、オプション引数を持つ単一のメソッドに統合
    /// <summary>
    /// ブロックを初期化
    /// </summary>
    public void Initialize(
        VoxelManager manager, Vector3Int position, bool[,,] pattern, int voxelsPerBlock, float worldBlockSize, BlockData data)
    {
        voxelManager = manager;
        blockPosition = position;
        VoxelsPerBlock = voxelsPerBlock;
        voxelWorldSize = worldBlockSize / VoxelsPerBlock;
        blockData = data; // BlockDataアセットを保持

        // BlockDataから各種設定を読み込む
        maxHP = blockData.voxelHp;
        droppedItemPrefab = blockData.droppedItemPrefab;
        disableRotation = blockData.disableRotation;
        autoScale = blockData.autoScale;
        scaleMultiplier = blockData.scaleMultiplier;
        
        // テクスチャを設定 (最初のテクスチャをtexture1, 2番目をtexture2とする)
        texture1 = (blockData.textures != null && blockData.textures.Count > 0) ? blockData.textures[0] : null;
        texture2 = (blockData.textures != null && blockData.textures.Count > 1) ? blockData.textures[1] : null;

        // テクスチャパターンを設定
        useTexture1Pattern = pattern ?? new bool[VoxelsPerBlock, VoxelsPerBlock, VoxelsPerBlock];

        // System initializations
        itemDropper.Initialize(blockData, enableTextureExtraction, voxelWorldSize, VoxelsPerBlock, 
            textureExtractor, texture1, texture2, useTexture1Pattern);

        diggingSystem.Initialize(voxelManager, itemDropper, this, diggingThreshold, 
            diggingFrameDelay, VoxelsPerBlock, blockPosition);

        // メッシュ生成はVoxelManagerへのデータ登録後に外部から呼び出す
        // GenerateMesh();
    }

    public void TakeDamage(Vector3 localPos, int damage)
    {
        diggingSystem.TakeDamage(localPos, damage);
    }

    public async UniTask<int> DigVoxels(BoxCollider diggingArea, int damagePerHit)
    {
        return await diggingSystem.DigVoxels(diggingArea, damagePerHit);
    }

    public void GenerateMesh()
    {
        meshGenerator.GenerateMesh(this, voxelManager, blockPosition, VoxelsPerBlock, maxHP, initialColor, mesh, collider);
    }

}
