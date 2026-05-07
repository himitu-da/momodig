using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class BlockDiggingSystem
{
    private VoxelManager voxelManager;
    private BlockItemDropper itemDropper;
    private Block targetBlock;

    private float diggingThreshold;
    private int diggingFrameDelay;
    private int voxelsPerBlock;
    private Vector3Int blockPosition;

    public void Initialize(VoxelManager manager, BlockItemDropper dropper, Block block,
        float threshold, int frameDelay, int vPerBlock, Vector3Int position)
    {
        voxelManager = manager;
        itemDropper = dropper;
        targetBlock = block;
        diggingThreshold = threshold;
        diggingFrameDelay = frameDelay;
        voxelsPerBlock = vPerBlock;
        blockPosition = position;
    }

    public void TakeDamage(Vector3 localPos, int damage)
    {
        int x = Mathf.FloorToInt(localPos.x + voxelsPerBlock / 2.0f);
        int y = Mathf.FloorToInt(localPos.y + voxelsPerBlock / 2.0f);
        int z = Mathf.FloorToInt(localPos.z + voxelsPerBlock / 2.0f);

        Vector3Int localVoxelPos = new Vector3Int(x, y, z);
        Voxel voxelData = voxelManager.GetVoxelAt(blockPosition, localVoxelPos);

        if (voxelData != null && voxelManager.DamageVoxel(blockPosition, localVoxelPos, damage))
        {
            itemDropper.DropItem(voxelData.worldPosition, voxelData.blockData, x, y, z);
            targetBlock.GenerateMesh();
        }
    }

    public async UniTask<int> DigVoxels(BoxCollider diggingArea, int damagePerHit)
    {
        int destroyedVoxelCount = 0;
        AudioClip destructionSound = null;
        float destructionSoundVolume = 1.0f;

        const int sampleResolution = 3;
        const int totalSamples = sampleResolution * sampleResolution * sampleResolution;

        Matrix4x4 worldToLocalMatrix = targetBlock.transform.worldToLocalMatrix;
        Matrix4x4 diggingAreaWorldToLocal = diggingArea.transform.worldToLocalMatrix;
        Vector3 halfSize = diggingArea.size * 0.5f;
        Vector3 center = diggingArea.center;

        Bounds diggingBounds = diggingArea.bounds;
        Vector3 localMin = worldToLocalMatrix.MultiplyPoint3x4(diggingBounds.min);
        Vector3 localMax = worldToLocalMatrix.MultiplyPoint3x4(diggingBounds.max);

        int startX = Mathf.Max(0, Mathf.FloorToInt(localMin.x + voxelsPerBlock / 2.0f));
        int endX = Mathf.Min(voxelsPerBlock - 1, Mathf.CeilToInt(localMax.x + voxelsPerBlock / 2.0f));
        int startY = Mathf.Max(0, Mathf.FloorToInt(localMin.y + voxelsPerBlock / 2.0f));
        int endY = Mathf.Min(voxelsPerBlock - 1, Mathf.CeilToInt(localMax.y + voxelsPerBlock / 2.0f));
        int startZ = Mathf.Max(0, Mathf.FloorToInt(localMin.z + voxelsPerBlock / 2.0f));
        int endZ = Mathf.Min(voxelsPerBlock - 1, Mathf.CeilToInt(localMax.z + voxelsPerBlock / 2.0f));

        PlayerController playerController = Object.FindFirstObjectByType<PlayerController>();
        PlayerController.MoveMode moveMode = playerController != null ? playerController.currentMoveMode : PlayerController.MoveMode.TopDown;

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
                        if (ProcessVoxel(x, y, z, diggingArea, sampleResolution, totalSamples, worldToLocalMatrix, diggingAreaWorldToLocal, halfSize, center, dropActions, damagePerHit, ref destructionSound, ref destructionSoundVolume))
                        {
                            layerModified = true;
                            destroyedVoxelCount++;
                        }
                    }
                }

                if (layerModified)
                {
                    foreach (var action in dropActions) action.Invoke();
                    targetBlock.GenerateMesh();
                }

                await DelayFrames();
            }
        }
        else
        {
            for (int y = endY; y >= startY; y--)
            {
                bool layerModified = false;
                List<System.Action> dropActions = new List<System.Action>();

                for (int x = startX; x <= endX; x++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        if (ProcessVoxel(x, y, z, diggingArea, sampleResolution, totalSamples, worldToLocalMatrix, diggingAreaWorldToLocal, halfSize, center, dropActions, damagePerHit, ref destructionSound, ref destructionSoundVolume))
                        {
                            layerModified = true;
                            destroyedVoxelCount++;
                        }
                    }
                }

                if (layerModified)
                {
                    foreach (var action in dropActions) action.Invoke();
                    targetBlock.GenerateMesh();
                }

                await DelayFrames();
            }
        }

        if (destructionSound != null)
        {
            AudioManager.Instance.PlayVoxelDestroyedSE(destructionSound, destroyedVoxelCount, destructionSoundVolume);
        }

        return destroyedVoxelCount;
    }

    private async UniTask DelayFrames()
    {
        int delay = Mathf.Max(1, diggingFrameDelay);
        for (int i = 0; i < delay; i++)
        {
            await UniTask.Yield();
        }
    }

    private bool ProcessVoxel(int x, int y, int z, BoxCollider diggingArea, int sampleResolution, int totalSamples,
        Matrix4x4 worldToLocalMatrix, Matrix4x4 diggingAreaWorldToLocal, Vector3 halfSize, Vector3 center,
        List<System.Action> dropActions, int damagePerHit, ref AudioClip destructionSound, ref float destructionSoundVolume)
    {
        Vector3Int localVoxelPos = new Vector3Int(x, y, z);
        Voxel voxelData = voxelManager.GetVoxelAt(blockPosition, localVoxelPos);
        if (voxelData == null || !voxelData.isActive) return false;

        int containedSamples = 0;
        Vector3 voxelMin = new Vector3(x - voxelsPerBlock / 2.0f, y - voxelsPerBlock / 2.0f, z - voxelsPerBlock / 2.0f);

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
        if (overlapRatio < diggingThreshold) return false;

        BlockData voxelBlockData = voxelData.blockData;
        Vector3 dropPosition = voxelData.worldPosition;

        if (!voxelManager.DamageVoxel(blockPosition, localVoxelPos, damagePerHit)) return false;

        if (destructionSound == null && voxelBlockData != null && voxelBlockData.destroyedSound != null)
        {
            destructionSound = voxelBlockData.destroyedSound;
            destructionSoundVolume = voxelBlockData.destroyedSoundVolume;
        }

        dropActions.Add(() => itemDropper.DropItem(dropPosition, voxelBlockData, x, y, z));
        return true;
    }
}
