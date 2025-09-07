using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロックの掘削処理を担当するクラス
/// Block.csから分離された掘削関連の機能を提供
/// </summary>
public class BlockDiggingSystem
{
    // 依存注入されるコンポーネント
    private VoxelManager voxelManager;
    private BlockItemDropper itemDropper;
    private Block targetBlock;
    
    // 掘削パラメータ
    private float diggingThreshold;
    private int diggingFrameDelay;
    private int chunkSize;
    private Vector3Int blockPosition;

    /// <summary>
    /// BlockDiggingSystemを初期化
    /// </summary>
    public void Initialize(VoxelManager manager, BlockItemDropper dropper, Block block, 
        float threshold, int frameDelay, int chunk, Vector3Int position)
    {
        voxelManager = manager;
        itemDropper = dropper;
        targetBlock = block;
        diggingThreshold = threshold;
        diggingFrameDelay = frameDelay;
        chunkSize = chunk;
        blockPosition = position;
    }

    /// <summary>
    /// ブロックにダメージを与える
    /// </summary>
    public void TakeDamage(Vector3 localPos, int damage)
    {
        int x = Mathf.FloorToInt(localPos.x + chunkSize / 2.0f);
        int y = Mathf.FloorToInt(localPos.y + chunkSize / 2.0f);
        int z = Mathf.FloorToInt(localPos.z + chunkSize / 2.0f);
        
        Vector3Int localVoxelPos = new Vector3Int(x, y, z);

        // VoxelManagerにダメージ処理を移管
        if (voxelManager.DamageVoxel(blockPosition, localVoxelPos, damage))
        {
            // Voxelが破壊された場合
            var voxelData = voxelManager.GetVoxelAt(blockPosition, localVoxelPos);
            if (voxelData != null)
            {
                 itemDropper.DropItem(voxelData.worldPosition, x, y, z);
            }
            targetBlock.GenerateMesh(); // メッシュを更新
        }
    }

    /// <summary>
    /// ボクセルを掘削する
    /// </summary>
    public System.Collections.IEnumerator DigVoxels(BoxCollider diggingArea, int damagePerHit)
    {
        const int sampleResolution = 3;
        const int totalSamples = sampleResolution * sampleResolution * sampleResolution;

        Matrix4x4 worldToLocalMatrix = targetBlock.transform.worldToLocalMatrix;
        Matrix4x4 diggingAreaWorldToLocal = diggingArea.transform.worldToLocalMatrix;
        Vector3 halfSize = diggingArea.size * 0.5f;
        Vector3 center = diggingArea.center;

        Bounds diggingBounds = diggingArea.bounds;
        Vector3 localMin = worldToLocalMatrix.MultiplyPoint3x4(diggingBounds.min);
        Vector3 localMax = worldToLocalMatrix.MultiplyPoint3x4(diggingBounds.max);

        int startX = Mathf.Max(0, Mathf.FloorToInt(localMin.x + chunkSize / 2.0f));
        int endX = Mathf.Min(chunkSize - 1, Mathf.CeilToInt(localMax.x + chunkSize / 2.0f));
        int startY = Mathf.Max(0, Mathf.FloorToInt(localMin.y + chunkSize / 2.0f));
        int endY = Mathf.Min(chunkSize - 1, Mathf.CeilToInt(localMax.y + chunkSize / 2.0f));
        int startZ = Mathf.Max(0, Mathf.FloorToInt(localMin.z + chunkSize / 2.0f));
        int endZ = Mathf.Min(chunkSize - 1, Mathf.CeilToInt(localMax.z + chunkSize / 2.0f));

        // 現在の移動モードを取得
        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        PlayerController.MoveMode moveMode = PlayerController.MoveMode.TopDown;
        if (playerController != null)
        {
            moveMode = playerController.currentMoveMode;
        }

        if (moveMode == PlayerController.MoveMode.SideScroller)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                bool layerModified = false;
                List<System.Action> dropActions = new List<System.Action>();

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (ProcessVoxel(x, y, z, diggingArea, sampleResolution, totalSamples, worldToLocalMatrix, diggingAreaWorldToLocal, halfSize, center, dropActions, damagePerHit))
                        {
                            layerModified = true;
                        }
                    }
                }

                if (layerModified)
                {
                    foreach (var action in dropActions) action.Invoke();
                    targetBlock.GenerateMesh();
                }

                int delay = Mathf.Max(1, diggingFrameDelay);
                for (int i = 0; i < delay; i++)
                {
                    yield return null;
                }
            }
        }
        else // TopDown or other modes
        {
            for (int y = endY; y >= startY; y--)
            {
                bool layerModified = false;
                List<System.Action> dropActions = new List<System.Action>();

                for (int x = startX; x <= endX; x++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        if (ProcessVoxel(x, y, z, diggingArea, sampleResolution, totalSamples, worldToLocalMatrix, diggingAreaWorldToLocal, halfSize, center, dropActions, damagePerHit))
                        {
                            layerModified = true;
                        }
                    }
                }

                if (layerModified)
                {
                    foreach (var action in dropActions) action.Invoke();
                    targetBlock.GenerateMesh();
                }

                int delay = Mathf.Max(1, diggingFrameDelay);
                for (int i = 0; i < delay; i++)
                {
                    yield return null;
                }
            }
        }
    }

    /// <summary>
    /// 個別ボクセルの掘削処理
    /// </summary>
    private bool ProcessVoxel(int x, int y, int z, BoxCollider diggingArea, int sampleResolution, int totalSamples, Matrix4x4 worldToLocalMatrix, Matrix4x4 diggingAreaWorldToLocal, Vector3 halfSize, Vector3 center, List<System.Action> dropActions, int damagePerHit)
    {
        Vector3Int localVoxelPos = new Vector3Int(x, y, z);
        var voxelData = voxelManager.GetVoxelAt(blockPosition, localVoxelPos);
        if (voxelData == null || !voxelData.isActive) return false;

        int containedSamples = 0;
        Vector3 voxelMin = new Vector3(x - chunkSize / 2.0f, y - chunkSize / 2.0f, z - chunkSize / 2.0f);

        for (int sx = 0; sx < sampleResolution; sx++)
        {
            for (int sy = 0; sy < sampleResolution; sy++)
            {
                for (int sz = 0; sz < sampleResolution; sz++)
                {
                    float sampleX = voxelMin.x + (sx + 0.5f) / sampleResolution;
                    float sampleY = voxelMin.y + (sy + 0.5f) / sampleResolution;
                    float sampleZ = voxelMin.z + (sz + 0.5f) / sampleResolution;
                    Vector3 sampleLocalPos = new Vector3(sampleX, sampleY, sampleZ);
                    Vector3 sampleWorldPos = targetBlock.transform.TransformPoint(sampleLocalPos);
                    Vector3 localPosInDiggingArea = diggingAreaWorldToLocal.MultiplyPoint3x4(sampleWorldPos);

                    if (Mathf.Abs(localPosInDiggingArea.x - center.x) <= halfSize.x &&
                        Mathf.Abs(localPosInDiggingArea.y - center.y) <= halfSize.y &&
                        Mathf.Abs(localPosInDiggingArea.z - center.z) <= halfSize.z)
                    {
                        containedSamples++;
                    }
                }
            }
        }

        float overlapRatio = (float)containedSamples / totalSamples;
        if (overlapRatio >= diggingThreshold)
        {
            if (voxelManager.DamageVoxel(blockPosition, localVoxelPos, damagePerHit))
            {
                dropActions.Add(() => itemDropper.DropItem(voxelData.worldPosition, x, y, z));
                return true;
            }
        }
        return false;
    }
}
