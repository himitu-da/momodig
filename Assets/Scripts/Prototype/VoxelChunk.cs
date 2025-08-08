using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class VoxelChunk : MonoBehaviour
{
    public const int ChunkSize = 4; // 16ドット単位の塊
    private byte[,,] voxelTypes; // 0: 空気, 1: 固体
    private int[,,] voxelHPs; // HP
    private int maxHP = 3;
    private Color initialColor = Color.white;
    [SerializeField] private Texture2D texture1, texture2;
    private bool[,,] useTexture1Pattern; // テクスチャパターン
    private float voxelSize; // BaseCubePlacerから受け取る

    private Mesh mesh;
    private MeshFilter meshFilter;
    private new MeshCollider collider; // newキーワード追加で警告解決

    void Awake()
    {
        voxelTypes = new byte[ChunkSize, ChunkSize, ChunkSize];
        voxelHPs = new int[ChunkSize, ChunkSize, ChunkSize];
        useTexture1Pattern = new bool[ChunkSize, ChunkSize, ChunkSize];

        meshFilter = GetComponent<MeshFilter>();
        collider = GetComponent<MeshCollider>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;
    }

    public void Initialize(bool[,,] pattern, float newVoxelSize, int hp)
    {
        useTexture1Pattern = pattern ?? new bool[ChunkSize, ChunkSize, ChunkSize];
        voxelSize = newVoxelSize;
        maxHP = hp;
        for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkSize; y++)
                for (int z = 0; z < ChunkSize; z++)
                {
                    voxelTypes[x, y, z] = 1; // 初期固体
                    voxelHPs[x, y, z] = maxHP;
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
        // Bounds worldBounds = diggingArea.bounds; // AABBではなく、回転を考慮した判定が必要

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (voxelTypes[x, y, z] == 0) continue;

                    // ボクセルのワールド座標を計算
                    Vector3 voxelCenterPos = new Vector3(x - ChunkSize / 2.0f + 0.5f, y - ChunkSize / 2.0f + 0.5f, z - ChunkSize / 2.0f + 0.5f);
                    Vector3 voxelWorldPos = transform.TransformPoint(voxelCenterPos);

                    // ボクセルのワールド座標をdiggingAreaのローカル座標に変換
                    Vector3 localPosInDiggingArea = diggingArea.transform.InverseTransformPoint(voxelWorldPos);

                    // diggingAreaのローカル座標系でバウンダリチェック
                    Bounds localBounds = new Bounds(diggingArea.center, diggingArea.size);
                    if (localBounds.Contains(localPosInDiggingArea))
                    {
                        voxelHPs[x, y, z]--;
                        needsMeshUpdate = true;

                        if (voxelHPs[x, y, z] <= 0)
                        {
                            voxelTypes[x, y, z] = 0;
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
