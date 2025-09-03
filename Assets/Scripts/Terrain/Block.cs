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
                 DropItem(voxelData.worldPosition, x, y, z);
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

        PlayerController.MoveMode moveMode = GetCurrentMoveMode();

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
                dropActions.Add(() => DropItem(voxelData.worldPosition, x, y, z));
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
    private VoxelFaceTextureInfo GetVoxelFaceTextureInfo(int voxelX, int voxelY, int voxelZ, Vector3 normal)
    {
        // ボクセル位置の境界チェック
        if (voxelX < 0 || voxelX >= ChunkSize || voxelY < 0 || voxelY >= ChunkSize || voxelZ < 0 || voxelZ >= ChunkSize)
        {
            Debug.LogWarning($"Voxel position ({voxelX}, {voxelY}, {voxelZ}) is out of bounds (chunk size: {ChunkSize})");
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }

        // AddFaceメソッドのUV計算ロジックを再利用
        float pixelSize = 1.0f / ChunkSize;
        float u_base = 0;
        float v_base = 0;

        if (normal == Vector3.right || normal == Vector3.left) // X面
        {
            u_base = (float)voxelZ / ChunkSize;
            v_base = (float)voxelY / ChunkSize;
        }
        else if (normal == Vector3.up || normal == Vector3.down) // Y面
        {
            u_base = (float)voxelX / ChunkSize;
            v_base = (float)voxelZ / ChunkSize;
        }
        else if (normal == Vector3.forward || normal == Vector3.back) // Z面
        {
            u_base = (float)voxelX / ChunkSize;
            v_base = (float)voxelY / ChunkSize;
        }

        // useTexture1Patternの配列チェック
        if (useTexture1Pattern == null)
        {
            Debug.LogWarning("useTexture1Pattern is null");
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }

        // 適切なテクスチャを選択
        bool useTexture1 = useTexture1Pattern[voxelX, voxelY, voxelZ];
        Texture2D sourceTexture = useTexture1 ? texture1 : texture2;
        
        // デバッグ情報
        // Debug.Log($"Voxel ({voxelX}, {voxelY}, {voxelZ}) - useTexture1: {useTexture1}, texture1: {(texture1 != null ? texture1.name : "null")}, texture2: {(texture2 != null ? texture2.name : "null")}, selected: {(sourceTexture != null ? sourceTexture.name : "null")}");
        
        if (sourceTexture == null)
        {
            Debug.LogWarning($"Source texture is null for voxel at ({voxelX}, {voxelY}, {voxelZ}), useTexture1: {useTexture1}");
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }
        
        // 露出判定（簡略版 - 後で拡張可能）
        bool isExposed = true; // 現在は常にtrue、後で隣接ボクセルチェックを追加可能

        VoxelFaceTextureInfo result = new VoxelFaceTextureInfo(
            normal,
            new Vector2(u_base, v_base),
            new Vector2(pixelSize, pixelSize),
            sourceTexture,
            isExposed
        );
        
        // Debug.Log($"Generated texture info for face {normal}: uvBase={result.uvBase}, uvSize={result.uvSize}");
        return result;
    }

    /// <summary>
    /// 指定されたボクセルのすべての面テクスチャ情報を取得
    /// </summary>
    private List<VoxelFaceTextureInfo> GetAllVoxelFaceTextureInfo(int voxelX, int voxelY, int voxelZ)
    {
        List<VoxelFaceTextureInfo> faceInfos = new List<VoxelFaceTextureInfo>();
        
        // 6つの面すべてのテクスチャ情報を取得
        Vector3[] faceNormals = {
            Vector3.right, Vector3.left,
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back
        };

        foreach (Vector3 normal in faceNormals)
        {
            VoxelFaceTextureInfo faceInfo = GetVoxelFaceTextureInfo(voxelX, voxelY, voxelZ, normal);
            faceInfos.Add(faceInfo);
        }

        return faceInfos;
    }

    /// <summary>
    /// ドロップアイテムにボクセルテクスチャを適用
    /// </summary>
    private void ApplyVoxelTextureToDroppedItem(GameObject item, int voxelX, int voxelY, int voxelZ)
    {
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer == null) return;

        // テクスチャ抽出が無効、またはテクスチャが設定されていない場合はスキップ
        if (!enableTextureExtraction || (texture1 == null && texture2 == null))
        {
            if (enableTextureExtraction && texture1 == null && texture2 == null)
            {
                Debug.LogWarning("Texture extraction is enabled but no textures are assigned. Please assign texture1 and/or texture2 in the Inspector.");
            }
            ApplyDefaultMaterial(itemRenderer);
            return;
        }

        // デバッグ情報の表示
        if (enableTextureExtraction)
        {
            // Debug.Log($"=== Applying voxel texture for position ({voxelX}, {voxelY}, {voxelZ}) ===");
            // Debug.Log($"texture1: {(texture1 != null ? texture1.name : "null")}, texture2: {(texture2 != null ? texture2.name : "null")}");
            // Debug.Log($"useTexture1Pattern length: {(useTexture1Pattern != null ? $"{useTexture1Pattern.GetLength(0)}x{useTexture1Pattern.GetLength(1)}x{useTexture1Pattern.GetLength(2)}" : "null")}");
        }

        // ボクセルのすべての面テクスチャ情報を取得
        List<VoxelFaceTextureInfo> faceInfos = GetAllVoxelFaceTextureInfo(voxelX, voxelY, voxelZ);
        
        if (enableTextureExtraction)
        {
            // Debug.Log($"Found {faceInfos.Count} face texture infos");
            // foreach (var faceInfo in faceInfos)
            // {
            //     Debug.Log($"Face: {faceInfo.faceNormal}, Texture: {(faceInfo.sourceTexture != null ? faceInfo.sourceTexture.name : "null")}, Exposed: {faceInfo.isExposed}");
            // }
        }
        
        // 現在は代表面（上面優先）のテクスチャを使用
        VoxelFaceTextureInfo representativeFace = GetRepresentativeFace(faceInfos);
        
        if (representativeFace.sourceTexture != null)
        {
            // ブロックテクスチャから該当部分を切り出して新しいテクスチャを作成
            Texture2D extractedTexture = ExtractVoxelTextureRegion(representativeFace);
            
            if (extractedTexture != null)
            {
                // URPマテリアルを作成
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.mainTexture = extractedTexture;
                itemRenderer.material = material;
                
                if (enableTextureExtraction)
                {
                    // Debug.Log($"Successfully applied extracted texture ({extractedTexture.width}x{extractedTexture.height})");
                }
            }
            else
            {
                // テクスチャ抽出に失敗した場合はデフォルトマテリアル
                ApplyDefaultMaterial(itemRenderer);
                Debug.LogWarning("Failed to extract texture, using default material");
            }
        }
        else
        {
            // テクスチャが見つからない場合はデフォルトマテリアル
            ApplyDefaultMaterial(itemRenderer);
            Debug.LogWarning($"No texture found for voxel at ({voxelX}, {voxelY}, {voxelZ})");
        }
    }

    /// <summary>
    /// ブロックテクスチャから指定された領域を切り出す
    /// </summary>
    private Texture2D ExtractVoxelTextureRegion(VoxelFaceTextureInfo faceInfo)
    {
        if (faceInfo.sourceTexture == null) return null;

        try
        {
            // テクスチャが読み取り可能かチェック
            if (!faceInfo.sourceTexture.isReadable)
            {
                Debug.LogWarning($"Texture '{faceInfo.sourceTexture.name}' is not readable. Enable 'Read/Write Enabled' in texture import settings.");
                return null;
            }

            // 元テクスチャのサイズ
            int sourceWidth = faceInfo.sourceTexture.width;
            int sourceHeight = faceInfo.sourceTexture.height;
            
            // 切り出し領域の計算（ピクセル単位）
            int startX = Mathf.FloorToInt(faceInfo.uvBase.x * sourceWidth);
            int startY = Mathf.FloorToInt(faceInfo.uvBase.y * sourceHeight);
            int regionWidth = Mathf.FloorToInt(faceInfo.uvSize.x * sourceWidth);
            int regionHeight = Mathf.FloorToInt(faceInfo.uvSize.y * sourceHeight);
            
            // 境界チェック
            startX = Mathf.Clamp(startX, 0, sourceWidth - 1);
            startY = Mathf.Clamp(startY, 0, sourceHeight - 1);
            regionWidth = Mathf.Clamp(regionWidth, 1, sourceWidth - startX);
            regionHeight = Mathf.Clamp(regionHeight, 1, sourceHeight - startY);
            
            // サイズが小さすぎる場合は最小サイズに調整
            regionWidth = Mathf.Max(regionWidth, 1);
            regionHeight = Mathf.Max(regionHeight, 1);
            
            // 新しいテクスチャを作成
            Texture2D extractedTexture = new Texture2D(regionWidth, regionHeight, TextureFormat.RGBA32, false);
            
            // 元テクスチャから該当部分のピクセルを取得
            Color[] sourcePixels = faceInfo.sourceTexture.GetPixels(startX, startY, regionWidth, regionHeight);
            
            // テクスチャを180度回転させる
            Color[] rotatedPixels = RotateTexture180(sourcePixels, regionWidth, regionHeight);
            
            // 新しいテクスチャにピクセルを設定
            extractedTexture.SetPixels(rotatedPixels);
            extractedTexture.Apply();
            
            return extractedTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to extract voxel texture: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// テクスチャのピクセル配列を180度回転させる
    /// </summary>
    private Color[] RotateTexture180(Color[] pixels, int width, int height)
    {
        Color[] rotatedPixels = new Color[pixels.Length];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 元の位置
                int sourceIndex = y * width + x;
                
                // 180度回転後の位置（右下から左上へ）
                int targetX = width - 1 - x;
                int targetY = height - 1 - y;
                int targetIndex = targetY * width + targetX;
                
                rotatedPixels[targetIndex] = pixels[sourceIndex];
            }
        }
        
        return rotatedPixels;
    }

    /// <summary>
    /// デフォルトマテリアルを適用
    /// </summary>
    private void ApplyDefaultMaterial(Renderer renderer)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = Color.gray;
        renderer.material = material;
    }

    /// <summary>
    /// 面情報リストから代表的な面を選択
    /// </summary>
    private VoxelFaceTextureInfo GetRepresentativeFace(List<VoxelFaceTextureInfo> faceInfos)
    {
        // 優先順位: Z軸正方向(前面) > 上面 > 右面 > 左面 > 後面 > 下面
        Vector3[] priorityOrder = {
            Vector3.forward, Vector3.up, Vector3.right,
            Vector3.left, Vector3.back, Vector3.down
        };

        foreach (Vector3 priorityNormal in priorityOrder)
        {
            var face = faceInfos.Find(f => f.faceNormal == priorityNormal && f.isExposed);
            if (face.sourceTexture != null)
            {
                return face;
            }
        }

        // 見つからない場合は最初の面を返す
        return faceInfos.Count > 0 ? faceInfos[0] : new VoxelFaceTextureInfo();
    }

    private void DropItem(Vector3 position) // エラー解決: メソッド追加（Block.csから移行）
    {
        // 座標情報が不明な場合は従来の処理を実行
        DropItem(position, -1, -1, -1);
    }

    private void DropItem(Vector3 position, int voxelX, int voxelY, int voxelZ) // ボクセル座標を受け取る新しいオーバーロード
    {
        if (DroppedItemManager.Instance == null)
        {
            Debug.LogError("DroppedItemManager.Instance is null. Please ensure a DroppedItemManager exists in the scene.");
            return;
        }
        if (blockData == null)
        {
            Debug.LogError($"Block at {blockPosition} has no BlockData assigned. Check TerrainManager setup.", this.gameObject);
            return;
        }
        if (blockData.droppedItemPrefab == null)
        {
            Debug.LogError($"BlockData '{blockData.name}' has no droppedItemPrefab assigned.", blockData);
            return;
        }

        // DroppedItemManagerから正しいPrefabのアイテムを取得
        GameObject item = DroppedItemManager.Instance.GetItem(blockData.droppedItemPrefab);
        if (item == null) return;

        item.transform.position = position;
        item.transform.rotation = Quaternion.identity;

        // 自動スケール調整が有効な場合
        if (blockData.autoScale)
        {
            float targetScale = voxelSize * blockData.scaleMultiplier;
            item.transform.localScale = Vector3.one * targetScale;
        }

        // ボクセル座標が有効な場合、テクスチャ抽出を実行
        if (enableTextureExtraction && voxelX >= 0 && voxelY >= 0 && voxelZ >= 0 && 
            voxelX < ChunkSize && voxelY < ChunkSize && voxelZ < ChunkSize)
        {
            ApplyVoxelTextureToDroppedItem(item, voxelX, voxelY, voxelZ);
        }
        else
        {
            // テクスチャ抽出が無効または座標が無効な場合、デフォルトマテリアルを適用
            var itemRenderer = item.GetComponent<Renderer>();
            if (itemRenderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = Color.gray; // 色を灰色に設定
                itemRenderer.material = material;
            }
        }

        // Rigidbodyが無い場合は追加
        Rigidbody itemRigidbody = item.GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = item.AddComponent<Rigidbody>();
        }

        // 移動モードに応じてRigidbodyのConstraintを設定
        SetDroppedItemConstraints(itemRigidbody);

        // DroppedItemコンポーネントの処理
        DroppedItem droppedItemComponent = item.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            droppedItemComponent.enabled = !blockData.disableRotation;
        }

        // タグが設定されていない場合は設定
        if (!item.CompareTag("DroppedItem"))
        {
            item.tag = "DroppedItem";
        }
    }

    /// <summary>
    /// ドロップアイテムのRigidbodyに移動モードに応じた制約を設定
    /// </summary>
    private void SetDroppedItemConstraints(Rigidbody itemRigidbody)
    {
        if (itemRigidbody == null) return;

        // 現在の移動モードを取得
        PlayerController.MoveMode currentMoveMode = GetCurrentMoveMode();

        // 移動モードに応じて制約を設定
        switch (currentMoveMode)
        {
            case PlayerController.MoveMode.SideScroller:
                // SideScrollerモード: XY平面のみ移動、Z軸は固定
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
                break;

            case PlayerController.MoveMode.TopDown:
                // TopDownモード: XZ平面のみ移動、Y軸は固定
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                break;

            default:
                // デフォルトは制約なし
                itemRigidbody.constraints = RigidbodyConstraints.None;
                break;
        }
    }

    /// <summary>
    /// 現在のゲームの移動モードを取得
    /// </summary>
    private PlayerController.MoveMode GetCurrentMoveMode()
    {
        // プレイヤーオブジェクトを探してPlayerControllerから移動モードを取得
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            return playerController.currentMoveMode;
        }

        // プレイヤーが見つからない場合はデフォルトでTopDownモードを返す
        return PlayerController.MoveMode.TopDown;
    }

}
