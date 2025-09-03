using UnityEngine;
using System.Collections.Generic;

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

    public int ChunkSize { get; private set; } = 4; // 16ドット単位の塊　この変数いらない説
    // voxelTypesとvoxelHPsはVoxelManagerで管理するため削除
    // private byte[,,] voxelTypes; // 0: 空気, 1: 固体
    // private int[,,] voxelHPs; // HP
    private int maxHP = 3;
    [Range(0.01f, 1.0f)]
    public float diggingThreshold = 0.1f; // 掘削判定の閾値（ボクセルとの重複率）
    [Tooltip("掘削処理の各レイヤー間の待機フレーム数。0にするとフリーズする可能性があります。")]
    [SerializeField] private int diggingFrameDelay = 1;
    private Color initialColor = Color.white;
    [SerializeField] private Texture2D texture1, texture2;
    private bool[,,] useTexture1Pattern; // テクスチャパターン
    private float voxelSize; // BaseCubePlacerから受け取る
    
    // メッシュ生成システム
    private BlockMeshGenerator meshGenerator;
    
    // テクスチャ抽出システム
    private VoxelTextureExtractor textureExtractor;
    
    // アイテムドロップシステム
    private BlockItemDropper itemDropper;
    
    [Header("Dropped Item Settings")]
    private GameObject droppedItemPrefab; // ドロップアイテムのPrefab（オプション）
    private bool disableRotation = true; // 回転を無効化するかどうか
    private bool autoScale = true; // Prefabのスケールを自動調整するかどうか
    private float scaleMultiplier = 0.8f; // スケール倍率（voxelSizeに対する倍率）
    
    [Header("Voxel Texture Extraction")]
    [SerializeField] private bool enableTextureExtraction = true; // ボクセルテクスチャ抽出を有効にするか
    [SerializeField] private int extractedTextureResolution = 32; // 抽出テクスチャの解像度

    private Mesh mesh;
    private MeshFilter meshFilter;
    private new MeshCollider collider; // newキーワード追加で警告解決

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        collider = GetComponent<MeshCollider>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;
        
        // メッシュ生成システムを初期化
        meshGenerator = new BlockMeshGenerator();
        
        // テクスチャ抽出システムを初期化
        textureExtractor = new VoxelTextureExtractor(enableTextureExtraction, extractedTextureResolution);
        
        // アイテムドロップシステムを初期化
        itemDropper = new BlockItemDropper();
    }

    private BlockData blockData; // このブロックの種類を定義するデータ

    // Initializeメソッドをオーバーロードではなく、オプション引数を持つ単一のメソッドに統合
    /// <summary>
    /// ブロックを初期化
    /// </summary>
    public void Initialize(
        VoxelManager manager, Vector3Int position, bool[,,] pattern, int newChunkSize, float worldChunkSize, BlockData data)
    {
        voxelManager = manager;
        blockPosition = position;
        ChunkSize = newChunkSize;
        voxelSize = worldChunkSize / ChunkSize;
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
        useTexture1Pattern = pattern ?? new bool[ChunkSize, ChunkSize, ChunkSize];

        // アイテムドロップシステムを初期化
        itemDropper.Initialize(blockData, enableTextureExtraction, voxelSize, ChunkSize, 
            textureExtractor, texture1, texture2, useTexture1Pattern);

        // メッシュ生成はVoxelManagerへのデータ登録後に外部から呼び出す
        // GenerateMesh();
    }

    public void TakeDamage(Vector3 localPos, int damage)
    {
        int x = Mathf.FloorToInt(localPos.x + ChunkSize / 2.0f);
        int y = Mathf.FloorToInt(localPos.y + ChunkSize / 2.0f);
        int z = Mathf.FloorToInt(localPos.z + ChunkSize / 2.0f);
        
        Vector3Int localVoxelPos = new Vector3Int(x, y, z);

        // VoxelManagerにダメージ処理を移管
        if (voxelManager.DamageVoxel(blockPosition, localVoxelPos, damage))
        {
            // Voxelが破壊された場合
            var voxelData = voxelManager.GetVoxelAt(blockPosition, localVoxelPos);
            if (voxelData != null)
            {
                 itemDropper.DropItem(voxelData.worldPosition, x, y, z);
            }
            GenerateMesh(); // メッシュを更新
        }
    }

    public System.Collections.IEnumerator DigVoxels(BoxCollider diggingArea)
    {
        const int sampleResolution = 3;
        const int totalSamples = sampleResolution * sampleResolution * sampleResolution;

        Matrix4x4 worldToLocalMatrix = transform.worldToLocalMatrix;
        Matrix4x4 diggingAreaWorldToLocal = diggingArea.transform.worldToLocalMatrix;
        Vector3 halfSize = diggingArea.size * 0.5f;
        Vector3 center = diggingArea.center;

        Bounds diggingBounds = diggingArea.bounds;
        Vector3 localMin = worldToLocalMatrix.MultiplyPoint3x4(diggingBounds.min);
        Vector3 localMax = worldToLocalMatrix.MultiplyPoint3x4(diggingBounds.max);

        int startX = Mathf.Max(0, Mathf.FloorToInt(localMin.x + ChunkSize / 2.0f));
        int endX = Mathf.Min(ChunkSize - 1, Mathf.CeilToInt(localMax.x + ChunkSize / 2.0f));
        int startY = Mathf.Max(0, Mathf.FloorToInt(localMin.y + ChunkSize / 2.0f));
        int endY = Mathf.Min(ChunkSize - 1, Mathf.CeilToInt(localMax.y + ChunkSize / 2.0f));
        int startZ = Mathf.Max(0, Mathf.FloorToInt(localMin.z + ChunkSize / 2.0f));
        int endZ = Mathf.Min(ChunkSize - 1, Mathf.CeilToInt(localMax.z + ChunkSize / 2.0f));

        // 現在の移動モードを取得
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        PlayerController.MoveMode moveMode = PlayerController.MoveMode.TopDown;
        if (playerController != null)
        {
            moveMode = playerController.currentMoveMode;
        }

        if (moveMode == PlayerController.MoveMode.SideScroller)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                bool layerModified = false;
                List<System.Action> dropActions = new List<System.Action>();

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (ProcessVoxel(x, y, z, diggingArea, sampleResolution, totalSamples, worldToLocalMatrix, diggingAreaWorldToLocal, halfSize, center, dropActions))
                        {
                            layerModified = true;
                        }
                    }
                }

                if (layerModified)
                {
                    foreach (var action in dropActions) action.Invoke();
                    GenerateMesh();
                }

                int delay = Mathf.Max(1, diggingFrameDelay);
                for (int i = 0; i < delay; i++)
                {
                    yield return null;
                }
            }
        }
        else // TopDown or other modes
        {
            for (int y = endY; y >= startY; y--)
            {
                bool layerModified = false;
                List<System.Action> dropActions = new List<System.Action>();

                for (int x = startX; x <= endX; x++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        if (ProcessVoxel(x, y, z, diggingArea, sampleResolution, totalSamples, worldToLocalMatrix, diggingAreaWorldToLocal, halfSize, center, dropActions))
                        {
                            layerModified = true;
                        }
                    }
                }

                if (layerModified)
                {
                    foreach (var action in dropActions) action.Invoke();
                    GenerateMesh();
                }

                int delay = Mathf.Max(1, diggingFrameDelay);
                for (int i = 0; i < delay; i++)
                {
                    yield return null;
                }
            }
        }
    }

    private bool ProcessVoxel(int x, int y, int z, BoxCollider diggingArea, int sampleResolution, int totalSamples, Matrix4x4 worldToLocalMatrix, Matrix4x4 diggingAreaWorldToLocal, Vector3 halfSize, Vector3 center, List<System.Action> dropActions)
    {
        Vector3Int localVoxelPos = new Vector3Int(x, y, z);
        var voxelData = voxelManager.GetVoxelAt(blockPosition, localVoxelPos);
        if (voxelData == null || !voxelData.isActive) return false;

        int containedSamples = 0;
        Vector3 voxelMin = new Vector3(x - ChunkSize / 2.0f, y - ChunkSize / 2.0f, z - ChunkSize / 2.0f);

        for (int sx = 0; sx < sampleResolution; sx++)
        {
            for (int sy = 0; sy < sampleResolution; sy++)
            {
                for (int sz = 0; sz < sampleResolution; sz++)
                {
                    float sampleX = voxelMin.x + (sx + 0.5f) / sampleResolution;
                    float sampleY = voxelMin.y + (sy + 0.5f) / sampleResolution;
                    float sampleZ = voxelMin.z + (sz + 0.5f) / sampleResolution;
                    Vector3 sampleLocalPos = new Vector3(sampleX, sampleY, sampleZ);
                    Vector3 sampleWorldPos = transform.TransformPoint(sampleLocalPos);
                    Vector3 localPosInDiggingArea = diggingAreaWorldToLocal.MultiplyPoint3x4(sampleWorldPos);

                    if (Mathf.Abs(localPosInDiggingArea.x - center.x) <= halfSize.x &&
                        Mathf.Abs(localPosInDiggingArea.y - center.y) <= halfSize.y &&
                        Mathf.Abs(localPosInDiggingArea.z - center.z) <= halfSize.z)
                    {
                        containedSamples++;
                    }
                }
            }
        }

        float overlapRatio = (float)containedSamples / totalSamples;
        if (overlapRatio >= diggingThreshold)
        {
            if (voxelManager.DamageVoxel(blockPosition, localVoxelPos, 1))
            {
                dropActions.Add(() => itemDropper.DropItem(voxelData.worldPosition, x, y, z));
                return true;
            }
        }
        return false;
    }

    public void GenerateMesh()
    {
        meshGenerator.GenerateMesh(this, voxelManager, blockPosition, ChunkSize, maxHP, initialColor, mesh, collider);
    }

    /// <summary>
    /// 指定されたボクセルの面テクスチャ情報を取得
    /// </summary>
    public VoxelFaceTextureInfo GetVoxelFaceTextureInfo(int voxelX, int voxelY, int voxelZ, Vector3 normal)
    {
        return textureExtractor.GetVoxelFaceTextureInfo(voxelX, voxelY, voxelZ, normal, 
            texture1, texture2, useTexture1Pattern, ChunkSize);
    }

    /// <summary>
    /// ドロップアイテムにボクセルテクスチャを適用
    /// </summary>
    private void ApplyVoxelTextureToDroppedItem(GameObject item, int voxelX, int voxelY, int voxelZ)
    {
        textureExtractor.ApplyVoxelTextureToDroppedItem(item, voxelX, voxelY, voxelZ, 
            texture1, texture2, useTexture1Pattern, ChunkSize);
    }

}
