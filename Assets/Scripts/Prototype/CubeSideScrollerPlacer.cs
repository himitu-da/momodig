using UnityEngine;
using System;

public class CubeSideScrollerPlacer : BaseCubePlacer
{
    [SerializeField] private Vector3Int center; // 中心座標
    [SerializeField] private Vector2Int chankCount;   // XY平面のマスの数

    protected override void Generate()
    {
        // チャンク全体のワールドサイズを計算
        float totalWorldSizeX = chankCount.x * chunkSize;
        float totalWorldSizeY = chankCount.y * chunkSize;

        // チャンク群の左下奥にあるチャンクの中心座標を計算
        Vector3 startPosition = new Vector3(
            center.x - totalWorldSizeX / 2.0f + chunkSize / 2.0f,
            center.y - totalWorldSizeY / 2.0f + chunkSize / 2.0f,
            center.z
        );

        // 元々のGameObjectの位置をオフセットとして加算
        transform.position = startPosition;

        // chankCountに基づいてチャンクを生成
        for (int x = 0; x < chankCount.x; x++)
        {
            for (int y = 0; y < chankCount.y; y++)
            {
                // チャンクの論理的な座標
                Vector3Int chunkPos = new Vector3Int(x, y, 0);

                // 全てが埋まったチャンクパターンを作成
                bool[,,] pattern = new bool[VoxelChunk.ChunkSize, VoxelChunk.ChunkSize, VoxelChunk.ChunkSize];
                for (int lx = 0; lx < VoxelChunk.ChunkSize; lx++)
                {
                    for (int ly = 0; ly < VoxelChunk.ChunkSize; ly++)
                    {
                        for (int lz = 0; lz < VoxelChunk.ChunkSize; lz++)
                        {
                            pattern[lx, ly, lz] = true;
                        }
                    }
                }
                
                CreateChunk(chunkPos, pattern);
            }
        }
    }
}
