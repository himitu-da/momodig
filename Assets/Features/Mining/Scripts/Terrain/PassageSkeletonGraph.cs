using System.Collections.Generic;
using UnityEngine;

// Builds a coarse passage graph by covering each Z layer with XY circles.
// Nodes are selected from the largest available circles first, and every
// passage cell is assigned to the nearest selected circle that contains it.
public sealed class PassageCircleGraph
{
    private const int DistanceTransformInfinity = 1000000000;

    private static readonly Vector3Int[] RegionNeighborOffsets =
    {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    private readonly VoxelManager voxelManager;
    private readonly TerrainDataManager terrainDataManager;
    private readonly int voxelsPerBlock;

    private readonly HashSet<VoxelCellKey> passageCells = new HashSet<VoxelCellKey>();
    private readonly Dictionary<Vector3Int, VoxelCellKey> globalToCell = new Dictionary<Vector3Int, VoxelCellKey>();
    private readonly Dictionary<int, CircleNode> nodes = new Dictionary<int, CircleNode>();
    private readonly Dictionary<VoxelCellKey, int> voxelToNode = new Dictionary<VoxelCellKey, int>();
    private readonly Dictionary<int, List<VoxelCellKey>> nodeCells = new Dictionary<int, List<VoxelCellKey>>();
    private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
    private int nextNodeId;

    public bool IsBuilt { get; private set; }
    public int NodeCount => nodes.Count;

    public PassageCircleGraph(VoxelManager voxelManager, TerrainDataManager terrainDataManager, int voxelsPerBlock)
    {
        this.voxelManager = voxelManager;
        this.terrainDataManager = terrainDataManager;
        this.voxelsPerBlock = Mathf.Max(1, voxelsPerBlock);
    }

    public void Invalidate()
    {
        IsBuilt = false;
    }

    public void Build(BlockManager blockManager)
    {
        Clear();
        if (voxelManager == null || blockManager == null)
            return;

        EnumeratePassageCells(blockManager);
        if (passageCells.Count == 0)
            return;

        List<CircleCandidate> candidates = BuildCircleCandidates();
        SelectCoveringCircles(candidates);
        AssignCellsToNearestCircle();
        BuildAdjacency();
        IsBuilt = nodes.Count > 0 && voxelToNode.Count == passageCells.Count;
    }

    public bool TryGetNodeForCell(VoxelCellKey cell, out int nodeId)
    {
        return voxelToNode.TryGetValue(cell, out nodeId);
    }

    public bool TryFindPathToNearestTarget(
        int startNodeId,
        IReadOnlyList<VoxelCellKey> targetCells,
        List<int> outNodePath,
        out VoxelCellKey selectedTargetCell)
    {
        selectedTargetCell = default;
        outNodePath.Clear();
        if (!IsBuilt || !nodes.ContainsKey(startNodeId) || targetCells == null || targetCells.Count == 0)
            return false;

        var targetNodeIds = new HashSet<int>();
        for (int i = 0; i < targetCells.Count; i++)
        {
            if (voxelToNode.TryGetValue(targetCells[i], out int nodeId))
                targetNodeIds.Add(nodeId);
        }

        if (targetNodeIds.Count == 0)
            return false;

        int reachedTargetNodeId;
        if (targetNodeIds.Contains(startNodeId))
        {
            reachedTargetNodeId = startNodeId;
            outNodePath.Add(startNodeId);
        }
        else if (!RunNodeGraphAStar(startNodeId, targetNodeIds, outNodePath, out reachedTargetNodeId))
        {
            return false;
        }

        return TrySelectTargetCellForNode(reachedTargetNodeId, targetCells, out selectedTargetCell);
    }

    public bool TryBuildPathRegion(
        List<int> nodePath,
        int extraPadding,
        VoxelCellKey start,
        VoxelCellKey target,
        HashSet<VoxelCellKey> outRegion)
    {
        outRegion.Clear();
        if (!IsBuilt || nodePath == null || nodePath.Count == 0)
            return false;

        var queue = new Queue<VoxelCellKey>();
        for (int i = 0; i < nodePath.Count; i++)
        {
            if (!nodeCells.TryGetValue(nodePath[i], out List<VoxelCellKey> cells))
                continue;

            for (int c = 0; c < cells.Count; c++)
            {
                if (outRegion.Add(cells[c]))
                    queue.Enqueue(cells[c]);
            }
        }

        if (outRegion.Add(start))
            queue.Enqueue(start);
        if (outRegion.Add(target))
            queue.Enqueue(target);

        int padding = Mathf.Max(0, extraPadding);
        for (int step = 0; step < padding; step++)
        {
            int count = queue.Count;
            if (count == 0)
                break;

            for (int i = 0; i < count; i++)
            {
                VoxelCellKey current = queue.Dequeue();
                for (int n = 0; n < RegionNeighborOffsets.Length; n++)
                {
                    if (!TryOffsetCell(current, RegionNeighborOffsets[n], out VoxelCellKey neighbor))
                        continue;
                    if (!IsPassageCell(neighbor))
                        continue;
                    if (outRegion.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }
        }

        return outRegion.Count > 0 && outRegion.Contains(start) && outRegion.Contains(target);
    }

    private void Clear()
    {
        passageCells.Clear();
        globalToCell.Clear();
        nodes.Clear();
        voxelToNode.Clear();
        nodeCells.Clear();
        adjacency.Clear();
        nextNodeId = 0;
        IsBuilt = false;
    }

    private void EnumeratePassageCells(BlockManager blockManager)
    {
        List<BlockManager.BlockInstanceData> allBlocks = blockManager.GetAllBlocks();
        for (int b = 0; b < allBlocks.Count; b++)
        {
            Vector3Int blockPos = allBlocks[b].position;
            for (int lx = 0; lx < voxelsPerBlock; lx++)
            for (int ly = 0; ly < voxelsPerBlock; ly++)
            for (int lz = 0; lz < voxelsPerBlock; lz++)
            {
                var cell = new VoxelCellKey(blockPos, new Vector3Int(lx, ly, lz));
                if (!IsPassageCell(cell))
                    continue;

                passageCells.Add(cell);
                globalToCell[GetGlobalPosition(cell)] = cell;
            }
        }
    }

    private List<CircleCandidate> BuildCircleCandidates()
    {
        var cellsByZ = new Dictionary<int, List<VoxelCellKey>>();
        foreach (VoxelCellKey cell in passageCells)
        {
            int z = GetGlobalPosition(cell).z;
            if (!cellsByZ.TryGetValue(z, out List<VoxelCellKey> cells))
            {
                cells = new List<VoxelCellKey>();
                cellsByZ[z] = cells;
            }
            cells.Add(cell);
        }

        var candidates = new List<CircleCandidate>(passageCells.Count);
        foreach (var layer in cellsByZ)
        {
            BuildLayerCircleCandidates(layer.Value, candidates);
        }

        candidates.Sort(CompareCandidates);
        return candidates;
    }

    private void BuildLayerCircleCandidates(List<VoxelCellKey> layerCells, List<CircleCandidate> candidates)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int z = 0;

        var layerPositions = new HashSet<Vector2Int>();
        for (int i = 0; i < layerCells.Count; i++)
        {
            Vector3Int global = GetGlobalPosition(layerCells[i]);
            z = global.z;
            minX = Mathf.Min(minX, global.x);
            minY = Mathf.Min(minY, global.y);
            maxX = Mathf.Max(maxX, global.x);
            maxY = Mathf.Max(maxY, global.y);
            layerPositions.Add(new Vector2Int(global.x, global.y));
        }

        int originX = minX - 1;
        int originY = minY - 1;
        int width = maxX - minX + 3;
        int height = maxY - minY + 3;
        int[,] source = new int[width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var pos = new Vector2Int(originX + x, originY + y);
            source[x, y] = layerPositions.Contains(pos) ? DistanceTransformInfinity : 0;
        }

        int[,] distSq = RunDistanceTransform(source, width, height);
        for (int i = 0; i < layerCells.Count; i++)
        {
            Vector3Int global = GetGlobalPosition(layerCells[i]);
            int gx = global.x - originX;
            int gy = global.y - originY;
            int radius = Mathf.Max(0, FloorSqrt(distSq[gx, gy]) - 1);
            candidates.Add(new CircleCandidate(layerCells[i], global, radius));
        }
    }

    private static int[,] RunDistanceTransform(int[,] source, int width, int height)
    {
        int[,] vertical = new int[width, height];
        int[] input = new int[Mathf.Max(width, height)];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                input[y] = source[x, y];
            int[] result = DistanceTransform1D(input, height);
            for (int y = 0; y < height; y++)
                vertical[x, y] = result[y];
        }

        int[,] output = new int[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                input[x] = vertical[x, y];
            int[] result = DistanceTransform1D(input, width);
            for (int x = 0; x < width; x++)
                output[x, y] = result[x];
        }

        return output;
    }

    private static int[] DistanceTransform1D(int[] values, int length)
    {
        int[] distances = new int[length];
        int[] locations = new int[length];
        float[] boundaries = new float[length + 1];

        int k = 0;
        locations[0] = 0;
        boundaries[0] = float.NegativeInfinity;
        boundaries[1] = float.PositiveInfinity;

        for (int q = 1; q < length; q++)
        {
            float s = Intersection(values, q, locations[k]);
            while (s <= boundaries[k])
            {
                k--;
                s = Intersection(values, q, locations[k]);
            }

            k++;
            locations[k] = q;
            boundaries[k] = s;
            boundaries[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (int q = 0; q < length; q++)
        {
            while (boundaries[k + 1] < q)
                k++;

            int diff = q - locations[k];
            distances[q] = diff * diff + values[locations[k]];
        }

        return distances;
    }

    private static float Intersection(int[] values, int q, int v)
    {
        return ((values[q] + q * q) - (values[v] + v * v)) / (2f * q - 2f * v);
    }

    private void SelectCoveringCircles(List<CircleCandidate> candidates)
    {
        var covered = new HashSet<VoxelCellKey>();
        for (int i = 0; i < candidates.Count; i++)
        {
            CircleCandidate candidate = candidates[i];
            if (!CircleContainsUncoveredPassageCell(candidate.GlobalPosition, candidate.Radius, covered))
                continue;

            int nodeId = nextNodeId++;
            nodes[nodeId] = new CircleNode(nodeId, candidate.Center, candidate.Radius, candidate.GlobalPosition);
            nodeCells[nodeId] = new List<VoxelCellKey>();
            adjacency[nodeId] = new List<int>();
            MarkCircleCovered(candidate.GlobalPosition, candidate.Radius, covered);
        }
    }

    private bool CircleContainsUncoveredPassageCell(Vector3Int center, int radius, HashSet<VoxelCellKey> covered)
    {
        int radiusSqr = radius * radius;
        for (int dx = -radius; dx <= radius; dx++)
        {
            int maxDy = FloorSqrt(radiusSqr - dx * dx);
            for (int dy = -maxDy; dy <= maxDy; dy++)
            {
                var global = new Vector3Int(center.x + dx, center.y + dy, center.z);
                if (!globalToCell.TryGetValue(global, out VoxelCellKey cell))
                    continue;
                if (!covered.Contains(cell))
                    return true;
            }
        }

        return false;
    }

    private void MarkCircleCovered(Vector3Int center, int radius, HashSet<VoxelCellKey> covered)
    {
        int radiusSqr = radius * radius;
        for (int dx = -radius; dx <= radius; dx++)
        {
            int maxDy = FloorSqrt(radiusSqr - dx * dx);
            for (int dy = -maxDy; dy <= maxDy; dy++)
            {
                var global = new Vector3Int(center.x + dx, center.y + dy, center.z);
                if (globalToCell.TryGetValue(global, out VoxelCellKey cell))
                    covered.Add(cell);
            }
        }
    }

    private void AssignCellsToNearestCircle()
    {
        var assignments = new Dictionary<VoxelCellKey, Assignment>();
        foreach (var kvp in nodes)
        {
            CircleNode node = kvp.Value;
            int radiusSqr = node.Radius * node.Radius;
            for (int dx = -node.Radius; dx <= node.Radius; dx++)
            {
                int maxDy = FloorSqrt(radiusSqr - dx * dx);
                for (int dy = -maxDy; dy <= maxDy; dy++)
                {
                    var global = new Vector3Int(node.GlobalPosition.x + dx, node.GlobalPosition.y + dy, node.GlobalPosition.z);
                    if (!globalToCell.TryGetValue(global, out VoxelCellKey cell))
                        continue;

                    int distanceSqr = dx * dx + dy * dy;
                    var assignment = new Assignment(node.NodeId, distanceSqr, node.Radius, node.GlobalPosition);
                    if (!assignments.TryGetValue(cell, out Assignment current) || IsBetterAssignment(assignment, current))
                        assignments[cell] = assignment;
                }
            }
        }

        foreach (VoxelCellKey cell in passageCells)
        {
            if (!assignments.TryGetValue(cell, out Assignment assignment))
            {
                Vector3Int global = GetGlobalPosition(cell);
                int nodeId = nextNodeId++;
                nodes[nodeId] = new CircleNode(nodeId, cell, 0, global);
                nodeCells[nodeId] = new List<VoxelCellKey>();
                adjacency[nodeId] = new List<int>();
                assignment = new Assignment(nodeId, 0, 0, global);
            }

            voxelToNode[cell] = assignment.NodeId;
            nodeCells[assignment.NodeId].Add(cell);
        }
    }

    private static bool IsBetterAssignment(Assignment candidate, Assignment current)
    {
        if (candidate.DistanceSqr != current.DistanceSqr)
            return candidate.DistanceSqr < current.DistanceSqr;
        if (candidate.Radius != current.Radius)
            return candidate.Radius > current.Radius;
        return IsLexSmaller(candidate.CenterGlobal, current.CenterGlobal);
    }

    private void BuildAdjacency()
    {
        var addedEdges = new HashSet<long>();
        foreach (var kvp in voxelToNode)
        {
            int nodeA = kvp.Value;
            for (int i = 0; i < RegionNeighborOffsets.Length; i++)
            {
                if (!TryOffsetCell(kvp.Key, RegionNeighborOffsets[i], out VoxelCellKey neighbor))
                    continue;
                if (!voxelToNode.TryGetValue(neighbor, out int nodeB) || nodeA == nodeB)
                    continue;

                int lo = nodeA < nodeB ? nodeA : nodeB;
                int hi = nodeA < nodeB ? nodeB : nodeA;
                long edgeKey = ((long)lo << 32) | (uint)hi;
                if (!addedEdges.Add(edgeKey))
                    continue;

                adjacency[nodeA].Add(nodeB);
                adjacency[nodeB].Add(nodeA);
            }
        }
    }

    private bool RunNodeGraphAStar(int startNodeId, HashSet<int> targetNodeIds, List<int> outPath, out int reachedTargetNodeId)
    {
        reachedTargetNodeId = -1;
        var openSet = new List<int> { startNodeId };
        var gScore = new Dictionary<int, float> { [startNodeId] = 0f };
        var cameFrom = new Dictionary<int, int>();
        var closedSet = new HashSet<int>();

        while (openSet.Count > 0)
        {
            int current = PopBestNode(openSet, gScore, targetNodeIds);
            if (!closedSet.Add(current))
                continue;

            if (targetNodeIds.Contains(current))
            {
                reachedTargetNodeId = current;
                ReconstructPath(current, cameFrom, outPath);
                return true;
            }

            if (!adjacency.TryGetValue(current, out List<int> neighbors))
                continue;

            float currentG = gScore[current];
            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighbor = neighbors[i];
                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeG = currentG + GetNodeDistance(current, neighbor);
                if (gScore.TryGetValue(neighbor, out float existingG) && tentativeG >= existingG)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
            }
        }

        return false;
    }

    private bool TrySelectTargetCellForNode(int nodeId, IReadOnlyList<VoxelCellKey> targetCells, out VoxelCellKey selectedTargetCell)
    {
        selectedTargetCell = default;
        if (!nodes.TryGetValue(nodeId, out CircleNode node))
            return false;

        int bestDistanceSqr = int.MaxValue;
        bool found = false;
        for (int i = 0; i < targetCells.Count; i++)
        {
            if (!voxelToNode.TryGetValue(targetCells[i], out int targetNodeId) || targetNodeId != nodeId)
                continue;

            Vector3Int targetGlobal = GetGlobalPosition(targetCells[i]);
            int distanceSqr = GetGlobalDistanceSqr(node.GlobalPosition, targetGlobal);
            if (!found || distanceSqr < bestDistanceSqr || (distanceSqr == bestDistanceSqr && IsLexSmaller(targetGlobal, GetGlobalPosition(selectedTargetCell))))
            {
                selectedTargetCell = targetCells[i];
                bestDistanceSqr = distanceSqr;
                found = true;
            }
        }

        return found;
    }

    private int PopBestNode(List<int> openSet, Dictionary<int, float> gScore, HashSet<int> targets)
    {
        int bestIdx = 0;
        float bestF = float.MaxValue;

        for (int i = 0; i < openSet.Count; i++)
        {
            int id = openSet[i];
            float g = gScore.TryGetValue(id, out float gv) ? gv : float.MaxValue;
            float h = MinHeuristicToTargets(id, targets);
            float f = g + h;
            if (f < bestF) { bestF = f; bestIdx = i; }
        }

        int best = openSet[bestIdx];
        openSet.RemoveAt(bestIdx);
        return best;
    }

    private float MinHeuristicToTargets(int nodeId, HashSet<int> targets)
    {
        if (!nodes.TryGetValue(nodeId, out CircleNode node))
            return float.MaxValue;

        float minDist = float.MaxValue;
        foreach (int targetId in targets)
        {
            if (!nodes.TryGetValue(targetId, out CircleNode target))
                continue;
            float d = Mathf.Sqrt(GetGlobalDistanceSqr(node.GlobalPosition, target.GlobalPosition));
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private float GetNodeDistance(int idA, int idB)
    {
        if (!nodes.TryGetValue(idA, out CircleNode a) || !nodes.TryGetValue(idB, out CircleNode b))
            return float.MaxValue;
        return Mathf.Sqrt(GetGlobalDistanceSqr(a.GlobalPosition, b.GlobalPosition));
    }

    private static int GetGlobalDistanceSqr(Vector3Int a, Vector3Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        int dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    private static void ReconstructPath(int goalId, Dictionary<int, int> cameFrom, List<int> outPath)
    {
        int current = goalId;
        outPath.Add(current);
        while (cameFrom.TryGetValue(current, out int prev))
        {
            outPath.Add(prev);
            current = prev;
        }
        outPath.Reverse();
    }

    private bool IsPassageCell(VoxelCellKey key)
    {
        Voxel voxel = voxelManager.GetVoxelIncludingInactive(key.blockPosition, key.localVoxelPosition);
        if (voxel != null)
            return !voxel.isActive;
        return terrainDataManager != null && terrainDataManager.IsBlockGenerationExcluded(key.blockPosition);
    }

    private bool TryOffsetCell(VoxelCellKey key, Vector3Int offset, out VoxelCellKey result)
    {
        Vector3Int blockPos = key.blockPosition;
        Vector3Int localPos = key.localVoxelPosition + offset;
        if (!voxelManager.NormalizeVoxelPosition(ref blockPos, ref localPos))
        {
            result = default;
            return false;
        }
        result = new VoxelCellKey(blockPos, localPos);
        return true;
    }

    private Vector3Int GetGlobalPosition(VoxelCellKey key)
    {
        return new Vector3Int(
            key.blockPosition.x * voxelsPerBlock + key.localVoxelPosition.x,
            key.blockPosition.y * voxelsPerBlock + key.localVoxelPosition.y,
            key.blockPosition.z * voxelsPerBlock + key.localVoxelPosition.z
        );
    }

    public void DrawGizmos(bool showEdges)
    {
        if (!IsBuilt || voxelManager == null || nodes.Count == 0)
            return;

        float voxelWorldSize = 1f;
        foreach (var first in nodes)
        {
            voxelWorldSize = voxelManager.GetVoxelCellWorldBounds(first.Value.Center).size.x;
            break;
        }

        foreach (var kvp in nodes)
        {
            CircleNode node = kvp.Value;
            Vector3 worldPos = voxelManager.GetVoxelCellWorldBounds(node.Center).center;
            float worldRadius = Mathf.Max(0.35f, node.Radius + 0.5f) * voxelWorldSize;

            float t = Mathf.Clamp01(node.Radius / 20f);
            Gizmos.color = Color.Lerp(
                new Color(0.4f, 0.6f, 1f, 0.5f),
                new Color(0.2f, 1f, 0.4f, 0.5f),
                t
            );
            DrawWireCircleXY(worldPos, worldRadius);

            if (!showEdges || !adjacency.TryGetValue(kvp.Key, out List<int> neighbors))
                continue;

            Gizmos.color = new Color(1f, 1f, 0.2f, 0.35f);
            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighborId = neighbors[i];
                if (neighborId <= kvp.Key) continue;
                if (!nodes.TryGetValue(neighborId, out CircleNode neighborNode)) continue;
                Vector3 neighborWorldPos = voxelManager.GetVoxelCellWorldBounds(neighborNode.Center).center;
                Gizmos.DrawLine(worldPos, neighborWorldPos);
            }
        }
    }

    private static void DrawWireCircleXY(Vector3 center, float radius)
    {
        const int segments = 24;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 current = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    private static int CompareCandidates(CircleCandidate a, CircleCandidate b)
    {
        int radiusCompare = b.Radius.CompareTo(a.Radius);
        if (radiusCompare != 0)
            return radiusCompare;
        if (a.GlobalPosition.x != b.GlobalPosition.x)
            return a.GlobalPosition.x.CompareTo(b.GlobalPosition.x);
        if (a.GlobalPosition.y != b.GlobalPosition.y)
            return a.GlobalPosition.y.CompareTo(b.GlobalPosition.y);
        return a.GlobalPosition.z.CompareTo(b.GlobalPosition.z);
    }

    private static bool IsLexSmaller(Vector3Int a, Vector3Int b)
    {
        if (a.x != b.x) return a.x < b.x;
        if (a.y != b.y) return a.y < b.y;
        return a.z < b.z;
    }

    private static int FloorSqrt(int value)
    {
        return value <= 0 ? 0 : Mathf.FloorToInt(Mathf.Sqrt(value));
    }

    private readonly struct CircleCandidate
    {
        public readonly VoxelCellKey Center;
        public readonly Vector3Int GlobalPosition;
        public readonly int Radius;

        public CircleCandidate(VoxelCellKey center, Vector3Int globalPosition, int radius)
        {
            Center = center;
            GlobalPosition = globalPosition;
            Radius = radius;
        }
    }

    private readonly struct Assignment
    {
        public readonly int NodeId;
        public readonly int DistanceSqr;
        public readonly int Radius;
        public readonly Vector3Int CenterGlobal;

        public Assignment(int nodeId, int distanceSqr, int radius, Vector3Int centerGlobal)
        {
            NodeId = nodeId;
            DistanceSqr = distanceSqr;
            Radius = radius;
            CenterGlobal = centerGlobal;
        }
    }

    private readonly struct CircleNode
    {
        public readonly int NodeId;
        public readonly VoxelCellKey Center;
        public readonly int Radius;
        public readonly Vector3Int GlobalPosition;

        public CircleNode(int nodeId, VoxelCellKey center, int radius, Vector3Int globalPosition)
        {
            NodeId = nodeId;
            Center = center;
            Radius = radius;
            GlobalPosition = globalPosition;
        }
    }
}
