using System;
using System.Collections.Generic;
using Unity.Profiling;
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

public partial class FluidManager : MonoBehaviour
{
    private const float MinLitersEpsilon = 0.0001f;
    private const int MaxFillSearchDepth = 32;

    private static readonly ProfilerMarker UpdateMarker =
        new ProfilerMarker("FluidManager.Update");
    private static readonly ProfilerMarker StepSimulationMarker =
        new ProfilerMarker("FluidManager.StepSimulation");
    private static readonly ProfilerMarker BuildProcessingBufferMarker =
        new ProfilerMarker("FluidManager.BuildProcessingBuffer");
    private static readonly ProfilerMarker SortProcessingBufferMarker =
        new ProfilerMarker("FluidManager.SortProcessingBuffer");
    private static readonly ProfilerMarker ProcessCellsMarker =
        new ProfilerMarker("FluidManager.ProcessCells");
    private static readonly ProfilerMarker CopyQueuedCellsForBudgetMarker =
        new ProfilerMarker("FluidManager.CopyQueuedCellsForBudget");
    private static readonly ProfilerMarker ApplyPendingImpulsesMarker =
        new ProfilerMarker("FluidManager.ApplyPendingImpulses");
    private static readonly ProfilerMarker SimulateCellMarker =
        new ProfilerMarker("FluidManager.SimulateCell");
    private static readonly ProfilerMarker ApplyVelocityTransferMarker =
        new ProfilerMarker("FluidManager.ApplyVelocityTransfer");
    private static readonly ProfilerMarker ApplyGravityTransferMarker =
        new ProfilerMarker("FluidManager.ApplyGravityTransfer");
    private static readonly ProfilerMarker ApplyLateralTransferMarker =
        new ProfilerMarker("FluidManager.ApplyLateralTransfer");
    private static readonly ProfilerMarker TransferLitersMarker =
        new ProfilerMarker("FluidManager.TransferLiters");
    private static readonly ProfilerMarker CanFluidMoveIntoCellMarker =
        new ProfilerMarker("FluidManager.CanFluidMoveIntoCell");
    private static readonly ProfilerMarker IsDynamicObstacleAtCellMarker =
        new ProfilerMarker("FluidManager.IsDynamicObstacleAtCell");
    private static readonly ProfilerMarker IsTerrainSolidAtCellMarker =
        new ProfilerMarker("FluidManager.IsTerrainSolidAtCell");
    private static readonly ProfilerMarker IsTerrainSolidAtCellUncachedMarker =
        new ProfilerMarker("FluidManager.IsTerrainSolidAtCellUncached");
    private static readonly ProfilerMarker QueueCellNeighborhoodMarker =
        new ProfilerMarker("FluidManager.QueueCellNeighborhood");
    private static readonly Comparison<LateralCandidate> CompareLateralCandidateByFill =
        CompareLateralCandidatesByFill;

    [Header("参照設定")]
    [SerializeField, InspectorName("地形マネージャー"), Tooltip("この流体系が参照する TerrainManager です。通常は同じシーンのものを割り当てます。")] private TerrainManager terrainManager;
    [SerializeField, InspectorName("既定の流体定義"), Tooltip("個別指定がないときに使う流体の基本設定です。通常は Water を指定します。")] private FluidDefinition defaultFluidDefinition;

    [Header("グリッド設定")]
    [SerializeField, InspectorName("シミュレーション原点オフセット"), Tooltip("地形基準の流体グリッド開始位置を微調整します。通常は 0 のままで構いません。")] private Vector3 simulationOriginOffset = Vector3.zero;
    [SerializeField, InspectorName("1ユニットの実メートル換算"), Tooltip("1 Unity Unit を何メートルとして扱うかです。内部セルの容量(L)計算に使います。")] private float metersPerUnit = 1.0f;
    [SerializeField, InspectorName("1ブロックあたりの内部流体ボクセル数"), Tooltip("流れの計算に使う細かさです。大きいほど隙間に入りやすいですが重くなります。")] private int internalVoxelsPerBlock = 8;
    [SerializeField, InspectorName("1ブロックあたりの表示流体ボクセル数"), Tooltip("見た目として何分割で描くかです。内部流体ボクセル数の約数にしてください。")] private int renderVoxelsPerBlock = 2;
    [SerializeField, InspectorName("重力方向"), Tooltip("液体が落ちる方向です。通常は NegativeY のまま使います。")] private FluidGravityAxis gravityAxis = FluidGravityAxis.NegativeY;
    [SerializeField, InspectorName("注入時に最下点へ直接入れる"), Tooltip("オンにすると注いだ水を到達可能な最下セルへ直接入れます。落下中を見せたい場合はオフにします。")] private bool injectIntoLowestReachableCell = false;

