using UnityEngine;
using System;

public class CubeTopDownPlacer : BaseCubePlacer
{
    [SerializeField] private Vector3Int center; // 中心座標
    [SerializeField] private Vector2Int chankCount;   // XZ平面のマスの数

    protected override void Generate()
    {
        // チャンク全体のワールドサイズを計算
        float totalWorldSizeX = chankCount.x * chunkSize;
        float totalWorldSizeZ = chankCount.y * chunkSize;

        // チャンク群の左下にあるチャンクの中心座標を計算
        Vector3 startPosition = new Vector3(
            center.x - totalWorldSizeX / 2.0f + chunkSize / 2.0f,
            center.y,
            center.z - totalWorldSizeZ / 2.0f + chunkSize / 2.0f
        );

        // 元々のGameObjectの位置をオフセットとして加算
        transform.position = startPosition;

        // chankCountに基づいてチャンクを生成
        for (int x = 0; x < chankCount.x; x++)
        {
            for (int z = 0; z < chankCount.y; z++)
            {
                // チャンクの論理的な座標
                Vector3Int chunkPos = new Vector3Int(x, 0, z);

                // 条件に基づいてチャンクパターンを作成
                bool[,,] pattern = new bool[voxelSize, voxelSize, voxelSize];
                float cubeSize = chunkSize / voxelSize;
                for (int lx = 0; lx < voxelSize; lx++)
                {
                    for (int ly = 0; ly < voxelSize; ly++)
                    {
                        for (int lz = 0; lz < voxelSize; lz++)
                        {
                            // Y軸の絶対値が0.5以下のボクセルのみ生成
                            float yPos = (ly - (voxelSize - 1) / 2.0f) * cubeSize;
                            if (Mathf.Abs(yPos) <= 0.5f)
                            {
                                pattern[lx, ly, lz] = true;
                            }
                            else
                            {
                                pattern[lx, ly, lz] = false;
                            }
                        }
                    }
                }
                
                CreateChunk(chunkPos, pattern);
            }
        }
    }
}
