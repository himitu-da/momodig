using System.Collections.Generic;
using UnityEngine;

public class SolidificationCandidateIndex
{
    public struct CandidateHit
    {
        public Vector3Int blockPosition;
        public Vector3Int localPosition;
        public Vector3 worldPosition;
        public float distanceSqr;
    }

    private static readonly Vector3Int[] NeighborDirections =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    private readonly Dictionary<Vector3Int, HashSet<Vector3Int>> bucketsByBlock =
        new Dictionary<Vector3Int, HashSet<Vector3Int>>();

    private VoxelManager voxelManager;
    private TerrainSettings settings;

    public void Initialize(VoxelManager voxelManager, TerrainSettings settings)
    {
        this.voxelManager = voxelManager;
        this.settings = settings;
    }

    public void Clear()
    {
        bucketsByBlock.Clear();
    }

    public void RemoveBlock(Vector3Int blockPosition)
    {
        bucketsByBlock.Remove(blockPosition);
        ReEvaluateNeighborBoundary(blockPosition);
    }

    public void RebuildBlock(Vector3Int blockPosition)
    {
        if (settings == null || voxelManager == null) return;

        if (bucketsByBlock.TryGetValue(blockPosition, out var bucket))
        {
            bucket.Clear();
        }

        int voxelsPerBlock = Mathf.Max(1, settings.voxelsPerBlock);
        for (int x = 0; x < voxelsPerBlock; x++)
        {
            for (int y = 0; y < voxelsPerBlock; y++)
            {
                for (int z = 0; z < voxelsPerBlock; z++)
                {
                    EvaluateCell(blockPosition, new Vector3Int(x, y, z));
                }
            }
        }

        ReEvaluateNeighborBoundary(blockPosition);
    }

    public void RefreshCellAndNeighbors(Vector3Int blockPosition, Vector3Int localPosition)
    {
        if (voxelManager == null) return;

        EvaluateCell(blockPosition, localPosition);

        foreach (var direction in NeighborDirections)
        {
            Vector3Int neighborBlock = blockPosition;
            Vector3Int neighborLocal = localPosition + direction;
            if (voxelManager.NormalizeVoxelPosition(ref neighborBlock, ref neighborLocal))
            {
                EvaluateCell(neighborBlock, neighborLocal);
            }
        }
    }

    public List<CandidateHit> FindNearestCandidates(Vector3 worldPosition, int maxResults, int maxBlockRadius = 8)
    {
        var results = new List<CandidateHit>();
        if (settings == null || voxelManager == null || maxResults <= 0) return results;

        float blockSize = Mathf.Max(0.001f, settings.blockSize);
        Vector3Int originBlock = new Vector3Int(
            Mathf.RoundToInt(worldPosition.x / blockSize),
            Mathf.RoundToInt(worldPosition.y / blockSize),
            0
        );

        int extraRingsAfterEnough = 1;
        int ringsScannedSinceEnough = -1;

        for (int r = 0; r <= maxBlockRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;

                    Vector3Int bp = new Vector3Int(originBlock.x + dx, originBlock.y + dy, 0);
                    if (!bucketsByBlock.TryGetValue(bp, out var bucket) || bucket.Count == 0) continue;

                    foreach (var lp in bucket)
                    {
                        Vector3 cellWorld = voxelManager.CalculateWorldPosition(bp, lp);
                        float distSqr = (cellWorld - worldPosition).sqrMagnitude;
                        results.Add(new CandidateHit
                        {
                            blockPosition = bp,
                            localPosition = lp,
                            worldPosition = cellWorld,
                            distanceSqr = distSqr
                        });
                    }
                }
            }

            if (ringsScannedSinceEnough < 0 && results.Count >= maxResults)
            {
                ringsScannedSinceEnough = 0;
            }
            else if (ringsScannedSinceEnough >= 0)
            {
                ringsScannedSinceEnough++;
                if (ringsScannedSinceEnough > extraRingsAfterEnough) break;
            }
        }

        results.Sort((a, b) => a.distanceSqr.CompareTo(b.distanceSqr));
        if (results.Count > maxResults)
        {
            results.RemoveRange(maxResults, results.Count - maxResults);
        }
        return results;
    }

    public int GetCandidateCount()
    {
        int count = 0;
        foreach (var bucket in bucketsByBlock.Values)
        {
            count += bucket.Count;
        }
        return count;
    }

    private void EvaluateCell(Vector3Int blockPosition, Vector3Int localPosition)
    {
        bool isCandidate = voxelManager.IsVoxelCellEmpty(blockPosition, localPosition) &&
                           voxelManager.HasActiveAdjacentVoxel(blockPosition, localPosition);

        if (isCandidate)
        {
            if (!bucketsByBlock.TryGetValue(blockPosition, out var bucket))
            {
                bucket = new HashSet<Vector3Int>();
                bucketsByBlock[blockPosition] = bucket;
            }
            bucket.Add(localPosition);
        }
        else if (bucketsByBlock.TryGetValue(blockPosition, out var bucket))
        {
            bucket.Remove(localPosition);
            if (bucket.Count == 0)
            {
                bucketsByBlock.Remove(blockPosition);
            }
        }
    }

    private void ReEvaluateNeighborBoundary(Vector3Int blockPosition)
    {
        if (settings == null || voxelManager == null) return;

        int voxelsPerBlock = Mathf.Max(1, settings.voxelsPerBlock);
        int last = voxelsPerBlock - 1;

        ReEvaluateBoundaryFace(blockPosition + new Vector3Int(1, 0, 0), 0, -1, -1, voxelsPerBlock);
        ReEvaluateBoundaryFace(blockPosition + new Vector3Int(-1, 0, 0), last, -1, -1, voxelsPerBlock);
        ReEvaluateBoundaryFace(blockPosition + new Vector3Int(0, 1, 0), -1, 0, -1, voxelsPerBlock);
        ReEvaluateBoundaryFace(blockPosition + new Vector3Int(0, -1, 0), -1, last, -1, voxelsPerBlock);
    }

    private void ReEvaluateBoundaryFace(Vector3Int blockPosition, int fixedX, int fixedY, int fixedZ, int voxelsPerBlock)
    {
        for (int x = 0; x < voxelsPerBlock; x++)
        {
            if (fixedX >= 0 && x != fixedX) continue;
            for (int y = 0; y < voxelsPerBlock; y++)
            {
                if (fixedY >= 0 && y != fixedY) continue;
                for (int z = 0; z < voxelsPerBlock; z++)
                {
                    if (fixedZ >= 0 && z != fixedZ) continue;
                    EvaluateCell(blockPosition, new Vector3Int(x, y, z));
                }
            }
        }
    }
}
