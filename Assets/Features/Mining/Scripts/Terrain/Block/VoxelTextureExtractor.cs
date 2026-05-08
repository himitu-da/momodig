using UnityEngine;
using System.Collections.Generic;

public class VoxelTextureExtractor
{
    public void ApplyVoxelTextureToDroppedItem(
        GameObject item,
        int voxelX,
        int voxelY,
        int voxelZ,
        Texture2D texture1,
        Texture2D texture2,
        bool useTexture1,
        int voxelsPerBlock)
    {
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer == null)
        {
            return;
        }

        if (texture1 == null && texture2 == null)
        {
            ApplyDefaultMaterial(itemRenderer);
            return;
        }

        List<VoxelFaceTextureInfo> faceInfos = GetAllVoxelFaceTextureInfo(
            voxelX,
            voxelY,
            voxelZ,
            texture1,
            texture2,
            useTexture1,
            voxelsPerBlock);

        if (faceInfos.Count == 0 || faceInfos.TrueForAll(face => face.sourceTexture == null))
        {
            ApplyDefaultMaterial(itemRenderer);
            Debug.LogWarning($"No texture found for voxel at ({voxelX}, {voxelY}, {voxelZ})");
            return;
        }

        DroppedItem droppedItem = item.GetComponent<DroppedItem>();
        if (droppedItem == null)
        {
            ApplyDefaultMaterial(itemRenderer);
            Debug.LogWarning("DroppedItem component is missing, using default material");
            return;
        }

        droppedItem.ApplyFaceTextureInfos(faceInfos, texture1, texture2);
    }

    public VoxelFaceTextureInfo GetVoxelFaceTextureInfo(
        int voxelX,
        int voxelY,
        int voxelZ,
        Vector3 normal,
        Texture2D texture1,
        Texture2D texture2,
        bool useTexture1,
        int voxelsPerBlock)
    {
        if (voxelX < 0 || voxelX >= voxelsPerBlock ||
            voxelY < 0 || voxelY >= voxelsPerBlock ||
            voxelZ < 0 || voxelZ >= voxelsPerBlock)
        {
            Debug.LogWarning($"Voxel position ({voxelX}, {voxelY}, {voxelZ}) is out of bounds (chunk size: {voxelsPerBlock})");
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }

        float pixelSize = 1.0f / voxelsPerBlock;
        float uBase = 0f;
        float vBase = 0f;

        if (normal == Vector3.right || normal == Vector3.left)
        {
            uBase = (float)voxelZ / voxelsPerBlock;
            vBase = (float)voxelY / voxelsPerBlock;
        }
        else if (normal == Vector3.up || normal == Vector3.down)
        {
            uBase = (float)voxelX / voxelsPerBlock;
            vBase = (float)voxelZ / voxelsPerBlock;
        }
        else if (normal == Vector3.forward || normal == Vector3.back)
        {
            uBase = (float)voxelX / voxelsPerBlock;
            vBase = (float)voxelY / voxelsPerBlock;
        }

        Texture2D primary = useTexture1 ? texture1 : texture2;
        Texture2D fallback = useTexture1 ? texture2 : texture1;
        Texture2D sourceTexture = primary != null ? primary : fallback;

        if (sourceTexture == null)
        {
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }

        return new VoxelFaceTextureInfo(
            normal,
            new Vector2(uBase, vBase),
            new Vector2(pixelSize, pixelSize),
            sourceTexture,
            true);
    }

    private List<VoxelFaceTextureInfo> GetAllVoxelFaceTextureInfo(
        int voxelX,
        int voxelY,
        int voxelZ,
        Texture2D texture1,
        Texture2D texture2,
        bool useTexture1,
        int voxelsPerBlock)
    {
        List<VoxelFaceTextureInfo> faceInfos = new List<VoxelFaceTextureInfo>();
        Vector3[] faceNormals =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        foreach (Vector3 normal in faceNormals)
        {
            VoxelFaceTextureInfo faceInfo = GetVoxelFaceTextureInfo(
                voxelX,
                voxelY,
                voxelZ,
                normal,
                texture1,
                texture2,
                useTexture1,
                voxelsPerBlock);
            faceInfos.Add(faceInfo);
        }

        return faceInfos;
    }

    private void ApplyDefaultMaterial(Renderer renderer)
    {
        var material = new Material(Shader.Find("Custom/Default"));
        material.renderQueue = RenderQueue.Geometry;
        material.color = Color.white;
        renderer.material = material;
    }
}
