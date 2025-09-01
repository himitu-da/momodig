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

        // TODO: いい感じにZ軸の調整を行い、チャンクが少なくとも1つは生成されるようにする
        // 元々のGameObjectの位置をオフセットとして加算
        transform.position = startPosition;

        // chankCountに基づいてチャンクを生成
        for (int x = 0; x < chankCount.x; x++)
        {
            for (int y = 0; y < chankCount.y; y++)
            {
                // チャンクの論理的な座標
                Vector3Int chunkPos = new Vector3Int(x, y, 0);

                // 条件に基づいてチャンクパターンを作成
                bool[,,] pattern = new bool[voxelSize, voxelSize, voxelSize];
                float cubeSize = chunkSize / voxelSize;
                for (int lx = 0; lx < voxelSize; lx++)
                {
                    for (int ly = 0; ly < voxelSize; ly++)
                    {
                        for (int lz = 0; lz < voxelSize; lz++)
                        {
                            // Z軸のローカル座標を計算
                            float zPos = (lz - (voxelSize - 1) / 2.0f) * cubeSize;
                            // ワールド座標系でのZ座標を計算
                            float worldZPos = center.z + zPos;
                            // ボクセルの頂点がZ軸の±0.5の境界をはみ出さないように条件を変更
                            if (Mathf.Abs(worldZPos) + cubeSize / 2.0f <= 0.5f)
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
