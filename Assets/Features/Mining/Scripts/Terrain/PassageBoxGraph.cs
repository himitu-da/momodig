using System.Collections.Generic;
using UnityEngine;

// Builds a coarse passage graph by partitioning passable voxel cells into
// non-overlapping axis-aligned 3D boxes. Each passage cell belongs to exactly
// one box node, and adjacent boxes are connected through neighboring cells.
public sealed class PassageBoxGraph
{
    private static readonly Vector3Int[] NeighborOffsets =
    {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    private static readonly Vector3Int[] ExpansionDirections =
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
    private readonly Dictionary<int, BoxNode> nodes = new Dictionary<int, BoxNode>();
    private readonly Dictionary<VoxelCellKey, int> voxelToNode = new Dictionary<VoxelCellKey, int>();
    private readonly Dictionary<int, List<VoxelCellKey>> nodeCells = new Dictionary<int, List<VoxelCellKey>>();
    private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
    private int nextNodeId;

    public bool IsBuilt { get; private set; }
    public int NodeCount => nodes.Count;

    public PassageBoxGraph(VoxelManager voxelManager, TerrainDataManager terrainDataManager, int voxelsPerBlock)
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

        BuildBoxes();
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
                for (int n = 0; n < NeighborOffsets.Length; n++)
                {
                    if (!TryOffsetCell(current, NeighborOffsets[n], out VoxelCellKey neighbor))
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
        var blockPositions = new HashSet<Vector3Int>();
        List<BlockManager.BlockInstanceData> allBlocks = blockManager.GetAllBlocks();
        for (int b = 0; b < allBlocks.Count; b++)
        {
            blockPositions.Add(allBlocks[b].position);
        }

        if (terrainDataManager != null)
        {
            var excludedBlocks = new List<Vector3Int>();
            terrainDataManager.AppendExcludedBlockPositions(excludedBlocks);
            for (int i = 0; i < excludedBlocks.Count; i++)
            {
                blockPositions.Add(excludedBlocks[i]);
            }
        }

        foreach (Vector3Int blockPos in blockPositions)
        {
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

    private void BuildBoxes()
    {
        var unassigned = new HashSet<VoxelCellKey>(passageCells);
        while (unassigned.Count > 0)
        {
            VoxelCellKey seedCell = FindLexicographicallySmallestCell(unassigned);
            Vector3Int seedGlobal = GetGlobalPosition(seedCell);
            var bounds = new BoxBounds(seedGlobal, seedGlobal);

            while (TryFindBestExpansion(bounds, unassigned, out BoxBounds expanded))
            {
                bounds = expanded;
            }

            int nodeId = nextNodeId++;
            var cells = new List<VoxelCellKey>(bounds.Volume);
            for (int x = bounds.Min.x; x <= bounds.Max.x; x++)
            for (int y = bounds.Min.y; y <= bounds.Max.y; y++)
            for (int z = bounds.Min.z; z <= bounds.Max.z; z++)
            {
                VoxelCellKey cell = globalToCell[new Vector3Int(x, y, z)];
                unassigned.Remove(cell);
                voxelToNode[cell] = nodeId;
                cells.Add(cell);
            }

            nodes[nodeId] = new BoxNode(nodeId, bounds, seedCell);
            nodeCells[nodeId] = cells;
            adjacency[nodeId] = new List<int>();
        }
    }

    private VoxelCellKey FindLexicographicallySmallestCell(HashSet<VoxelCellKey> cells)
    {
        VoxelCellKey best = default;
        Vector3Int bestGlobal = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        bool found = false;

        foreach (VoxelCellKey cell in cells)
        {
            Vector3Int global = GetGlobalPosition(cell);
            if (!found || IsLexSmaller(global, bestGlobal))
            {
                best = cell;
                bestGlobal = global;
                found = true;
            }
        }

        return best;
    }

    private bool TryFindBestExpansion(BoxBounds bounds, HashSet<VoxelCellKey> unassigned, out BoxBounds expanded)
    {
        expanded = bounds;
        int bestAddedCells = 0;
        bool found = false;

        for (int i = 0; i < ExpansionDirections.Length; i++)
        {
            Vector3Int direction = ExpansionDirections[i];
            BoxBounds candidate = bounds.Expanded(direction);
            if (!TryCountExpansionFaceCells(bounds, candidate, direction, unassigned, out int addedCells))
                continue;

            if (!found || addedCells > bestAddedCells)
            {
                expanded = candidate;
                bestAddedCells = addedCells;
                found = true;
            }
        }

        return found;
    }

    private bool TryCountExpansionFaceCells(
        BoxBounds previous,
        BoxBounds candidate,
        Vector3Int direction,
        HashSet<VoxelCellKey> unassigned,
        out int addedCells)
    {
        addedCells = 0;

        int minX = candidate.Min.x;
        int minY = candidate.Min.y;
        int minZ = candidate.Min.z;
        int maxX = candidate.Max.x;
        int maxY = candidate.Max.y;
        int maxZ = candidate.Max.z;

        if (direction.x > 0) minX = maxX = candidate.Max.x;
        else if (direction.x < 0) minX = maxX = candidate.Min.x;
        else if (direction.y > 0) minY = maxY = candidate.Max.y;
        else if (direction.y < 0) minY = maxY = candidate.Min.y;
        else if (direction.z > 0) minZ = maxZ = candidate.Max.z;
        else if (direction.z < 0) minZ = maxZ = candidate.Min.z;

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            var global = new Vector3Int(x, y, z);
            if (previous.Contains(global))
                continue;

            if (!globalToCell.TryGetValue(global, out VoxelCellKey cell) || !unassigned.Contains(cell))
                return false;

            addedCells++;
        }

        return addedCells > 0;
    }

    private void BuildAdjacency()
    {
        var addedEdges = new HashSet<long>();
        foreach (var kvp in voxelToNode)
        {
            int nodeA = kvp.Value;
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                if (!TryOffsetCell(kvp.Key, NeighborOffsets[i], out VoxelCellKey neighbor))
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
        if (!nodes.TryGetValue(nodeId, out BoxNode node))
            return false;

        float bestDistanceSqr = float.MaxValue;
        bool found = false;
        for (int i = 0; i < targetCells.Count; i++)
        {
            if (!voxelToNode.TryGetValue(targetCells[i], out int targetNodeId) || targetNodeId != nodeId)
                continue;

            Vector3Int targetGlobal = GetGlobalPosition(targetCells[i]);
            float distanceSqr = (node.Bounds.Center - targetGlobal).sqrMagnitude;
            if (!found || distanceSqr < bestDistanceSqr || (Mathf.Approximately(distanceSqr, bestDistanceSqr) && IsLexSmaller(targetGlobal, GetGlobalPosition(selectedTargetCell))))
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
        if (!nodes.TryGetValue(nodeId, out BoxNode node))
            return float.MaxValue;

        float minDist = float.MaxValue;
        foreach (int targetId in targets)
        {
            if (!nodes.TryGetValue(targetId, out BoxNode target))
                continue;
            float d = Vector3.Distance(node.Bounds.Center, target.Bounds.Center);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private float GetNodeDistance(int idA, int idB)
    {
        if (!nodes.TryGetValue(idA, out BoxNode a) || !nodes.TryGetValue(idB, out BoxNode b))
            return float.MaxValue;
        return Vector3.Distance(a.Bounds.Center, b.Bounds.Center);
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

        foreach (var kvp in nodes)
        {
            BoxNode node = kvp.Value;
            if (!TryGetWorldBounds(node.Bounds, out Bounds worldBounds))
                continue;

            float t = Mathf.Clamp01(node.Bounds.Volume / 512f);
            Gizmos.color = Color.Lerp(
                new Color(0.4f, 0.6f, 1f, 0.45f),
                new Color(0.2f, 1f, 0.4f, 0.45f),
                t
            );
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

            if (!showEdges || !adjacency.TryGetValue(kvp.Key, out List<int> neighbors))
                continue;

            Gizmos.color = new Color(1f, 1f, 0.2f, 0.35f);
            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighborId = neighbors[i];
                if (neighborId <= kvp.Key) continue;
                if (!nodes.TryGetValue(neighborId, out BoxNode neighborNode)) continue;
                if (!TryGetWorldBounds(neighborNode.Bounds, out Bounds neighborBounds)) continue;
                Gizmos.DrawLine(worldBounds.center, neighborBounds.center);
            }
        }
    }

    private bool TryGetWorldBounds(BoxBounds bounds, out Bounds worldBounds)
    {
        worldBounds = default;
        if (!globalToCell.TryGetValue(bounds.Min, out VoxelCellKey minCell) ||
            !globalToCell.TryGetValue(bounds.Max, out VoxelCellKey maxCell))
        {
            return false;
        }

        Bounds minWorld = voxelManager.GetVoxelCellWorldBounds(minCell);
        Bounds maxWorld = voxelManager.GetVoxelCellWorldBounds(maxCell);
        Vector3 min = minWorld.min;
        Vector3 max = maxWorld.max;
        worldBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private static bool IsLexSmaller(Vector3Int a, Vector3Int b)
    {
        if (a.x != b.x) return a.x < b.x;
        if (a.y != b.y) return a.y < b.y;
        return a.z < b.z;
    }

    private readonly struct BoxBounds
    {
        public readonly Vector3Int Min;
        public readonly Vector3Int Max;

        public BoxBounds(Vector3Int min, Vector3Int max)
        {
            Min = min;
            Max = max;
        }

        public int Volume => (Max.x - Min.x + 1) * (Max.y - Min.y + 1) * (Max.z - Min.z + 1);

        public Vector3 Center => new Vector3(
            (Min.x + Max.x) * 0.5f,
            (Min.y + Max.y) * 0.5f,
            (Min.z + Max.z) * 0.5f
        );

        public bool Contains(Vector3Int global)
        {
            return global.x >= Min.x && global.x <= Max.x &&
                   global.y >= Min.y && global.y <= Max.y &&
                   global.z >= Min.z && global.z <= Max.z;
        }

        public BoxBounds Expanded(Vector3Int direction)
        {
            Vector3Int min = Min;
            Vector3Int max = Max;
            if (direction.x > 0) max.x++;
            else if (direction.x < 0) min.x--;
            else if (direction.y > 0) max.y++;
            else if (direction.y < 0) min.y--;
            else if (direction.z > 0) max.z++;
            else if (direction.z < 0) min.z--;
            return new BoxBounds(min, max);
        }
    }

    private readonly struct BoxNode
    {
        public readonly int NodeId;
        public readonly BoxBounds Bounds;
        public readonly VoxelCellKey SeedCell;

        public BoxNode(int nodeId, BoxBounds bounds, VoxelCellKey seedCell)
        {
            NodeId = nodeId;
            Bounds = bounds;
            SeedCell = seedCell;
        }
    }
}
