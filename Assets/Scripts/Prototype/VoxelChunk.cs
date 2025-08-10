using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class VoxelChunk : MonoBehaviour
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
        ChunkSize = newChunkSize;
        voxelSize = worldChunkSize / ChunkSize;
        maxHP = hp;

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
            DropItem(transform.position + localPos); // エラー解決: メソッド呼び出し
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
                            DropItem(voxelWorldPos);
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

    private void DropItem(Vector3 position) // エラー解決: メソッド追加（Block.csから移行）
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.transform.position = position;
        item.transform.localScale = Vector3.one * voxelSize;
        item.AddComponent<Rigidbody>();
        item.AddComponent<DroppedItem>();
        item.tag = "DroppedItem";

        // URP用のマテリアルを動的に作成して割り当てる
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer != null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.gray; // 色を灰色に設定
            itemRenderer.material = material;
        }
    }

}
