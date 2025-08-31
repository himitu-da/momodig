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
    public int ChunkSize { get; private set; } = 4; // 16ドット単位の塊　この変数いらない説
    private byte[,,] voxelTypes; // 0: 空気, 1: 固体
    private int[,,] voxelHPs; // HP
    private int maxHP = 3;
    [Range(0.01f, 1.0f)]
    public float diggingThreshold = 0.1f; // 掘削判定の閾値（ボクセルとの重複率）
    private Color initialColor = Color.white;
    [SerializeField] private Texture2D texture1, texture2;
    private bool[,,] useTexture1Pattern; // テクスチャパターン
    private float voxelSize; // BaseCubePlacerから受け取る
    
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
    }

    public void Initialize(bool[,,] pattern, int newChunkSize, float worldChunkSize, int hp)
    {
        Initialize(pattern, newChunkSize, worldChunkSize, hp, null, true, true, 0.8f);
    }

    public void Initialize(bool[,,] pattern, int newChunkSize, float worldChunkSize, int hp, GameObject itemPrefab, bool disableItemRotation)
    {
        Initialize(pattern, newChunkSize, worldChunkSize, hp, itemPrefab, disableItemRotation, true, 0.8f);
    }

    public void Initialize(bool[,,] pattern, int newChunkSize, float worldChunkSize, int hp, GameObject itemPrefab, bool disableItemRotation, bool autoScaleItems, float itemScaleMultiplier)
    {
        Initialize(pattern, newChunkSize, worldChunkSize, hp, itemPrefab, disableItemRotation, autoScaleItems, itemScaleMultiplier, null, null);
    }

    /// <summary>
    /// テクスチャを含む完全な初期化
    /// </summary>
    public void Initialize(bool[,,] pattern, int newChunkSize, float worldChunkSize, int hp, GameObject itemPrefab, bool disableItemRotation, bool autoScaleItems, float itemScaleMultiplier, Texture2D tex1, Texture2D tex2)
    {
        ChunkSize = newChunkSize;
        voxelSize = worldChunkSize / ChunkSize;
        maxHP = hp;
        droppedItemPrefab = itemPrefab;
        disableRotation = disableItemRotation;
        autoScale = autoScaleItems;
        scaleMultiplier = itemScaleMultiplier;
        
        // テクスチャを設定
        texture1 = tex1;
        texture2 = tex2;

        // 配列を初期化
        voxelTypes = new byte[ChunkSize, ChunkSize, ChunkSize];
        voxelHPs = new int[ChunkSize, ChunkSize, ChunkSize];
        useTexture1Pattern = pattern ?? new bool[ChunkSize, ChunkSize, ChunkSize];

        for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                {
                    // patternに基づいてボクセルの種類を決定
                    if (useTexture1Pattern[x, y, z])
                    {
                        voxelTypes[x, y, z] = 1; // 固体
                        voxelHPs[x, y, z] = maxHP;
                    }
                    else
                    {
                        voxelTypes[x, y, z] = 0; // 空気
                        voxelHPs[x, y, z] = 0;
                    }
                }
        GenerateMesh();
    }

    public void TakeDamage(Vector3 localPos, int damage)
    {
        int x = Mathf.FloorToInt(localPos.x + ChunkSize / 2.0f);
        int y = Mathf.FloorToInt(localPos.y + ChunkSize / 2.0f);
        int z = Mathf.FloorToInt(localPos.z + ChunkSize / 2.0f);
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize || z < 0 || z >= ChunkSize || voxelTypes[x, y, z] == 0) return;

        voxelHPs[x, y, z] -= damage;
        if (voxelHPs[x, y, z] <= 0)
        {
            voxelTypes[x, y, z] = 0;
            DropItem(transform.position + localPos, x, y, z); // ボクセル座標も渡す
        }
        GenerateMesh(); // 破壊後更新
    }

    public void DigVoxels(BoxCollider diggingArea)
    {
        bool needsMeshUpdate = false;
        const int sampleResolution = 3; // 各軸のサンプル解像度
        const int totalSamples = sampleResolution * sampleResolution * sampleResolution;

        // diggingAreaの判定用情報を事前に計算
        Matrix4x4 worldToLocalMatrix = diggingArea.transform.worldToLocalMatrix;
        Vector3 halfSize = diggingArea.size * 0.5f;
        Vector3 center = diggingArea.center;

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (voxelTypes[x, y, z] == 0) continue;

                    int containedSamples = 0;
                    // ボクセルのローカル座標でのバウンディングボックスの最小点を計算
                    Vector3 voxelMin = new Vector3(x - ChunkSize / 2.0f, y - ChunkSize / 2.0f, z - ChunkSize / 2.0f);

                    // ボクセル内をサンプリングして、diggingAreaとの重複をチェック
                    for (int sx = 0; sx < sampleResolution; sx++)
                    {
                        for (int sy = 0; sy < sampleResolution; sy++)
                        {
                            for (int sz = 0; sz < sampleResolution; sz++)
                            {
                                // サンプル点のチャンク内ローカル座標を計算 (ボクセルサイズは1x1x1と仮定)
                                float sampleX = voxelMin.x + (sx + 0.5f) / sampleResolution;
                                float sampleY = voxelMin.y + (sy + 0.5f) / sampleResolution;
                                float sampleZ = voxelMin.z + (sz + 0.5f) / sampleResolution;
                                Vector3 sampleLocalPos = new Vector3(sampleX, sampleY, sampleZ);

                                // ワールド座標に変換
                                Vector3 sampleWorldPos = transform.TransformPoint(sampleLocalPos);

                                // diggingAreaのローカル座標に変換
                                Vector3 localPosInDiggingArea = worldToLocalMatrix.MultiplyPoint3x4(sampleWorldPos);

                                // diggingAreaの中心を考慮して、AABBの内外判定
                                if (Mathf.Abs(localPosInDiggingArea.x - center.x) <= halfSize.x &&
                                    Mathf.Abs(localPosInDiggingArea.y - center.y) <= halfSize.y &&
                                    Mathf.Abs(localPosInDiggingArea.z - center.z) <= halfSize.z)
                                {
                                    containedSamples++;
                                }
                            }
                        }
                    }

                    // 重複率が閾値を超えていればダメージを与える
                    float overlapRatio = (float)containedSamples / totalSamples;
                    if (overlapRatio >= diggingThreshold)
                    {
                        voxelHPs[x, y, z]--;
                        needsMeshUpdate = true;

                        if (voxelHPs[x, y, z] <= 0)
                        {
                            voxelTypes[x, y, z] = 0;
                            Vector3 voxelCenterPos = new Vector3(x - ChunkSize / 2.0f + 0.5f, y - ChunkSize / 2.0f + 0.5f, z - ChunkSize / 2.0f + 0.5f);
                            Vector3 voxelWorldPos = transform.TransformPoint(voxelCenterPos);
                            DropItem(voxelWorldPos, x, y, z); // ボクセル座標も渡す
                        }
                    }
                }
            }
        }

        if (needsMeshUpdate)
        {
            GenerateMesh();
        }
    }

    private void GenerateMesh()
    {
        mesh.Clear();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();

        // 各ボクセルをループ
        for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (voxelTypes[x, y, z] == 0) continue;

                    float healthPercentage = (float)voxelHPs[x, y, z] / maxHP;
                    Color healthColor = Color.Lerp(Color.black, initialColor, healthPercentage);
                    healthColor.a = healthPercentage; // ドット透過

                    Vector3 pos = new Vector3(x - ChunkSize / 2.0f + 0.5f, y - ChunkSize / 2.0f + 0.5f, z - ChunkSize / 2.0f + 0.5f);

                    // 6面追加（露出チェック）
                    // X, Y方向はそのまま、Z方向の面のみ巻き順を反転させる
                    if (x == ChunkSize - 1 || voxelTypes[x + 1, y, z] == 0)
                        AddFace(pos, Vector3.right, vertices, triangles, uvs, colors, healthColor, false, x, y, z);
                    if (x == 0 || voxelTypes[x - 1, y, z] == 0)
                        AddFace(pos, Vector3.left, vertices, triangles, uvs, colors, healthColor, false, x, y, z);
                    if (y == ChunkSize - 1 || voxelTypes[x, y + 1, z] == 0)
                        AddFace(pos, Vector3.up, vertices, triangles, uvs, colors, healthColor, false, x, y, z);
                    if (y == 0 || voxelTypes[x, y - 1, z] == 0)
                        AddFace(pos, Vector3.down, vertices, triangles, uvs, colors, healthColor, false, x, y, z);
                    if (z == ChunkSize - 1 || voxelTypes[x, y, z + 1] == 0)
                        AddFace(pos, Vector3.forward, vertices, triangles, uvs, colors, healthColor, true, x, y, z);
                    if (z == 0 || voxelTypes[x, y, z - 1] == 0)
                        AddFace(pos, Vector3.back, vertices, triangles, uvs, colors, healthColor, true, x, y, z);
                }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();

        if (vertices.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        collider.sharedMesh = mesh;
    }

    private void AddFace(Vector3 pos, Vector3 normal, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> colors, Color faceColor, bool reverse, int voxelX, int voxelY, int voxelZ)
    {
        int vertCount = verts.Count;

        // 頂点追加（ボクセル中心posから-0.5~0.5オフセット）
        verts.Add(pos + GetVertexOffset(normal, 0));
        verts.Add(pos + GetVertexOffset(normal, 1));
        verts.Add(pos + GetVertexOffset(normal, 2));
        verts.Add(pos + GetVertexOffset(normal, 3));

        // 三角形（reverseで巻き方向を逆転し、面の向きを統一）
        if (reverse)
        {
            tris.Add(vertCount + 0);
            tris.Add(vertCount + 3);
            tris.Add(vertCount + 2);
            tris.Add(vertCount + 0);
            tris.Add(vertCount + 2);
            tris.Add(vertCount + 1);
        }
        else
        {
            tris.Add(vertCount + 0);
            tris.Add(vertCount + 1);
            tris.Add(vertCount + 2);
            tris.Add(vertCount + 0);
            tris.Add(vertCount + 2);
            tris.Add(vertCount + 3);
        }

        // UV（テクスチャ用）
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

        if (normal == Vector3.left || normal == Vector3.back)
        {
            // テクスチャが反転しないようにUVの順序を調整
            uvs.Add(new Vector2(u_base + pixelSize, v_base));
            uvs.Add(new Vector2(u_base + pixelSize, v_base + pixelSize));
            uvs.Add(new Vector2(u_base, v_base + pixelSize));
            uvs.Add(new Vector2(u_base, v_base));
        }
        else if (normal == Vector3.down)
        {
            // 下面のテクスチャ順序を調整
            uvs.Add(new Vector2(u_base, v_base + pixelSize));
            uvs.Add(new Vector2(u_base, v_base));
            uvs.Add(new Vector2(u_base + pixelSize, v_base));
            uvs.Add(new Vector2(u_base + pixelSize, v_base + pixelSize));
        }
        else // right, up, forward
        {
            uvs.Add(new Vector2(u_base, v_base));
            uvs.Add(new Vector2(u_base, v_base + pixelSize));
            uvs.Add(new Vector2(u_base + pixelSize, v_base + pixelSize));
            uvs.Add(new Vector2(u_base + pixelSize, v_base));
        }

        // 色
        colors.Add(faceColor);
        colors.Add(faceColor);
        colors.Add(faceColor);
        colors.Add(faceColor);
    }

    private Vector3 GetVertexOffset(Vector3 normal, int index)
    {
        // 各normalの4頂点（時計回り、見た目上正面向き）
        if (normal == Vector3.right) // +X
        {
            switch (index)
            {
                case 0: return new Vector3(0.5f, -0.5f, -0.5f);
                case 1: return new Vector3(0.5f, 0.5f, -0.5f);
                case 2: return new Vector3(0.5f, 0.5f, 0.5f);
                case 3: return new Vector3(0.5f, -0.5f, 0.5f);
            }
        }
        else if (normal == Vector3.left) // -X
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, -0.5f, 0.5f);
                case 1: return new Vector3(-0.5f, 0.5f, 0.5f);
                case 2: return new Vector3(-0.5f, 0.5f, -0.5f);
                case 3: return new Vector3(-0.5f, -0.5f, -0.5f);
            }
        }
        else if (normal == Vector3.up) // +Y
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, 0.5f, -0.5f);
                case 1: return new Vector3(-0.5f, 0.5f, 0.5f);
                case 2: return new Vector3(0.5f, 0.5f, 0.5f);
                case 3: return new Vector3(0.5f, 0.5f, -0.5f);
            }
        }
        else if (normal == Vector3.down) // -Y
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, -0.5f, 0.5f);
                case 1: return new Vector3(-0.5f, -0.5f, -0.5f);
                case 2: return new Vector3(0.5f, -0.5f, -0.5f);
                case 3: return new Vector3(0.5f, -0.5f, 0.5f);
            }
        }
        else if (normal == Vector3.forward) // +Z
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, -0.5f, 0.5f);
                case 1: return new Vector3(-0.5f, 0.5f, 0.5f);
                case 2: return new Vector3(0.5f, 0.5f, 0.5f);
                case 3: return new Vector3(0.5f, -0.5f, 0.5f);
            }
        }
        else if (normal == Vector3.back) // -Z
        {
            switch (index)
            {
                case 0: return new Vector3(0.5f, -0.5f, -0.5f);
                case 1: return new Vector3(0.5f, 0.5f, -0.5f);
                case 2: return new Vector3(-0.5f, 0.5f, -0.5f);
                case 3: return new Vector3(-0.5f, -0.5f, -0.5f);
            }
        }
        return Vector3.zero;
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
        GameObject item;
        
        // Prefabが指定されている場合はそれを使用、されていない場合はデフォルトのCubeを作成
        if (droppedItemPrefab != null)
        {
            item = Instantiate(droppedItemPrefab, position, Quaternion.identity);
            
            // 自動スケール調整が有効な場合
            if (autoScale)
            {
                float targetScale = voxelSize * scaleMultiplier;
                item.transform.localScale = Vector3.one * targetScale;
            }
        }
        else
        {
            // デフォルト処理：Cubeを作成
            item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.transform.position = position;
            item.transform.localScale = Vector3.one * voxelSize;
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
        if (droppedItemComponent == null)
        {
            droppedItemComponent = item.AddComponent<DroppedItem>();
        }

        // 回転を無効化する場合の処理
        if (disableRotation)
        {
            droppedItemComponent.enabled = false; // DroppedItemコンポーネントを無効化して回転を停止
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
