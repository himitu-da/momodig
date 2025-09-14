using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロックのメッシュ生成を担当するクラス
/// Block.csから分離してメッシュ生成の責務を専門化
/// </summary>
public class BlockMeshGenerator
{
    /// <summary>
    /// ブロックのメッシュを生成
    /// </summary>
    public void GenerateMesh(Block block, VoxelManager voxelManager, Vector3Int blockPosition, int voxelsPerBlock, int maxHP, Color initialColor, Mesh mesh, MeshCollider collider)
    {
        mesh.Clear();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();

        // VoxelManagerからこのブロックのボクセルリストを取得
        var voxelsInBlock = voxelManager.GetVoxelsInBlock(blockPosition);

        foreach (var voxelData in voxelsInBlock)
        {
            if (!voxelData.isActive) continue;

            int x = voxelData.localPosition.x;
            int y = voxelData.localPosition.y;
            int z = voxelData.localPosition.z;

            float healthPercentage = (float)voxelData.health / maxHP;
            Color healthColor = Color.Lerp(Color.black, initialColor, healthPercentage);
            healthColor.a = healthPercentage; // ドット透過

            Vector3 pos = new Vector3(x - voxelsPerBlock / 2.0f + 0.5f, y - voxelsPerBlock / 2.0f + 0.5f, z - voxelsPerBlock / 2.0f + 0.5f);

            // 6面追加（露出チェック）
            if (IsVoxelFaceExposed(voxelManager, blockPosition, x + 1, y, z, voxelsPerBlock))
                AddFace(pos, Vector3.right, vertices, triangles, uvs, colors, healthColor, false, x, y, z, voxelsPerBlock);
            if (IsVoxelFaceExposed(voxelManager, blockPosition, x - 1, y, z, voxelsPerBlock))
                AddFace(pos, Vector3.left, vertices, triangles, uvs, colors, healthColor, false, x, y, z, voxelsPerBlock);
            if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y + 1, z, voxelsPerBlock))
                AddFace(pos, Vector3.up, vertices, triangles, uvs, colors, healthColor, false, x, y, z, voxelsPerBlock);
            if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y - 1, z, voxelsPerBlock))
                AddFace(pos, Vector3.down, vertices, triangles, uvs, colors, healthColor, false, x, y, z, voxelsPerBlock);
            if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y, z + 1, voxelsPerBlock))
                AddFace(pos, Vector3.forward, vertices, triangles, uvs, colors, healthColor, true, x, y, z, voxelsPerBlock);
            if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y, z - 1, voxelsPerBlock))
                AddFace(pos, Vector3.back, vertices, triangles, uvs, colors, healthColor, true, x, y, z, voxelsPerBlock);
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();

        if (vertices.Count == 0)
        {
            // メッシュが空の場合は親のBlockに破壊を通知
            Object.Destroy(block.gameObject);
            return;
        }

        collider.sharedMesh = mesh;
    }

    /// <summary>
    /// 指定されたローカル座標のボクセルの面が露出しているかチェック
    /// </summary>
    private bool IsVoxelFaceExposed(VoxelManager voxelManager, Vector3Int blockPosition, int x, int y, int z, int voxelsPerBlock)
    {
        // 座標がブロックの範囲外なら、その面は露出している
        if (x < 0 || x >= voxelsPerBlock || y < 0 || y >= voxelsPerBlock || z < 0 || z >= voxelsPerBlock)
        {
            return true;
        }

        // VoxelManagerに問い合わせて、隣接ボクセルが存在しないか非アクティブなら露出している
        var neighborVoxel = voxelManager.GetVoxelAt(blockPosition, new Vector3Int(x, y, z));
        return neighborVoxel == null || !neighborVoxel.isActive;
    }

    /// <summary>
    /// ボクセル面を追加
    /// </summary>
    private void AddFace(Vector3 pos, Vector3 normal, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> colors, Color faceColor, bool reverse, int voxelX, int voxelY, int voxelZ, int voxelsPerBlock)
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
        float pixelSize = 1.0f / voxelsPerBlock;
        float u_base = 0;
        float v_base = 0;

        if (normal == Vector3.right || normal == Vector3.left) // X面
        {
            u_base = (float)voxelZ / voxelsPerBlock;
            v_base = (float)voxelY / voxelsPerBlock;
        }
        else if (normal == Vector3.up || normal == Vector3.down) // Y面
        {
            u_base = (float)voxelX / voxelsPerBlock;
            v_base = (float)voxelZ / voxelsPerBlock;
        }
        else if (normal == Vector3.forward || normal == Vector3.back) // Z面
        {
            u_base = (float)voxelX / voxelsPerBlock;
            v_base = (float)voxelY / voxelsPerBlock;
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

    /// <summary>
    /// 指定された面の法線と頂点インデックスに基づいて頂点オフセットを取得
    /// </summary>
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
}
