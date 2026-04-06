using System.Collections.Generic;
using UnityEngine;

public enum FluidGravityAxis
{
    NegativeX,
    NegativeY,
    NegativeZ
}

public struct FluidCellSnapshot
{
    public FluidCellSnapshot(Vector3Int cellPosition, float liters, FluidDefinition definition)
    {
        CellPosition = cellPosition;
        Liters = liters;
        Definition = definition;
    }

    public Vector3Int CellPosition { get; }
    public float Liters { get; }
    public FluidDefinition Definition { get; }
}

public class FluidSimulation : MonoBehaviour
{
    private const float MinLitersEpsilon = 0.0001f;
    private const int MaxFillSearchDepth = 32;

    [Header("References")]
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private FluidDefinition defaultFluidDefinition;

    [Header("Grid")]
    [SerializeField] private Vector3 simulationOriginOffset = Vector3.zero;
    [SerializeField] private float metersPerUnit = 1.0f;
    [SerializeField] private int internalVoxelsPerBlock = 8;
    [SerializeField] private int renderVoxelsPerBlock = 2;
    [SerializeField] private FluidGravityAxis gravityAxis = FluidGravityAxis.NegativeY;
    [SerializeField] private bool injectIntoLowestReachableCell = false;

    [Header("Simulation")]
    [SerializeField] private float simulationTickInterval = 0.05f;
    [SerializeField] private int maxCellsPerStep = 2048;
    [SerializeField] private int fullSolveCellThreshold = 8192;
    [SerializeField] private float flowRateMultiplier = 4f;
    [SerializeField] private float generationSliceHalfThickness = 0.5f;
    [SerializeField] private int maxVerticalCascadeSteps = 12;
    [SerializeField] private int maxVelocityCascadeSteps = 12;
    [SerializeField] [Range(0f, 1f)] private float velocityTransferRetention = 0.2f;
    [SerializeField] private bool useDynamicObstacleLayers = true;
    [SerializeField] private LayerMask dynamicObstacleLayers;
    [SerializeField] private bool showDebugLogs = false;

    private readonly Dictionary<Vector3Int, FluidCellState> cells = new Dictionary<Vector3Int, FluidCellState>();
    private readonly HashSet<Vector3Int> queuedCells = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> processingBuffer = new List<Vector3Int>();
    private readonly Dictionary<Vector3Int, bool> dynamicObstacleCache = new Dictionary<Vector3Int, bool>();
    private readonly Queue<FluidImpulse> pendingImpulses = new Queue<FluidImpulse>();

    private float tickTimer;

