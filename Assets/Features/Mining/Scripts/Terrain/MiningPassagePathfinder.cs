using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MiningPassagePathOptions
{
    [Min(1)] public int endpointSearchRadiusCells = 3;
    [Min(0)] public int searchPaddingCells = 24;
    [Min(16)] public int maxVisitedCells = 4096;
}

public sealed class MiningPassagePathfinder
{
    private static readonly Vector3Int[] NeighborOffsets =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    private readonly TerrainDataManager terrainDataManager;
    private readonly VoxelManager voxelManager;
    private readonly int voxelsPerBlock;
    private readonly MiningPassagePathOptions options;

    public MiningPassagePathfinder(TerrainManager terrainManager, MiningPassagePathOptions options)
    {
        this.terrainDataManager = terrainManager != null ? terrainManager.TerrainDataManager : null;
        this.voxelManager = terrainManager != null ? terrainManager.VoxelManager : null;
        TerrainSettings settings = terrainManager != null ? terrainManager.Settings : null;
        this.voxelsPerBlock = Mathf.Max(1, settings != null ? settings.voxelsPerBlock : 1);
        this.options = options ?? new MiningPassagePathOptions();
    }

    public bool TryFindPath(Vector3 startWorldPosition, Vector3 goalWorldPosition, List<Vector3> waypoints)
    {
        return TryFindPath(startWorldPosition, goalWorldPosition, waypoints, out _);
    }

    public bool TryFindPath(Vector3 startWorldPosition, Vector3 goalWorldPosition, List<Vector3> waypoints, out float pathLength)
    {
        pathLength = 0f;
        if (waypoints == null)
        {
            return false;
        }

        waypoints.Clear();
        if (voxelManager == null)
        {
            return false;
        }

        if (!voxelManager.TryGetVoxelCellAtWorldPosition(startWorldPosition, out VoxelCellKey startKey) ||
            !voxelManager.TryGetVoxelCellAtWorldPosition(goalWorldPosition, out VoxelCellKey goalKey))
        {
            return false;
        }

        if (!TryResolvePassageCell(startKey, out VoxelCellKey resolvedStart) ||
            !TryResolvePassageCell(goalKey, out VoxelCellKey resolvedGoal))
        {
            return false;
        }

        if (resolvedStart.Equals(resolvedGoal))
        {
            AppendWaypoint(waypoints, voxelManager.GetVoxelCellWorldBounds(resolvedGoal).center);
            AppendWaypoint(waypoints, goalWorldPosition);
            pathLength = CalculatePathLength(startWorldPosition, waypoints);
            return waypoints.Count > 0;
        }

        if (!TrySearch(resolvedStart, resolvedGoal, waypoints))
        {
            waypoints.Clear();
            return false;
        }

        AppendWaypoint(waypoints, goalWorldPosition);
        pathLength = CalculatePathLength(startWorldPosition, waypoints);
        return waypoints.Count > 0;
    }

