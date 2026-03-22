using UnityEngine;
using System.Collections.Generic;

public class VoxelTextureExtractor
{
    private int extractedTextureResolution;
    private bool enableTextureExtraction;

    public VoxelTextureExtractor(bool enableExtraction = true, int resolution = 32)
    {
        enableTextureExtraction = enableExtraction;
        extractedTextureResolution = resolution;
    }

    public void ApplyVoxelTextureToDroppedItem(
        GameObject item,
        int voxelX,
        int voxelY,
        int voxelZ,
        Texture2D texture1,
        Texture2D texture2,
        bool[,,] useTexture1Pattern,
        int voxelsPerBlock)
    {
        var itemRenderer = item.GetComponent<Renderer>();
        if (itemRenderer == null)
        {
            return;
        }

        if (!enableTextureExtraction || (texture1 == null && texture2 == null))
        {
            if (enableTextureExtraction && texture1 == null && texture2 == null)
            {
                Debug.LogWarning("Texture extraction is enabled but no textures are assigned. Please assign texture1 and/or texture2 in the Inspector.");
            }

            ApplyDefaultMaterial(itemRenderer);
            return;
        }

        List<VoxelFaceTextureInfo> faceInfos = GetAllVoxelFaceTextureInfo(
            voxelX,
            voxelY,
            voxelZ,
            texture1,
            texture2,
            useTexture1Pattern,
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

    public Texture2D ExtractVoxelTextureRegion(VoxelFaceTextureInfo faceInfo)
    {
        if (faceInfo.sourceTexture == null)
        {
            return null;
        }

        try
        {
            if (!faceInfo.sourceTexture.isReadable)
            {
                Debug.LogWarning($"Texture '{faceInfo.sourceTexture.name}' is not readable. Enable 'Read/Write Enabled' in texture import settings.");
                return null;
            }

            int sourceWidth = faceInfo.sourceTexture.width;
            int sourceHeight = faceInfo.sourceTexture.height;

            int startX = Mathf.FloorToInt(faceInfo.uvBase.x * sourceWidth);
            int startY = Mathf.FloorToInt(faceInfo.uvBase.y * sourceHeight);
            int regionWidth = Mathf.FloorToInt(faceInfo.uvSize.x * sourceWidth);
            int regionHeight = Mathf.FloorToInt(faceInfo.uvSize.y * sourceHeight);

            startX = Mathf.Clamp(startX, 0, sourceWidth - 1);
            startY = Mathf.Clamp(startY, 0, sourceHeight - 1);
            regionWidth = Mathf.Clamp(regionWidth, 1, sourceWidth - startX);
            regionHeight = Mathf.Clamp(regionHeight, 1, sourceHeight - startY);

            regionWidth = Mathf.Max(regionWidth, 1);
            regionHeight = Mathf.Max(regionHeight, 1);

            Texture2D extractedTexture = new Texture2D(regionWidth, regionHeight, TextureFormat.RGBA32, false);
            Color[] sourcePixels = faceInfo.sourceTexture.GetPixels(startX, startY, regionWidth, regionHeight);
            extractedTexture.SetPixels(sourcePixels);
            extractedTexture.Apply();

            return extractedTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to extract voxel texture: {e.Message}");
            return null;
        }
    }

    public VoxelFaceTextureInfo GetVoxelFaceTextureInfo(
        int voxelX,
        int voxelY,
        int voxelZ,
        Vector3 normal,
        Texture2D texture1,
        Texture2D texture2,
        bool[,,] useTexture1Pattern,
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

        if (useTexture1Pattern == null)
        {
            Debug.LogWarning("useTexture1Pattern is null");
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }

        bool useTexture1 = useTexture1Pattern[voxelX, voxelY, voxelZ];
        Texture2D sourceTexture = useTexture1 ? texture1 : texture2;

        if (sourceTexture == null)
        {
            Debug.LogWarning($"Source texture is null for voxel at ({voxelX}, {voxelY}, {voxelZ}), useTexture1: {useTexture1}");
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
        bool[,,] useTexture1Pattern,
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
                useTexture1Pattern,
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

    private VoxelFaceTextureInfo GetRepresentativeFace(List<VoxelFaceTextureInfo> faceInfos)
    {
        Vector3[] priorityOrder =
        {
            Vector3.forward,
            Vector3.up,
            Vector3.right,
            Vector3.left,
            Vector3.back,
            Vector3.down
        };

        foreach (Vector3 priorityNormal in priorityOrder)
        {
            VoxelFaceTextureInfo face = faceInfos.Find(f => f.faceNormal == priorityNormal && f.isExposed);
            if (face.sourceTexture != null)
            {
                return face;
            }
        }

        return faceInfos.Count > 0 ? faceInfos[0] : new VoxelFaceTextureInfo();
    }
}