    public TerrainManager TerrainManager => terrainManager;
    public FluidDefinition DefaultFluidDefinition => defaultFluidDefinition;
    public Vector3 SimulationOrigin => GetAlignedSimulationOrigin();
    public float InternalVoxelSize => GetBlockSize() / Mathf.Max(1, internalVoxelsPerBlock);
    public float RenderVoxelSize => GetBlockSize() / Mathf.Max(1, renderVoxelsPerBlock);
    public int InternalVoxelsPerBlock => internalVoxelsPerBlock;
    public int RenderVoxelsPerBlock => renderVoxelsPerBlock;
    public float SimulationTickInterval => simulationTickInterval;
    public FluidGravityAxis GravityAxis => gravityAxis;
    public int RenderToInternalRatio => Mathf.Max(1, internalVoxelsPerBlock / Mathf.Max(1, renderVoxelsPerBlock));
    public float InternalCellCapacityLiters => Mathf.Pow(InternalVoxelSize * metersPerUnit, 3f) * 1000f;
    public float RenderCellCapacityLiters => InternalCellCapacityLiters * Mathf.Pow(RenderToInternalRatio, 3f);
    public int Version { get; private set; }

    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;
        OnValidate();
    }

    void OnValidate()
    {
        metersPerUnit = Mathf.Max(0.001f, metersPerUnit);
        internalVoxelsPerBlock = Mathf.Max(1, internalVoxelsPerBlock);
        renderVoxelsPerBlock = Mathf.Clamp(renderVoxelsPerBlock, 1, internalVoxelsPerBlock);
        while (internalVoxelsPerBlock % renderVoxelsPerBlock != 0 && renderVoxelsPerBlock > 1)
        {
            renderVoxelsPerBlock--;
        }
        simulationTickInterval = Mathf.Max(0.01f, simulationTickInterval);
        maxCellsPerStep = Mathf.Max(16, maxCellsPerStep);
        fullSolveCellThreshold = Mathf.Max(maxCellsPerStep, fullSolveCellThreshold);
        flowRateMultiplier = Mathf.Max(0.1f, flowRateMultiplier);
        generationSliceHalfThickness = Mathf.Max(0.01f, generationSliceHalfThickness);
        maxVerticalCascadeSteps = Mathf.Max(1, maxVerticalCascadeSteps);
        maxVelocityCascadeSteps = Mathf.Max(1, maxVelocityCascadeSteps);
        velocityTransferRetention = Mathf.Clamp01(velocityTransferRetention);
    }

    private float GetBlockSize()
    {
        if (terrainManager != null && terrainManager.Settings != null && terrainManager.Settings.blockSize > 0f)
        {
            return terrainManager.Settings.blockSize;
        }

        return 1f;
    }

    private Vector3 GetTerrainCenter()
    {
        if (terrainManager != null && terrainManager.Settings != null)
        {
            Vector3Int center = terrainManager.Settings.center;
            return new Vector3(center.x, center.y, center.z);
        }

        return Vector3.zero;
    }

    private Vector3 GetAlignedSimulationOrigin()
    {
        return GetTerrainCenter() + simulationOriginOffset - Vector3.one * (GetBlockSize() * 0.5f);
    }

    void Update()
    {
        tickTimer += Time.deltaTime;
        while (tickTimer >= simulationTickInterval)
        {
            tickTimer -= simulationTickInterval;
            StepSimulation(simulationTickInterval);
        }
    }

    public void ClearFluid()
    {
        if (cells.Count == 0 && queuedCells.Count == 0)
        {
            return;
        }

        cells.Clear();
        queuedCells.Clear();
        processingBuffer.Clear();
        pendingImpulses.Clear();
        dynamicObstacleCache.Clear();
        MarkSimulationChanged();
    }

    public void GetFluidCellSnapshots(List<FluidCellSnapshot> output)
    {
        if (output == null)
        {
            return;
        }

        output.Clear();
        foreach (var pair in cells)
        {
            if (pair.Value.Liters <= MinLitersEpsilon || pair.Value.Definition == null)
            {
                continue;
            }

            output.Add(new FluidCellSnapshot(pair.Key, pair.Value.Liters, pair.Value.Definition));
        }
    }

    public float AddFluidAtWorldPosition(Vector3 worldPosition, float liters, FluidDefinition fluidDefinition = null)
    {
        if (liters <= MinLitersEpsilon)
        {
            return 0f;
        }

        FluidDefinition definition = fluidDefinition != null ? fluidDefinition : defaultFluidDefinition;
        if (definition == null)
        {
            Debug.LogWarning("FluidSimulation: No FluidDefinition is assigned.");
            return 0f;
        }

        Vector3Int startCell = WorldToInternalCell(worldPosition);
        if (injectIntoLowestReachableCell)
        {
            startCell = FindLowestReachableCell(startCell, MaxFillSearchDepth, definition);
        }
        float accepted = AddFluidBreadthFirst(startCell, liters, definition);
        if (accepted > MinLitersEpsilon)
        {
            MarkSimulationChanged();
        }

        return accepted;
    }

    public float ExtractFluidAtWorldPosition(Vector3 worldPosition, float liters, FluidDefinition preferredDefinition = null, int searchRadius = 2)
    {
        if (liters <= MinLitersEpsilon)
        {
            return 0f;
        }

        Vector3Int startCell = WorldToInternalCell(worldPosition);
        Queue<Vector3Int> searchQueue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        searchQueue.Enqueue(startCell);
        visited.Add(startCell);

        float removedTotal = 0f;
        while (searchQueue.Count > 0 && removedTotal + MinLitersEpsilon < liters)
        {
            Vector3Int current = searchQueue.Dequeue();
            if (cells.TryGetValue(current, out FluidCellState cell) && cell.Liters > MinLitersEpsilon)
            {
                if (preferredDefinition == null || preferredDefinition == cell.Definition)
                {
                    float removed = Mathf.Min(liters - removedTotal, cell.Liters);
                    cell.Liters -= removed;
                    removedTotal += removed;

                    if (cell.Liters <= MinLitersEpsilon)
                    {
                        cells.Remove(current);
                    }

                    QueueCellNeighborhood(current, 1);
                }
            }

            if (Mathf.Abs(current.x - startCell.x) >= searchRadius ||
                Mathf.Abs(current.y - startCell.y) >= searchRadius ||
                Mathf.Abs(current.z - startCell.z) >= searchRadius)
            {
                continue;
            }

            foreach (Vector3Int direction in GetAllNeighborDirections())
            {
                Vector3Int next = current + direction;
                if (visited.Add(next))
                {
                    searchQueue.Enqueue(next);
                }
            }
        }

        if (removedTotal > MinLitersEpsilon)
        {
            MarkSimulationChanged();
        }

        return removedTotal;
    }

    public float GetFluidAmountAtWorldPosition(Vector3 worldPosition, FluidDefinition preferredDefinition = null)
    {
        Vector3Int cellPos = WorldToInternalCell(worldPosition);
        if (!cells.TryGetValue(cellPos, out FluidCellState cell))
        {
            return 0f;
        }

        if (preferredDefinition != null && cell.Definition != preferredDefinition)
        {
            return 0f;
        }

        return cell.Liters;
    }

    public float GetFluidFillRatioAtWorldPosition(Vector3 worldPosition, FluidDefinition preferredDefinition = null)
    {
        float liters = GetFluidAmountAtWorldPosition(worldPosition, preferredDefinition);
        if (liters <= MinLitersEpsilon)
        {
            return 0f;
        }

        return Mathf.Clamp01(liters / Mathf.Max(0.0001f, InternalCellCapacityLiters));
    }

    public void NotifySolidVoxelRemoved(Vector3 worldPosition)
    {
        QueueCellNeighborhood(WorldToInternalCell(worldPosition), 2);
    }

    public void MarkDirtyAroundWorldPosition(Vector3 worldPosition, int radius = 1)
    {
        QueueCellNeighborhood(WorldToInternalCell(worldPosition), radius);
    }

    public void QueueExplosion(Vector3 center, Vector3 size, float force)
    {
        pendingImpulses.Enqueue(new FluidImpulse(center, size * 0.5f, force));
        QueueCellNeighborhood(WorldToInternalCell(center), 3);
    }

    public bool IsTerrainSolidAtWorldPosition(Vector3 worldPosition)
    {
        return IsTerrainSolidAtCell(WorldToInternalCell(worldPosition));
    }

    public bool IsRenderNeighborSolid(Vector3Int renderCellPosition, Vector3Int direction, float fillRatio)
    {
        Vector3 min = GetRenderCellWorldMin(renderCellPosition);
        Vector3 max = min + Vector3.one * RenderVoxelSize;
        int verticalAxis = GetGravityAxisIndex();
        float visibleTop = Mathf.Lerp(GetAxis(min, verticalAxis), GetAxis(max, verticalAxis), Mathf.Clamp01(fillRatio));

        Vector3 sample = (min + max) * 0.5f;
        SetAxis(ref sample, verticalAxis, Mathf.Lerp(GetAxis(min, verticalAxis), visibleTop, 0.5f));
        sample += new Vector3(direction.x, direction.y, direction.z) * (InternalVoxelSize * 0.5f);
        return IsTerrainSolidAtWorldPosition(sample);
    }

    public Vector3Int WorldToInternalCell(Vector3 worldPosition)
    {
        Vector3 relative = (worldPosition - SimulationOrigin) / InternalVoxelSize;
        return new Vector3Int(
            Mathf.FloorToInt(relative.x),
            Mathf.FloorToInt(relative.y),
            Mathf.FloorToInt(relative.z));
    }

    public Vector3 InternalCellToWorldCenter(Vector3Int cellPosition)
    {
        return SimulationOrigin + new Vector3(
            (cellPosition.x + 0.5f) * InternalVoxelSize,
            (cellPosition.y + 0.5f) * InternalVoxelSize,
            (cellPosition.z + 0.5f) * InternalVoxelSize);
    }

    public Vector3Int InternalToRenderCell(Vector3Int internalCellPosition)
    {
        int ratio = RenderToInternalRatio;
        return new Vector3Int(
            Mathf.FloorToInt((float)internalCellPosition.x / ratio),
            Mathf.FloorToInt((float)internalCellPosition.y / ratio),
            Mathf.FloorToInt((float)internalCellPosition.z / ratio));
    }

    public Vector3 GetRenderCellWorldMin(Vector3Int renderCellPosition)
    {
        return SimulationOrigin + new Vector3(
            renderCellPosition.x * RenderVoxelSize,
            renderCellPosition.y * RenderVoxelSize,
            renderCellPosition.z * RenderVoxelSize);
    }

    private void StepSimulation(float deltaTime)
    {
        dynamicObstacleCache.Clear();

        bool changed = ApplyPendingImpulses();

        if (queuedCells.Count == 0)
        {
            if (changed)
            {
                MarkSimulationChanged();
            }
            return;
        }

        processingBuffer.Clear();
        processingBuffer.AddRange(queuedCells);
        queuedCells.Clear();
        processingBuffer.Sort(CompareCellsByGravity);

        int stepBudget = processingBuffer.Count <= fullSolveCellThreshold ? processingBuffer.Count : maxCellsPerStep;
        int processCount = Mathf.Min(processingBuffer.Count, Mathf.Max(16, stepBudget));
        for (int i = 0; i < processCount; i++)
        {
            changed |= SimulateCell(processingBuffer[i], deltaTime);
        }

        for (int i = processCount; i < processingBuffer.Count; i++)
        {
            queuedCells.Add(processingBuffer[i]);
        }

        if (changed)
        {
            MarkSimulationChanged();
        }
    }

    private bool ApplyPendingImpulses()
    {
        bool changed = false;
        while (pendingImpulses.Count > 0)
        {
            FluidImpulse impulse = pendingImpulses.Dequeue();
            Vector3 min = impulse.Center - impulse.HalfExtents;
            Vector3 max = impulse.Center + impulse.HalfExtents;

            Vector3Int minCell = WorldToInternalCell(min);
            Vector3Int maxCell = WorldToInternalCell(max);

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        Vector3Int cellPos = new Vector3Int(x, y, z);
                        if (!cells.TryGetValue(cellPos, out FluidCellState cell))
                        {
                            continue;
                        }

                        Vector3 cellWorld = InternalCellToWorldCenter(cellPos);
                        if (!IsPointInsideBox(cellWorld, impulse.Center, impulse.HalfExtents + Vector3.one * (InternalVoxelSize * 0.5f)))
                        {
                            continue;
                        }

                        Vector3 outward = cellWorld - impulse.Center;
                        if (outward.sqrMagnitude < 0.0001f)
                        {
                            outward = -GetGravityDirectionVector() + Vector3.up * 0.5f;
                        }

                        cell.Velocity += outward.normalized * (impulse.Force * Mathf.Max(0f, cell.Definition != null ? cell.Definition.explosionImpulseMultiplier : 1f));
                        QueueCellNeighborhood(cellPos, 1);
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    private bool SimulateCell(Vector3Int cellPosition, float deltaTime)
    {
        if (!cells.TryGetValue(cellPosition, out FluidCellState cell))
        {
            return false;
        }

        if (cell.Definition == null || cell.Liters <= MinLitersEpsilon)
        {
            cells.Remove(cellPosition);
            return true;
        }

        bool changed = false;
        changed |= ApplyVelocityTransfer(cellPosition, cell, deltaTime);
        changed |= ApplyGravityTransfer(cellPosition, cell, deltaTime);
        changed |= ApplyLateralTransfer(cellPosition, cell, deltaTime);

        float damping = Mathf.Max(0f, cell.Definition.velocityDamping) / Mathf.Max(0.01f, cell.Definition.viscosity);
        cell.Velocity = Vector3.Lerp(cell.Velocity, Vector3.zero, 1f - Mathf.Exp(-damping * deltaTime));

        if (cell.Liters <= MinLitersEpsilon)
        {
            cells.Remove(cellPosition);
            changed = true;
        }

        return changed;
    }

    private bool ApplyVelocityTransfer(Vector3Int sourcePos, FluidCellState source, float deltaTime)
    {
        if (source.Velocity.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 velocity = source.Velocity;
        Vector3 absVelocity = new Vector3(Mathf.Abs(velocity.x), Mathf.Abs(velocity.y), Mathf.Abs(velocity.z));
        Vector3Int direction;
        float dominantComponent;

        if (absVelocity.x >= absVelocity.y && absVelocity.x >= absVelocity.z)
        {
            direction = velocity.x >= 0f ? Vector3Int.right : Vector3Int.left;
            dominantComponent = absVelocity.x;
        }
        else if (absVelocity.y >= absVelocity.z)
        {
            direction = velocity.y >= 0f ? Vector3Int.up : Vector3Int.down;
            dominantComponent = absVelocity.y;
        }
        else
        {
            direction = velocity.z >= 0f ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 0, -1);
            dominantComponent = absVelocity.z;
        }

        float remainingTransfer = InternalCellCapacityLiters * dominantComponent * flowRateMultiplier * deltaTime;
        bool changed = false;
        Vector3Int currentPos = sourcePos;
        FluidCellState currentCell = source;

        for (int step = 0; step < maxVelocityCascadeSteps; step++)
        {
            if (currentCell == null || currentCell.Liters <= MinLitersEpsilon || remainingTransfer <= MinLitersEpsilon)
            {
                break;
            }

            Vector3Int targetPos = currentPos + direction;
            if (!TransferLiters(currentPos, targetPos, currentCell, ref remainingTransfer, false, out FluidCellState targetCell, out _))
            {
                break;
            }

            changed = true;
            if (currentCell.Liters > MinLitersEpsilon)
            {
                break;
            }

            cells.Remove(currentPos);
            currentPos = targetPos;
            currentCell = targetCell;
        }

        if (changed && currentCell != null)
        {
            currentCell.Velocity *= velocityTransferRetention;
            if (!ReferenceEquals(currentCell, source))
            {
                source.Velocity = Vector3.zero;
            }
        }

        return changed;
    }

    private bool ApplyGravityTransfer(Vector3Int sourcePos, FluidCellState source, float deltaTime)
    {
        float rate = Mathf.Max(0.1f, source.Definition.downwardCellVolumesPerSecond) / Mathf.Max(0.01f, source.Definition.viscosity);
        float remainingTransfer = InternalCellCapacityLiters * rate * flowRateMultiplier * deltaTime;
        bool changed = false;
        Vector3Int currentPos = sourcePos;
        FluidCellState currentCell = source;
        Vector3Int down = GetDownDirection();

        for (int step = 0; step < maxVerticalCascadeSteps; step++)
        {
            if (currentCell == null || currentCell.Liters <= MinLitersEpsilon || remainingTransfer <= MinLitersEpsilon)
            {
                break;
            }

            Vector3Int targetPos = currentPos + down;
            bool targetHadFluid = cells.TryGetValue(targetPos, out FluidCellState existingTarget) && existingTarget.Liters > MinLitersEpsilon;
            if (!TransferLiters(currentPos, targetPos, currentCell, ref remainingTransfer, false, out FluidCellState targetCell, out _))
            {
                break;
            }

            changed = true;
            if (currentCell.Liters > MinLitersEpsilon)
            {
                break;
            }

            cells.Remove(currentPos);
            currentPos = targetPos;
            currentCell = targetCell;
            if (!targetHadFluid)
            {
                break;
            }
        }

        return changed;
    }

    private bool ApplyLateralTransfer(Vector3Int sourcePos, FluidCellState source, float deltaTime)
    {
        if (HasDownwardCapacity(sourcePos, source.Definition))
        {
            return false;
        }

        Vector3Int[] lateralDirections = GetLateralDirections();
        if (lateralDirections.Length == 0)
        {
            return false;
        }

        List<LateralCandidate> candidates = new List<LateralCandidate>(lateralDirections.Length);
        float capacity = InternalCellCapacityLiters;

        foreach (Vector3Int direction in lateralDirections)
        {
            Vector3Int targetPos = sourcePos + direction;
            if (!CanFluidMoveIntoCell(targetPos, source.Definition))
            {
                continue;
            }

            float targetFill = 0f;
            if (cells.TryGetValue(targetPos, out FluidCellState targetCell))
            {
                if (targetCell.Definition != null && targetCell.Definition != source.Definition && targetCell.Liters > MinLitersEpsilon)
                {
                    continue;
                }

                targetFill = targetCell.Liters;
            }

            if (targetFill + MinLitersEpsilon >= source.Liters)
            {
                continue;
            }

            candidates.Add(new LateralCandidate(targetPos, targetFill));
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        candidates.Sort((a, b) => a.TargetFill.CompareTo(b.TargetFill));

        bool changed = false;
        float rate = Mathf.Max(0f, source.Definition.lateralCellVolumesPerSecond) / Mathf.Max(0.01f, source.Definition.viscosity);
        float remainingTransfer = capacity * rate * flowRateMultiplier * deltaTime;

        foreach (LateralCandidate candidate in candidates)
        {
            float desiredEqualize = Mathf.Max(0f, (source.Liters - candidate.TargetFill) * 0.5f);
            if (desiredEqualize <= MinLitersEpsilon)
            {
                continue;
            }

            float maxTransfer = Mathf.Min(remainingTransfer, desiredEqualize);
            if (maxTransfer <= MinLitersEpsilon)
            {
                break;
            }

            float transferBudget = maxTransfer;
            changed |= TransferLiters(sourcePos, candidate.Position, source, ref transferBudget, true, out _, out float moved);
            remainingTransfer -= moved;

            if (source.Liters <= MinLitersEpsilon)
            {
                break;
            }
        }

        return changed;
    }

    private bool TryTransfer(Vector3Int sourcePos, Vector3Int targetPos, FluidCellState source, float maxTransferLiters, bool blendVelocity)
    {
        float remainingTransfer = maxTransferLiters;
        return TransferLiters(sourcePos, targetPos, source, ref remainingTransfer, blendVelocity, out _, out _);
    }

    private bool TransferLiters(
        Vector3Int sourcePos,
        Vector3Int targetPos,
        FluidCellState source,
        ref float remainingTransfer,
        bool blendVelocity,
        out FluidCellState target,
        out float moved)
    {
        target = null;
        moved = 0f;

        if (source == null || source.Liters <= MinLitersEpsilon || remainingTransfer <= MinLitersEpsilon)
        {
            return false;
        }

        target = GetOrCreateCompatibleTarget(targetPos, source.Definition);
        if (target == null)
        {
            return false;
        }

        float capacityRemaining = InternalCellCapacityLiters - target.Liters;
        if (capacityRemaining <= MinLitersEpsilon)
        {
            return false;
        }

        moved = Mathf.Min(source.Liters, remainingTransfer, capacityRemaining);
        if (moved <= MinLitersEpsilon)
        {
            return false;
        }

        source.Liters -= moved;
        target.Liters += moved;
        remainingTransfer -= moved;
        target.Velocity = blendVelocity ? Vector3.Lerp(target.Velocity, source.Velocity, 0.4f) : source.Velocity * velocityTransferRetention;

        QueueCellNeighborhood(sourcePos, 1);
        QueueCellNeighborhood(targetPos, 1);
        return true;
    }

    private bool HasDownwardCapacity(Vector3Int sourcePos, FluidDefinition definition)
    {
        Vector3Int downPos = sourcePos + GetDownDirection();
        if (!CanFluidMoveIntoCell(downPos, definition))
        {
            return false;
        }

        if (!cells.TryGetValue(downPos, out FluidCellState downCell))
        {
            return true;
        }

        return downCell.Liters < InternalCellCapacityLiters - MinLitersEpsilon;
    }

    private FluidCellState GetOrCreateCompatibleTarget(Vector3Int position, FluidDefinition definition)
    {
        if (!CanFluidMoveIntoCell(position, definition))
        {
            return null;
        }

        if (cells.TryGetValue(position, out FluidCellState existing))
        {
            if (existing.Definition != null && existing.Definition != definition && existing.Liters > MinLitersEpsilon)
            {
                return null;
            }

            if (existing.Definition == null)
            {
                existing.Definition = definition;
            }

            return existing;
        }

        FluidCellState created = new FluidCellState
        {
            Definition = definition,
            Liters = 0f,
            Velocity = Vector3.zero
        };
        cells[position] = created;
        return created;
    }

    private float AddFluidBreadthFirst(Vector3Int startCell, float liters, FluidDefinition definition)
    {
        Queue<Vector3Int> fillQueue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        fillQueue.Enqueue(startCell);
        visited.Add(startCell);

        float remaining = liters;
        float acceptedTotal = 0f;

        while (fillQueue.Count > 0 && remaining > MinLitersEpsilon)
        {
            Vector3Int current = fillQueue.Dequeue();
            FluidCellState target = GetOrCreateCompatibleTarget(current, definition);
            if (target != null)
            {
                float accepted = Mathf.Min(remaining, InternalCellCapacityLiters - target.Liters);
                if (accepted > MinLitersEpsilon)
                {
                    target.Liters += accepted;
                    remaining -= accepted;
                    acceptedTotal += accepted;
                    QueueCellNeighborhood(current, 1);
                }
            }

            if (remaining <= MinLitersEpsilon)
            {
                break;
            }

            EnqueueFillNeighbors(current, fillQueue, visited);
        }

        return acceptedTotal;
    }

    private void EnqueueFillNeighbors(Vector3Int current, Queue<Vector3Int> queue, HashSet<Vector3Int> visited)
    {
        Vector3Int[] directions = GetPreferredFillDirections();
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3Int next = current + directions[i];
            if (visited.Add(next))
            {
                queue.Enqueue(next);
            }
        }
    }

    private Vector3Int[] GetPreferredFillDirections()
    {
        Vector3Int down = GetDownDirection();
        Vector3Int up = -down;
        Vector3Int[] laterals = GetLateralDirections();

        Vector3Int[] order = new Vector3Int[2 + laterals.Length];
        order[0] = down;
        for (int i = 0; i < laterals.Length; i++)
        {
            order[i + 1] = laterals[i];
        }

        order[order.Length - 1] = up;
        return order;
    }

    private Vector3Int[] GetAllNeighborDirections()
    {
        return new[]
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };
    }

    private bool CanFluidMoveIntoCell(Vector3Int cellPosition, FluidDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (IsTerrainSolidAtCell(cellPosition))
        {
            return false;
        }

        if (useDynamicObstacleLayers && IsDynamicObstacleAtCell(cellPosition))
        {
            return false;
        }

        if (cells.TryGetValue(cellPosition, out FluidCellState existing))
        {
            if (existing.Definition != null && existing.Definition != definition && existing.Liters > MinLitersEpsilon)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsDynamicObstacleAtCell(Vector3Int cellPosition)
    {
        if (dynamicObstacleLayers.value == 0)
        {
            return false;
        }

        if (dynamicObstacleCache.TryGetValue(cellPosition, out bool cached))
        {
            return cached;
        }

        Vector3 center = InternalCellToWorldCenter(cellPosition);
        Vector3 halfExtents = Vector3.one * (InternalVoxelSize * 0.45f);
        bool blocked = Physics.CheckBox(center, halfExtents, Quaternion.identity, dynamicObstacleLayers, QueryTriggerInteraction.Ignore);
        dynamicObstacleCache[cellPosition] = blocked;
        return blocked;
    }

    private bool IsTerrainSolidAtCell(Vector3Int cellPosition)
    {
        if (terrainManager == null)
        {
            terrainManager = FindFirstObjectByType<TerrainManager>();
            if (terrainManager == null)
            {
                return false;
            }
        }

        TerrainSettings settings = terrainManager.Settings;
        if (settings == null || settings.voxelsPerBlock <= 0 || settings.blockSize <= 0f)
        {
            return false;
        }

        Vector3 worldCenter = InternalCellToWorldCenter(cellPosition);
        if (IsOutsideGenerationSlice(worldCenter, settings))
        {
            return true;
        }

        float voxelSize = settings.blockSize / settings.voxelsPerBlock;

        Vector3 terrainRelative = worldCenter - new Vector3(settings.center.x, settings.center.y, settings.center.z);

        int blockX = Mathf.RoundToInt(terrainRelative.x / settings.blockSize);
        int blockY = Mathf.RoundToInt(terrainRelative.y / settings.blockSize);
        int blockZ = Mathf.RoundToInt(terrainRelative.z / settings.blockSize);

        Vector3 blockWorldCenter = new Vector3(
            settings.center.x + blockX * settings.blockSize,
            settings.center.y + blockY * settings.blockSize,
            settings.center.z + blockZ * settings.blockSize);

        Vector3 blockLocal = worldCenter - blockWorldCenter;
        int localX = Mathf.Clamp(Mathf.FloorToInt(blockLocal.x / voxelSize + settings.voxelsPerBlock / 2f), 0, settings.voxelsPerBlock - 1);
        int localY = Mathf.Clamp(Mathf.FloorToInt(blockLocal.y / voxelSize + settings.voxelsPerBlock / 2f), 0, settings.voxelsPerBlock - 1);
        int localZ = Mathf.Clamp(Mathf.FloorToInt(blockLocal.z / voxelSize + settings.voxelsPerBlock / 2f), 0, settings.voxelsPerBlock - 1);

        Vector3Int blockPos = new Vector3Int(blockX, blockY, blockZ);
        Vector3Int localVoxelPos = new Vector3Int(localX, localY, localZ);

        GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager.destroyedBlockPositions.Contains(blockPos))
        {
            return false;
        }

        if (persistenceManager.partiallyDestroyedBlocks.TryGetValue(blockPos, out HashSet<Vector3Int> destroyedVoxels) &&
            destroyedVoxels.Contains(localVoxelPos))
        {
            return false;
        }

        if (terrainManager.TerrainDataManager == null || terrainManager.TerrainDataManager.GetBiomeForHeight(blockPos.y) == null)
        {
            return false;
        }

        if (terrainManager.BlockGenerator == null)
        {
            return false;
        }

        return terrainManager.BlockGenerator.IsVoxelSolid(
            settings.generationType,
            settings.voxelsPerBlock,
            settings.blockSize,
            blockPos,
            localVoxelPos);
    }

    private bool IsOutsideGenerationSlice(Vector3 worldPosition, TerrainSettings settings)
    {
        switch (settings.generationType)
        {
            case TerrainGenerationType.SideScroller:
                return Mathf.Abs(worldPosition.z - settings.center.z) > generationSliceHalfThickness;
            case TerrainGenerationType.TopDown:
                return Mathf.Abs(worldPosition.y - settings.center.y) > generationSliceHalfThickness;
            default:
                return false;
        }
    }

    private Vector3Int FindLowestReachableCell(Vector3Int startCell, int maxDepth, FluidDefinition definition)
    {
        Vector3Int current = startCell;
        Vector3Int down = GetDownDirection();

        for (int i = 0; i < maxDepth; i++)
        {
            Vector3Int next = current + down;
            if (!CanFluidMoveIntoCell(next, definition))
            {
                break;
            }

            current = next;
        }

        return current;
    }

    private Vector3Int GetDownDirection()
    {
        switch (gravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return Vector3Int.left;
            case FluidGravityAxis.NegativeZ:
                return new Vector3Int(0, 0, -1);
            default:
                return Vector3Int.down;
        }
    }

    private Vector3 GetGravityDirectionVector()
    {
        Vector3Int down = GetDownDirection();
        return new Vector3(down.x, down.y, down.z);
    }

    private Vector3Int[] GetLateralDirections()
    {
        switch (gravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return new[]
                {
                    Vector3Int.up,
                    Vector3Int.down,
                    new Vector3Int(0, 0, 1),
                    new Vector3Int(0, 0, -1)
                };
            case FluidGravityAxis.NegativeZ:
                return new[]
                {
                    Vector3Int.right,
                    Vector3Int.left,
                    Vector3Int.up,
                    Vector3Int.down
                };
            default:
                return new[]
                {
                    Vector3Int.right,
                    Vector3Int.left,
                    new Vector3Int(0, 0, 1),
                    new Vector3Int(0, 0, -1)
                };
        }
    }

    private void QueueCellNeighborhood(Vector3Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    queuedCells.Add(new Vector3Int(center.x + x, center.y + y, center.z + z));
                }
            }
        }
    }

    private int CompareCellsByGravity(Vector3Int a, Vector3Int b)
    {
        switch (gravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return b.x.CompareTo(a.x);
            case FluidGravityAxis.NegativeZ:
                return b.z.CompareTo(a.z);
            default:
                return b.y.CompareTo(a.y);
        }
    }

    private int GetGravityAxisIndex()
    {
        switch (gravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return 0;
            case FluidGravityAxis.NegativeZ:
                return 2;
            default:
                return 1;
        }
    }

    private void MarkSimulationChanged()
    {
        Version++;

        if (showDebugLogs)
        {
            Debug.Log($"FluidSimulation: version updated to {Version}, active cells={cells.Count}");
        }
    }

    private static bool IsPointInsideBox(Vector3 point, Vector3 center, Vector3 halfExtents)
    {
        Vector3 delta = point - center;
        return Mathf.Abs(delta.x) <= halfExtents.x &&
               Mathf.Abs(delta.y) <= halfExtents.y &&
               Mathf.Abs(delta.z) <= halfExtents.z;
    }

    private static float GetAxis(Vector3 value, int axisIndex)
    {
        switch (axisIndex)
        {
            case 0:
                return value.x;
            case 2:
                return value.z;
            default:
                return value.y;
        }
    }

    private static void SetAxis(ref Vector3 value, int axisIndex, float axisValue)
    {
        switch (axisIndex)
        {
            case 0:
                value.x = axisValue;
                break;
            case 2:
                value.z = axisValue;
                break;
            default:
                value.y = axisValue;
                break;
        }
    }

    private sealed class FluidCellState
    {
        public FluidDefinition Definition;
        public float Liters;
        public Vector3 Velocity;
    }

    private struct LateralCandidate
    {
        public LateralCandidate(Vector3Int position, float targetFill)
        {
            Position = position;
            TargetFill = targetFill;
        }

        public Vector3Int Position { get; }
        public float TargetFill { get; }
    }

    private struct FluidImpulse
    {
        public FluidImpulse(Vector3 center, Vector3 halfExtents, float force)
        {
            Center = center;
            HalfExtents = halfExtents;
            Force = force;
        }

        public Vector3 Center { get; }
        public Vector3 HalfExtents { get; }
        public float Force { get; }
    }
}