    private bool TryResolvePassageCell(VoxelCellKey origin, out VoxelCellKey resolved)
    {
        resolved = origin;
        if (IsPassageCell(origin))
        {
            return true;
        }

        int radiusLimit = Mathf.Max(0, options.endpointSearchRadiusCells);
        float bestDistanceSqr = float.MaxValue;
        bool found = false;

        for (int radius = 1; radius <= radiusLimit; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy), Mathf.Abs(dz)) != radius)
                        {
                            continue;
                        }

                        if (!TryOffsetCell(origin, new Vector3Int(dx, dy, dz), out VoxelCellKey candidate) ||
                            !IsPassageCell(candidate))
                        {
                            continue;
                        }

                        float distanceSqr = GetCellDistanceSqr(origin, candidate);
                        if (distanceSqr < bestDistanceSqr)
                        {
                            bestDistanceSqr = distanceSqr;
                            resolved = candidate;
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySearch(VoxelCellKey start, VoxelCellKey goal, List<Vector3> waypoints)
    {
        SearchBounds bounds = SearchBounds.Create(start, goal, voxelsPerBlock, options.searchPaddingCells);
        List<VoxelCellKey> openSet = new List<VoxelCellKey> { start };
        HashSet<VoxelCellKey> closedSet = new HashSet<VoxelCellKey>();
        Dictionary<VoxelCellKey, VoxelCellKey> cameFrom = new Dictionary<VoxelCellKey, VoxelCellKey>();
        Dictionary<VoxelCellKey, int> gScore = new Dictionary<VoxelCellKey, int>
        {
            [start] = 0
        };

        int maxVisitedCells = Mathf.Max(16, options.maxVisitedCells);
        while (openSet.Count > 0 && closedSet.Count < maxVisitedCells)
        {
            VoxelCellKey current = PopBestOpenCell(openSet, goal, gScore);
            if (current.Equals(goal))
            {
                BuildWaypoints(start, goal, cameFrom, waypoints);
                return waypoints.Count > 0;
            }

            closedSet.Add(current);

            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                if (!TryOffsetCell(current, NeighborOffsets[i], out VoxelCellKey neighbor) ||
                    closedSet.Contains(neighbor) ||
                    !bounds.Contains(neighbor, voxelsPerBlock) ||
                    !IsPassageCell(neighbor))
                {
                    continue;
                }

                int tentativeScore = gScore[current] + 10;
                if (gScore.TryGetValue(neighbor, out int existingScore) && tentativeScore >= existingScore)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeScore;
                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
            }
        }

        return false;
    }

    private VoxelCellKey PopBestOpenCell(List<VoxelCellKey> openSet, VoxelCellKey goal, Dictionary<VoxelCellKey, int> gScore)
    {
        int bestIndex = 0;
        int bestScore = int.MaxValue;
        int bestHeuristic = int.MaxValue;

        for (int i = 0; i < openSet.Count; i++)
        {
            VoxelCellKey key = openSet[i];
            int heuristic = GetManhattanDistance(key, goal);
            int score = gScore[key] + heuristic * 10;
            if (score < bestScore || (score == bestScore && heuristic < bestHeuristic))
            {
                bestIndex = i;
                bestScore = score;
                bestHeuristic = heuristic;
            }
        }

        VoxelCellKey best = openSet[bestIndex];
        openSet.RemoveAt(bestIndex);
        return best;
    }

    private void BuildWaypoints(VoxelCellKey start, VoxelCellKey goal, Dictionary<VoxelCellKey, VoxelCellKey> cameFrom, List<Vector3> waypoints)
    {
        List<VoxelCellKey> cells = new List<VoxelCellKey>();
        VoxelCellKey current = goal;
        cells.Add(current);

        while (!current.Equals(start))
        {
            if (!cameFrom.TryGetValue(current, out current))
            {
                waypoints.Clear();
                return;
            }

            cells.Add(current);
        }

        cells.Reverse();

        for (int i = 0; i < cells.Count; i++)
        {
            AppendWaypoint(waypoints, voxelManager.GetVoxelCellWorldBounds(cells[i]).center);
        }
    }

    private bool TryOffsetCell(VoxelCellKey key, Vector3Int offset, out VoxelCellKey result)
    {
        Vector3Int blockPosition = key.blockPosition;
        Vector3Int localPosition = key.localVoxelPosition + offset;
        if (!voxelManager.NormalizeVoxelPosition(ref blockPosition, ref localPosition))
        {
            result = default;
            return false;
        }

        result = new VoxelCellKey(blockPosition, localPosition);
        return true;
    }

    private bool IsPassageCell(VoxelCellKey key)
    {
        Voxel voxel = voxelManager.GetVoxelIncludingInactive(key.blockPosition, key.localVoxelPosition);
        if (voxel != null)
        {
            return !voxel.isActive;
        }

        return terrainDataManager != null && terrainDataManager.IsBlockGenerationExcluded(key.blockPosition);
    }

    private int GetManhattanDistance(VoxelCellKey a, VoxelCellKey b)
    {
        Vector3Int aGlobal = GetGlobalCellPosition(a);
        Vector3Int bGlobal = GetGlobalCellPosition(b);
        return Mathf.Abs(aGlobal.x - bGlobal.x) +
               Mathf.Abs(aGlobal.y - bGlobal.y) +
               Mathf.Abs(aGlobal.z - bGlobal.z);
    }

    private float GetCellDistanceSqr(VoxelCellKey a, VoxelCellKey b)
    {
        Vector3Int aGlobal = GetGlobalCellPosition(a);
        Vector3Int bGlobal = GetGlobalCellPosition(b);
        return (aGlobal - bGlobal).sqrMagnitude;
    }

    private Vector3Int GetGlobalCellPosition(VoxelCellKey key)
    {
        return new Vector3Int(
            key.blockPosition.x * voxelsPerBlock + key.localVoxelPosition.x,
            key.blockPosition.y * voxelsPerBlock + key.localVoxelPosition.y,
            key.blockPosition.z * voxelsPerBlock + key.localVoxelPosition.z
        );
    }

    private static void AppendWaypoint(List<Vector3> waypoints, Vector3 waypoint)
    {
        if (waypoints.Count > 0 && (waypoints[waypoints.Count - 1] - waypoint).sqrMagnitude < 0.0001f)
        {
            return;
        }

        waypoints.Add(waypoint);
    }

    private static float CalculatePathLength(Vector3 startWorldPosition, List<Vector3> waypoints)
    {
        float length = 0f;
        Vector3 previous = startWorldPosition;
        for (int i = 0; i < waypoints.Count; i++)
        {
            length += Vector3.Distance(previous, waypoints[i]);
            previous = waypoints[i];
        }

        return length;
    }

    private readonly struct SearchBounds
    {
        private readonly Vector3Int min;
        private readonly Vector3Int max;

        private SearchBounds(Vector3Int min, Vector3Int max)
        {
            this.min = min;
            this.max = max;
        }

        public static SearchBounds Create(VoxelCellKey start, VoxelCellKey goal, int voxelsPerBlock, int padding)
        {
            Vector3Int startGlobal = GetGlobalCellPosition(start, voxelsPerBlock);
            Vector3Int goalGlobal = GetGlobalCellPosition(goal, voxelsPerBlock);
            int safePadding = Mathf.Max(0, padding);
            Vector3Int paddingVector = new Vector3Int(safePadding, safePadding, safePadding);
            return new SearchBounds(Vector3Int.Min(startGlobal, goalGlobal) - paddingVector, Vector3Int.Max(startGlobal, goalGlobal) + paddingVector);
        }

        public bool Contains(VoxelCellKey key, int voxelsPerBlock)
        {
            Vector3Int global = GetGlobalCellPosition(key, voxelsPerBlock);
            return global.x >= min.x && global.x <= max.x &&
                   global.y >= min.y && global.y <= max.y &&
                   global.z >= min.z && global.z <= max.z;
        }

        private static Vector3Int GetGlobalCellPosition(VoxelCellKey key, int voxelsPerBlock)
        {
            return new Vector3Int(
                key.blockPosition.x * voxelsPerBlock + key.localVoxelPosition.x,
                key.blockPosition.y * voxelsPerBlock + key.localVoxelPosition.y,
                key.blockPosition.z * voxelsPerBlock + key.localVoxelPosition.z
            );
        }
    }
}
