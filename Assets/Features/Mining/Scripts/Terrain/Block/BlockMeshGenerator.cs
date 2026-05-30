using UnityEngine;
using System.Collections.Generic;

public class BlockMeshGenerator
{
    public struct GenerationResult
    {
        public List<BlockData> submeshBlockData;
        public Dictionary<Vector3Int, List<int>> vertexIndicesByLocalCell;
        public bool hasGeometry;
    }

    public GenerationResult GenerateMesh(Block block, VoxelManager voxelManager, Vector3Int blockPosition, int voxelsPerBlock, Color initialColor, Mesh mesh, MeshCollider collider)
    {
        mesh.Clear();

        var voxelsInBlock = voxelManager.GetVoxelsInBlock(blockPosition);

        var voxelsByBlockData = new Dictionary<BlockData, List<Voxel>>();
        foreach (var voxel in voxelsInBlock)
        {
            if (!voxel.isActive || voxel.blockData == null) continue;
            if (!voxelsByBlockData.TryGetValue(voxel.blockData, out var list))
            {
                list = new List<Voxel>();
                voxelsByBlockData[voxel.blockData] = list;
            }
            list.Add(voxel);
        }

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var submeshTriangles = new List<List<int>>();
        var submeshBlockData = new List<BlockData>();
        var vertexIndicesByLocalCell = new Dictionary<Vector3Int, List<int>>();

        foreach (var pair in voxelsByBlockData)
        {
            var triangles = new List<int>();

            foreach (var voxelData in pair.Value)
            {
                int x = voxelData.localPosition.x;
                int y = voxelData.localPosition.y;
                int z = voxelData.localPosition.z;

                int voxelMaxHealth = Mathf.Max(1, voxelData.maxHealth);
                float healthPercentage = Mathf.Clamp01((float)voxelData.health / voxelMaxHealth);
                Color healthColor = Color.Lerp(Color.black, initialColor, healthPercentage);
                healthColor.a = 1f;

                Vector3 pos = new Vector3(x - voxelsPerBlock / 2.0f + 0.5f, y - voxelsPerBlock / 2.0f + 0.5f, z - voxelsPerBlock / 2.0f + 0.5f);
                Vector3Int localCell = new Vector3Int(x, y, z);

                if (IsVoxelFaceExposed(voxelManager, blockPosition, x + 1, y, z, voxelsPerBlock))
                    AddFace(pos, Vector3.right, vertices, triangles, uvs, colors, vertexIndicesByLocalCell, localCell, healthColor, false, x, y, z, voxelsPerBlock);
                if (IsVoxelFaceExposed(voxelManager, blockPosition, x - 1, y, z, voxelsPerBlock))
                    AddFace(pos, Vector3.left, vertices, triangles, uvs, colors, vertexIndicesByLocalCell, localCell, healthColor, false, x, y, z, voxelsPerBlock);
                if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y + 1, z, voxelsPerBlock))
                    AddFace(pos, Vector3.up, vertices, triangles, uvs, colors, vertexIndicesByLocalCell, localCell, healthColor, false, x, y, z, voxelsPerBlock);
                if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y - 1, z, voxelsPerBlock))
                    AddFace(pos, Vector3.down, vertices, triangles, uvs, colors, vertexIndicesByLocalCell, localCell, healthColor, false, x, y, z, voxelsPerBlock);
                if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y, z + 1, voxelsPerBlock))
                    AddFace(pos, Vector3.forward, vertices, triangles, uvs, colors, vertexIndicesByLocalCell, localCell, healthColor, true, x, y, z, voxelsPerBlock);
                if (IsVoxelFaceExposed(voxelManager, blockPosition, x, y, z - 1, voxelsPerBlock))
                    AddFace(pos, Vector3.back, vertices, triangles, uvs, colors, vertexIndicesByLocalCell, localCell, healthColor, true, x, y, z, voxelsPerBlock);
            }

            if (triangles.Count > 0)
            {
                submeshTriangles.Add(triangles);
                submeshBlockData.Add(pair.Key);
            }
        }

        if (vertices.Count == 0)
        {
            mesh.subMeshCount = 0;
            if (collider != null)
            {
                collider.sharedMesh = null;
            }
            block.gameObject.SetActive(false);
            return new GenerationResult { submeshBlockData = submeshBlockData, vertexIndicesByLocalCell = vertexIndicesByLocalCell, hasGeometry = false };
        }

        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.subMeshCount = submeshTriangles.Count;
        for (int i = 0; i < submeshTriangles.Count; i++)
        {
            mesh.SetTriangles(submeshTriangles[i], i);
        }
        mesh.RecalculateNormals();

        if (!block.gameObject.activeSelf)
        {
            block.gameObject.SetActive(true);
        }

        collider.sharedMesh = null;
        collider.sharedMesh = mesh;

        return new GenerationResult { submeshBlockData = submeshBlockData, vertexIndicesByLocalCell = vertexIndicesByLocalCell, hasGeometry = true };
    }

    private bool IsVoxelFaceExposed(VoxelManager voxelManager, Vector3Int blockPosition, int x, int y, int z, int voxelsPerBlock)
    {
        if (x < 0 || x >= voxelsPerBlock || y < 0 || y >= voxelsPerBlock || z < 0 || z >= voxelsPerBlock)
        {
            return true;
        }

        var neighborVoxel = voxelManager.GetVoxelAt(blockPosition, new Vector3Int(x, y, z));
        return neighborVoxel == null || !neighborVoxel.isActive;
    }

    private void AddFace(
        Vector3 pos,
        Vector3 normal,
        List<Vector3> verts,
        List<int> tris,
        List<Vector2> uvs,
        List<Color> colors,
        Dictionary<Vector3Int, List<int>> vertexIndicesByLocalCell,
        Vector3Int localCell,
        Color faceColor,
        bool reverse,
        int voxelX,
        int voxelY,
        int voxelZ,
        int voxelsPerBlock)
    {
        int vertCount = verts.Count;

        verts.Add(pos + GetVertexOffset(normal, 0));
        verts.Add(pos + GetVertexOffset(normal, 1));
        verts.Add(pos + GetVertexOffset(normal, 2));
        verts.Add(pos + GetVertexOffset(normal, 3));

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

        float pixelSize = 1.0f / voxelsPerBlock;
        float u_base = 0;
        float v_base = 0;

        if (normal == Vector3.right || normal == Vector3.left)
        {
            u_base = (float)voxelZ / voxelsPerBlock;
            v_base = (float)voxelY / voxelsPerBlock;
        }
        else if (normal == Vector3.up || normal == Vector3.down)
        {
            u_base = (float)voxelX / voxelsPerBlock;
            v_base = (float)voxelZ / voxelsPerBlock;
        }
        else if (normal == Vector3.forward || normal == Vector3.back)
        {
            u_base = (float)voxelX / voxelsPerBlock;
            v_base = (float)voxelY / voxelsPerBlock;
        }

        if (normal == Vector3.left || normal == Vector3.back)
        {
            uvs.Add(new Vector2(u_base + pixelSize, v_base));
            uvs.Add(new Vector2(u_base + pixelSize, v_base + pixelSize));
            uvs.Add(new Vector2(u_base, v_base + pixelSize));
            uvs.Add(new Vector2(u_base, v_base));
        }
        else if (normal == Vector3.down)
        {
            uvs.Add(new Vector2(u_base, v_base + pixelSize));
            uvs.Add(new Vector2(u_base, v_base));
            uvs.Add(new Vector2(u_base + pixelSize, v_base));
            uvs.Add(new Vector2(u_base + pixelSize, v_base + pixelSize));
        }
        else
        {
            uvs.Add(new Vector2(u_base, v_base));
            uvs.Add(new Vector2(u_base, v_base + pixelSize));
            uvs.Add(new Vector2(u_base + pixelSize, v_base + pixelSize));
            uvs.Add(new Vector2(u_base + pixelSize, v_base));
        }

        colors.Add(faceColor);
        colors.Add(faceColor);
        colors.Add(faceColor);
        colors.Add(faceColor);

        if (!vertexIndicesByLocalCell.TryGetValue(localCell, out List<int> vertexIndices))
        {
            vertexIndices = new List<int>(24);
            vertexIndicesByLocalCell.Add(localCell, vertexIndices);
        }

        vertexIndices.Add(vertCount);
        vertexIndices.Add(vertCount + 1);
        vertexIndices.Add(vertCount + 2);
        vertexIndices.Add(vertCount + 3);
    }

    private Vector3 GetVertexOffset(Vector3 normal, int index)
    {
        if (normal == Vector3.right)
        {
            switch (index)
            {
                case 0: return new Vector3(0.5f, -0.5f, -0.5f);
                case 1: return new Vector3(0.5f, 0.5f, -0.5f);
                case 2: return new Vector3(0.5f, 0.5f, 0.5f);
                case 3: return new Vector3(0.5f, -0.5f, 0.5f);
            }
        }
        else if (normal == Vector3.left)
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, -0.5f, 0.5f);
                case 1: return new Vector3(-0.5f, 0.5f, 0.5f);
                case 2: return new Vector3(-0.5f, 0.5f, -0.5f);
                case 3: return new Vector3(-0.5f, -0.5f, -0.5f);
            }
        }
        else if (normal == Vector3.up)
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, 0.5f, -0.5f);
                case 1: return new Vector3(-0.5f, 0.5f, 0.5f);
                case 2: return new Vector3(0.5f, 0.5f, 0.5f);
                case 3: return new Vector3(0.5f, 0.5f, -0.5f);
            }
        }
        else if (normal == Vector3.down)
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, -0.5f, 0.5f);
                case 1: return new Vector3(-0.5f, -0.5f, -0.5f);
                case 2: return new Vector3(0.5f, -0.5f, -0.5f);
                case 3: return new Vector3(0.5f, -0.5f, 0.5f);
            }
        }
        else if (normal == Vector3.forward)
        {
            switch (index)
            {
                case 0: return new Vector3(-0.5f, -0.5f, 0.5f);
                case 1: return new Vector3(-0.5f, 0.5f, 0.5f);
                case 2: return new Vector3(0.5f, 0.5f, 0.5f);
                case 3: return new Vector3(0.5f, -0.5f, 0.5f);
            }
        }
        else if (normal == Vector3.back)
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
