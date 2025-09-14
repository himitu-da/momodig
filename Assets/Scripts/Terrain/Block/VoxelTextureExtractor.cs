using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ボクセルのテクスチャ抽出処理を担当するクラス
/// Block.csから分離されたテクスチャ関連の機能を提供
/// </summary>
public class VoxelTextureExtractor
{
    private int extractedTextureResolution;
    private bool enableTextureExtraction;

    public VoxelTextureExtractor(bool enableExtraction = true, int resolution = 32)
    {
        enableTextureExtraction = enableExtraction;
        extractedTextureResolution = resolution;
    }

    /// <summary>
    /// ドロップアイテムにボクセルテクスチャを適用
    /// </summary>
    public void ApplyVoxelTextureToDroppedItem(GameObject item, int voxelX, int voxelY, int voxelZ, 
        Texture2D texture1, Texture2D texture2, bool[,,] useTexture1Pattern, int voxelsPerBlock)
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
        List<VoxelFaceTextureInfo> faceInfos = GetAllVoxelFaceTextureInfo(voxelX, voxelY, voxelZ, 
            texture1, texture2, useTexture1Pattern, voxelsPerBlock);
        
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
                // Custom Unlitマテリアルを作成
                var material = new Material(Shader.Find("Custom/UnlitBlock"));
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
    public Texture2D ExtractVoxelTextureRegion(VoxelFaceTextureInfo faceInfo)
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
    /// 指定されたボクセルの面テクスチャ情報を取得
    /// </summary>
    public VoxelFaceTextureInfo GetVoxelFaceTextureInfo(int voxelX, int voxelY, int voxelZ, Vector3 normal,
        Texture2D texture1, Texture2D texture2, bool[,,] useTexture1Pattern, int voxelsPerBlock)
    {
        // ボクセル位置の境界チェック
        if (voxelX < 0 || voxelX >= voxelsPerBlock || voxelY < 0 || voxelY >= voxelsPerBlock || voxelZ < 0 || voxelZ >= voxelsPerBlock)
        {
            Debug.LogWarning($"Voxel position ({voxelX}, {voxelY}, {voxelZ}) is out of bounds (chunk size: {voxelsPerBlock})");
            return new VoxelFaceTextureInfo(normal, Vector2.zero, Vector2.zero, null, false);
        }

        // AddFaceメソッドのUV計算ロジックを再利用
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
    private List<VoxelFaceTextureInfo> GetAllVoxelFaceTextureInfo(int voxelX, int voxelY, int voxelZ,
        Texture2D texture1, Texture2D texture2, bool[,,] useTexture1Pattern, int voxelsPerBlock)
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
            VoxelFaceTextureInfo faceInfo = GetVoxelFaceTextureInfo(voxelX, voxelY, voxelZ, normal,
                texture1, texture2, useTexture1Pattern, voxelsPerBlock);
            faceInfos.Add(faceInfo);
        }

        return faceInfos;
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
        var material = new Material(Shader.Find("Custom/UnlitBlock"));
        material.color = Color.white; // Unlitなのでテクスチャの色をそのまま出すために白に
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
}