    [Header("シミュレーション設定")]
    [SerializeField, InspectorName("更新間隔(秒)"), Tooltip("流体計算を行う間隔です。大きいほど軽くなりますが、動きは粗くなります。")] private float simulationTickInterval = 0.05f;
    [SerializeField, InspectorName("1回の最大処理セル数"), Tooltip("1 tick で処理する内部セル数の上限です。")] private int maxCellsPerStep = 2048;
    [SerializeField, InspectorName("全件処理に切り替えるセル数"), Tooltip("待機セル数がこの値以下なら、その tick で全セルを処理します。")] private int fullSolveCellThreshold = 8192;
    [SerializeField, InspectorName("流れの全体倍率"), Tooltip("落下、横流れ、爆発後の移動量をまとめて増減します。")] private float flowRateMultiplier = 2f;
    [SerializeField, InspectorName("プレイ面の半厚み"), Tooltip("プレイ面から流体がはみ出せる厚みの半分です。")] private float generationSliceHalfThickness = 0.5f;
    [SerializeField, InspectorName("1回の最大落下セル数"), Tooltip("1 tick で下方向へ連続移動できる最大セル数です。")] private int maxVerticalCascadeSteps = 3;
    [SerializeField, InspectorName("1回の最大吹き飛びセル数"), Tooltip("爆発などの速度で 1 tick に連続移動できる最大セル数です。")] private int maxVelocityCascadeSteps = 4;
    [SerializeField, InspectorName("吹き飛び速度の残りやすさ"), Tooltip("爆発などで付いた速度を移動先にどれだけ残すかです。小さいほどすぐ止まります。"), Range(0f, 1f)] private float velocityTransferRetention = 0.75f;
    [SerializeField, InspectorName("動的障害物を使う"), Tooltip("瓦礋など動いている Collider を流体の障害物として扱います。")] private bool useDynamicObstacleLayers = true;
    [SerializeField, InspectorName("動的障害物レイヤー"), Tooltip("流体の進行を塞ぐ動的オブジェクトの Layer を指定します。")] private LayerMask dynamicObstacleLayers;
    [SerializeField, InspectorName("デバッグログを表示"), Tooltip("流体更新のログを Console に出します。通常はオフのままで構いません。")] private bool showDebugLogs = false;

    [Header("ハイブリッド設定 (飛沫ボクセル化)")]
    [SerializeField, InspectorName("流体飛沫プレハブ"), Tooltip("爆破時に弾け飛ぶ流体の物理オブジェクトPrefabです。")] private GameObject fluidSplashPrefab;

    private readonly Dictionary<Vector3Int, FluidCellState> cells = new Dictionary<Vector3Int, FluidCellState>();
    private readonly HashSet<Vector3Int> queuedCells = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> processingBuffer = new List<Vector3Int>();
    private readonly Dictionary<Vector3Int, bool> dynamicObstacleCache = new Dictionary<Vector3Int, bool>();
    private readonly Dictionary<Vector3Int, bool> terrainSolidCache = new Dictionary<Vector3Int, bool>();
    private readonly Queue<FluidImpulse> pendingImpulses = new Queue<FluidImpulse>();
    private readonly List<LateralCandidate> lateralCandidateBuffer = new List<LateralCandidate>();
    private readonly FluidSimulationSolver simulationSolver = new FluidSimulationSolver();

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
    public bool IsSimulationPausedForRestore { get; private set; }

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
        if (IsSimulationPausedForRestore)
        {
            return;
        }

        using var updateScope = UpdateMarker.Auto();
        tickTimer += Time.deltaTime;
        while (tickTimer >= simulationTickInterval)
        {
            tickTimer -= simulationTickInterval;
            simulationSolver.Step(this, simulationTickInterval);
        }
    }

    public void PauseSimulationForRestore()
    {
        IsSimulationPausedForRestore = true;
        tickTimer = 0f;
    }

    public void ResumeSimulationAfterRestore()
    {
        IsSimulationPausedForRestore = false;
        tickTimer = 0f;
    }

    public void QueueRuntimeActiveCells()
    {
        dynamicObstacleCache.Clear();
        terrainSolidCache.Clear();
        if (cells.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Vector3Int, FluidCellState> pair in cells)
        {
            QueueCellNeighborhood(pair.Key, 1);
        }

        MarkSimulationChanged();
    }

    public void QueueRuntimeActiveCellsInWorldBounds(Bounds worldBounds)
    {
        dynamicObstacleCache.Clear();
        terrainSolidCache.Clear();
        if (cells.Count == 0)
        {
            return;
        }

        bool queuedAny = false;
        foreach (KeyValuePair<Vector3Int, FluidCellState> pair in cells)
        {
            if (!worldBounds.Contains(InternalCellToWorldCenter(pair.Key)))
            {
                continue;
            }

            QueueCellNeighborhood(pair.Key, 1);
            queuedAny = true;
        }

        if (queuedAny)
        {
            MarkSimulationChanged();
        }
    }

    public void QueuePostRestoreActiveCells()
    {
        QueueRuntimeActiveCells();
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
        terrainSolidCache.Clear();
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
            Debug.LogError("FluidManager: No FluidDefinition is assigned.", this);
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

    public FluidDefinition GetFluidDefinitionAtWorldPosition(Vector3 worldPosition)
    {
        Vector3Int cellPos = WorldToInternalCell(worldPosition);
        if (cells.TryGetValue(cellPos, out FluidCellState cell))
        {
            return cell.Definition;
        }
        return defaultFluidDefinition;
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
        Vector3Int center = WorldToInternalCell(worldPosition);
        InvalidateTerrainSolidCacheNeighborhood(center, 2);
        QueueCellNeighborhood(center, 2);
    }

    public void MarkDirtyAroundWorldPosition(Vector3 worldPosition, int radius = 1)
    {
        Vector3Int center = WorldToInternalCell(worldPosition);
        InvalidateTerrainSolidCacheNeighborhood(center, radius);
        QueueCellNeighborhood(center, radius);
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

}
