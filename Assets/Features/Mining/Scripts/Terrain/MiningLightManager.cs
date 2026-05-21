using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class MiningLightManager : MonoBehaviour
{
    private static readonly ProfilerMarker RestartPropagationMarker = new ProfilerMarker("MiningLightManager.RestartPropagation");
    private static readonly ProfilerMarker PropagationStepMarker = new ProfilerMarker("MiningLightManager.PropagationStep");
    private static readonly ProfilerMarker DrawGizmosMarker = new ProfilerMarker("MiningLightManager.DrawGizmos");

    private static readonly Vector3Int[] NeighborOffsets =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    [Header("Required References")]
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private Transform playerTransform;

    [Header("Light Propagation")]
    [SerializeField, Range(0f, 1f)] private float sourceBrightness = 1f;
    [SerializeField, Range(0f, 1f)] private float airCellTransmission = 0.9f;
    [SerializeField, Range(0f, 1f)] private float solidCellTransmission = 0.8f;
    [SerializeField, Range(0.001f, 1f)] private float minBrightness = 0.05f;
    [SerializeField, Min(0)] private int playerSourceRadiusCells = 1;
    [SerializeField, Min(1)] private int maxCalculatedCells = 4096;
    [SerializeField, Min(1)] private int maxPropagationCellsPerPropagationPerFrame = 64;
    [SerializeField, Min(1)] private int maxPropagationCellsPerFrame = 256;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = false;
    [SerializeField, Min(1)] private int maxGizmoCells = 8192;
    [SerializeField, Range(0.05f, 1f)] private float gizmoCellScale = 0.75f;
    [SerializeField] private Color airCellGizmoColor = new Color(1f, 0.92f, 0.25f, 0.45f);
    [SerializeField] private Color solidCellGizmoColor = new Color(1f, 0.45f, 0.15f, 0.45f);
    [SerializeField] private Color sourceCellGizmoColor = new Color(1f, 1f, 1f, 0.8f);

    private readonly Queue<VoxelCellKey> dirtyBrightnessCells = new Queue<VoxelCellKey>(512);
    private readonly HashSet<VoxelCellKey> queuedDirtyBrightnessCells = new HashSet<VoxelCellKey>();
    private PropagationRun displayedPropagation;
    private readonly List<PropagationRun> activePropagations = new List<PropagationRun>(4);
    private PropagationRun retainedPreviousPropagation;
    private int roundRobinPropagationIndex;

    private VoxelCellKey lastPlayerCell;
    private bool hasLastPlayerCell;
    private bool terrainDirty = true;
    private bool hasCalculated;
    private bool invalidSourceLogged;
    private bool noPropagationSourceLogged;
    private sealed class PropagationRun
    {
        public readonly Dictionary<VoxelCellKey, float> cellBrightness = new Dictionary<VoxelCellKey, float>();
        public readonly Dictionary<VoxelCellKey, bool> propagationCellCache = new Dictionary<VoxelCellKey, bool>();
        public readonly Dictionary<VoxelCellKey, bool> solidCellCache = new Dictionary<VoxelCellKey, bool>();
        public readonly List<PropagationJob> activeJobs = new List<PropagationJob>(32);
        public readonly HashSet<VoxelCellKey> sourceCells = new HashSet<VoxelCellKey>();
        public readonly List<VoxelCellKey> sourceCellOrder = new List<VoxelCellKey>(32);

        public PropagationRun previousPropagation;
        public int roundRobinJobIndex;
        public bool maxCalculatedCellsLogged;

        public void ClearCellStateCaches()
        {
            propagationCellCache.Clear();
            solidCellCache.Clear();
        }
    }

    private sealed class PropagationJob
    {
        public readonly VoxelCellKey sourceCell;
        public readonly Queue<FrontierCell> frontier = new Queue<FrontierCell>(64);

        public PropagationJob(VoxelCellKey sourceCell, float brightness)
        {
            this.sourceCell = sourceCell;
            frontier.Enqueue(new FrontierCell(sourceCell, brightness));
        }
    }

    private readonly struct FrontierCell
    {
        public readonly VoxelCellKey key;
        public readonly float brightness;

        public FrontierCell(VoxelCellKey key, float brightness)
        {
            this.key = key;
            this.brightness = brightness;
        }
    }

    public bool TryGetBrightness(VoxelCellKey key, out float brightness)
    {
        return TryGetDisplayedBrightness(key, out brightness);
    }

    public int DrainDirtyBrightnessCells(List<VoxelCellKey> buffer, int maxCells)
    {
        if (buffer == null)
        {
            Debug.LogError("MiningLightManager: dirty brightness cell buffer is null.", this);
            return 0;
        }

        buffer.Clear();
        if (maxCells <= 0)
        {
            Debug.LogError($"MiningLightManager: maxCells must be greater than 0. maxCells={maxCells}", this);
            return 0;
        }

        int drained = 0;
        while (dirtyBrightnessCells.Count > 0 && drained < maxCells)
        {
            VoxelCellKey key = dirtyBrightnessCells.Dequeue();
            queuedDirtyBrightnessCells.Remove(key);
            buffer.Add(key);
            drained++;
        }

        return drained;
    }

    private void OnEnable()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        terrainManager.VoxelManager.TerrainCellsChanged += HandleTerrainCellsChanged;
        terrainDirty = true;
    }

    private void OnDisable()
    {
        if (terrainManager != null && terrainManager.VoxelManager != null)
        {
            terrainManager.VoxelManager.TerrainCellsChanged -= HandleTerrainCellsChanged;
        }
    }

    private void Update()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        if (!TryGetPlayerCell(out VoxelCellKey playerCell))
        {
            ClearLightState();
            if (!invalidSourceLogged)
            {
                invalidSourceLogged = true;
                Debug.LogWarning("MiningLightManager: player position could not be converted to a voxel cell.", this);
            }
            return;
        }

        invalidSourceLogged = false;

        bool playerCellChanged = !hasLastPlayerCell || !playerCell.Equals(lastPlayerCell);
        if (!hasCalculated || terrainDirty || playerCellChanged)
        {
            RestartPropagation(playerCell);
        }

        if (activePropagations.Count > 0)
        {
            ProcessPropagationStep();
        }
    }

    private void RestartPropagation(VoxelCellKey playerCell)
    {
        using (RestartPropagationMarker.Auto())
        {
            PropagationRun nextPropagation = new PropagationRun();
            retainedPreviousPropagation = displayedPropagation;
            nextPropagation.previousPropagation = retainedPreviousPropagation;
            displayedPropagation = nextPropagation;
            activePropagations.Add(nextPropagation);
            terrainDirty = false;
            hasCalculated = true;
            hasLastPlayerCell = true;
            lastPlayerCell = playerCell;

            AddSourcePropagation(nextPropagation, playerCell);
            int radius = Mathf.Max(0, playerSourceRadiusCells);
            for (int shell = 1; shell <= radius; shell++)
            {
                AddSourceShell(nextPropagation, playerCell, shell);
            }

            if (nextPropagation.activeJobs.Count == 0)
            {
                if (!noPropagationSourceLogged)
                {
                    noPropagationSourceLogged = true;
                    Debug.LogWarning("MiningLightManager: no generated player-adjacent cells were available for light propagation.", this);
                }

                MarkStalePreviousCellsDirty(nextPropagation);
                nextPropagation.previousPropagation = null;
                activePropagations.Remove(nextPropagation);
                ClampRoundRobinPropagationIndex();
                retainedPreviousPropagation = null;
                return;
            }

            noPropagationSourceLogged = false;
        }
    }

    private void AddSourceShell(PropagationRun propagation, VoxelCellKey centerCell, int shell)
    {
        for (int x = -shell; x <= shell; x++)
        {
            for (int y = -shell; y <= shell; y++)
            {
                for (int z = -shell; z <= shell; z++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != shell)
                    {
                        continue;
                    }

                    if (TryGetOffsetCell(centerCell, new Vector3Int(x, y, z), out VoxelCellKey sourceCell))
                    {
                        AddSourcePropagation(propagation, sourceCell);
                    }
                }
            }
        }
    }

    private void AddSourcePropagation(PropagationRun propagation, VoxelCellKey sourceCell)
    {
        if (propagation.sourceCells.Contains(sourceCell) || !IsLightPropagationCellCached(propagation, sourceCell))
        {
            return;
        }

        float brightness = Mathf.Clamp01(sourceBrightness);
        if (!AddOrUpdateCell(propagation, sourceCell, brightness))
        {
            return;
        }

        propagation.sourceCells.Add(sourceCell);
        propagation.sourceCellOrder.Add(sourceCell);
        propagation.activeJobs.Add(new PropagationJob(sourceCell, brightness));
    }

    private void ProcessPropagationStep()
    {
        using (PropagationStepMarker.Auto())
        {
            int processedCells = 0;
            int maxCellsPerPropagation = Mathf.Max(1, maxPropagationCellsPerPropagationPerFrame);
            int maxCellsThisFrame = Mathf.Max(1, maxPropagationCellsPerFrame);
            int propagationSlotsThisFrame = activePropagations.Count;
            while (activePropagations.Count > 0 &&
                   propagationSlotsThisFrame > 0 &&
                   processedCells < maxCellsThisFrame)
            {
                ClampRoundRobinPropagationIndex();
                PropagationRun propagation = activePropagations[roundRobinPropagationIndex];

                int processedPropagationCells = 0;
                while (propagation.activeJobs.Count > 0 &&
                       processedPropagationCells < maxCellsPerPropagation &&
                       processedCells < maxCellsThisFrame)
                {
                    ProcessOnePropagationCell(propagation);
                    processedPropagationCells++;
                    processedCells++;
                }

                if (propagation.activeJobs.Count == 0)
                {
                    CompletePropagation(propagation);
                    propagationSlotsThisFrame--;
                    continue;
                }

                roundRobinPropagationIndex++;
                propagationSlotsThisFrame--;
            }
        }
    }

    private void ProcessOnePropagationCell(PropagationRun propagation)
    {
        if (propagation.activeJobs.Count == 0)
        {
            return;
        }

        if (propagation.roundRobinJobIndex >= propagation.activeJobs.Count)
        {
            propagation.roundRobinJobIndex = 0;
        }

        PropagationJob job = propagation.activeJobs[propagation.roundRobinJobIndex];
        bool jobStillActive = ProcessOneFrontierCell(propagation, job);
        if (!jobStillActive)
        {
            propagation.activeJobs.RemoveAt(propagation.roundRobinJobIndex);
            if (propagation.roundRobinJobIndex >= propagation.activeJobs.Count)
            {
                propagation.roundRobinJobIndex = 0;
            }
            return;
        }

        propagation.roundRobinJobIndex++;
        if (propagation.roundRobinJobIndex >= propagation.activeJobs.Count)
        {
            propagation.roundRobinJobIndex = 0;
        }
    }

    private void CompletePropagation(PropagationRun propagation)
    {
        MarkStalePreviousCellsDirty(propagation);
        propagation.previousPropagation = null;
        activePropagations.Remove(propagation);
        ClampRoundRobinPropagationIndex();
        if (displayedPropagation == propagation)
        {
            retainedPreviousPropagation = null;
        }
    }

    private void ClampRoundRobinPropagationIndex()
    {
        if (activePropagations.Count == 0)
        {
            roundRobinPropagationIndex = 0;
            return;
        }

        if (roundRobinPropagationIndex >= activePropagations.Count)
        {
            roundRobinPropagationIndex = 0;
        }
    }

    private bool ProcessOneFrontierCell(PropagationRun propagation, PropagationJob job)
    {
        if (job.frontier.Count == 0)
        {
            return false;
        }

        FrontierCell current = job.frontier.Dequeue();
        if (propagation.cellBrightness.TryGetValue(current.key, out float recordedBrightness) &&
            current.brightness >= recordedBrightness)
        {
            PropagateFromCell(propagation, job, current);
        }

        return job.frontier.Count > 0;
    }

    private void PropagateFromCell(PropagationRun propagation, PropagationJob job, FrontierCell current)
    {
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            if (!TryGetOffsetCell(current.key, NeighborOffsets[i], out VoxelCellKey neighbor) ||
                !IsLightPropagationCellCached(propagation, neighbor))
            {
                continue;
            }

            bool solid = IsSolidCellCached(propagation, neighbor);
            float transmission = solid ? solidCellTransmission : airCellTransmission;
            float nextBrightness = current.brightness * transmission;
            if (nextBrightness < minBrightness)
            {
                continue;
            }

            if (AddOrUpdateCell(propagation, neighbor, nextBrightness))
            {
                job.frontier.Enqueue(new FrontierCell(neighbor, nextBrightness));
            }
        }
    }

    private bool AddOrUpdateCell(PropagationRun propagation, VoxelCellKey key, float brightness)
    {
        if (propagation.cellBrightness.TryGetValue(key, out float existingBrightness))
        {
            if (brightness <= existingBrightness)
            {
                return false;
            }

            propagation.cellBrightness[key] = brightness;
            EnqueueDirtyBrightnessCell(key);
            return true;
        }

        if (propagation.cellBrightness.Count >= maxCalculatedCells)
        {
            if (!propagation.maxCalculatedCellsLogged)
            {
                propagation.maxCalculatedCellsLogged = true;
                Debug.LogWarning($"MiningLightManager: reached maxCalculatedCells={maxCalculatedCells}. Increase the limit if light is visibly clipped.", this);
            }
            return false;
        }

        propagation.cellBrightness.Add(key, brightness);
        EnqueueDirtyBrightnessCell(key);
        return true;
    }

    private void MarkStalePreviousCellsDirty(PropagationRun propagation)
    {
        PropagationRun previous = propagation.previousPropagation;
        while (previous != null)
        {
            foreach (KeyValuePair<VoxelCellKey, float> pair in previous.cellBrightness)
            {
                if (!propagation.cellBrightness.ContainsKey(pair.Key))
                {
                    EnqueueDirtyBrightnessCell(pair.Key);
                }
            }
            previous = previous.previousPropagation;
        }
    }

    private void EnqueueDirtyBrightnessCell(VoxelCellKey key)
    {
        if (queuedDirtyBrightnessCells.Add(key))
        {
            dirtyBrightnessCells.Enqueue(key);
        }
    }

    private bool TryGetPlayerCell(out VoxelCellKey key)
    {
        key = default;
        return terrainManager != null &&
               terrainManager.VoxelManager != null &&
               playerTransform != null &&
               terrainManager.VoxelManager.TryGetVoxelCellAtWorldPosition(playerTransform.position, out key);
    }

    private bool TryGetDisplayedBrightness(VoxelCellKey key, out float brightness)
    {
        PropagationRun propagation = displayedPropagation;
        while (propagation != null)
        {
            if (propagation.cellBrightness.TryGetValue(key, out brightness))
            {
                return true;
            }

            propagation = propagation.previousPropagation;
        }

        brightness = 0f;
        return false;
    }

    private bool TryGetOffsetCell(VoxelCellKey key, Vector3Int offset, out VoxelCellKey offsetCell)
    {
        Vector3Int blockPosition = key.blockPosition;
        Vector3Int localPosition = key.localVoxelPosition + offset;
        if (!terrainManager.VoxelManager.NormalizeVoxelPosition(ref blockPosition, ref localPosition))
        {
            offsetCell = default;
            return false;
        }

        offsetCell = new VoxelCellKey(blockPosition, localPosition);
        return true;
    }

    private bool IsLightPropagationCell(VoxelCellKey key)
    {
        if (terrainManager == null || terrainManager.BlockManager == null || terrainManager.TerrainDataManager == null)
        {
            return false;
        }

        BlockManager.BlockInstanceData block = terrainManager.BlockManager.GetBlockAt(key.blockPosition);
        if (block != null && block.block != null)
        {
            return true;
        }

        return terrainManager.TerrainDataManager.IsBlockGenerationExcluded(key.blockPosition);
    }

    private bool IsLightPropagationCellCached(PropagationRun propagation, VoxelCellKey key)
    {
        if (propagation == null)
        {
            Debug.LogError("MiningLightManager: propagation run is null while resolving light propagation cell.", this);
            return false;
        }

        if (propagation.propagationCellCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool resolved = IsLightPropagationCell(key);
        propagation.propagationCellCache.Add(key, resolved);
        return resolved;
    }

    private bool IsSolidCellCached(PropagationRun propagation, VoxelCellKey key)
    {
        if (propagation == null)
        {
            Debug.LogError("MiningLightManager: propagation run is null while resolving solid cell.", this);
            return false;
        }

        if (propagation.solidCellCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool resolved = terrainManager.VoxelManager.IsVoxelCellSolid(key);
        propagation.solidCellCache.Add(key, resolved);
        return resolved;
    }

    private void HandleTerrainCellsChanged(TerrainChangeBatch change)
    {
        ClearActivePropagationCellStateCaches();
        terrainDirty = true;
    }

    private void ClearActivePropagationCellStateCaches()
    {
        for (int i = 0; i < activePropagations.Count; i++)
        {
            activePropagations[i]?.ClearCellStateCaches();
        }
    }

    private void ClearLightState()
    {
        MarkDisplayedCellsDirty();
        displayedPropagation = null;
        activePropagations.Clear();
        retainedPreviousPropagation = null;
        roundRobinPropagationIndex = 0;
        hasLastPlayerCell = false;
        hasCalculated = false;
    }

    private void MarkDisplayedCellsDirty()
    {
        PropagationRun propagation = displayedPropagation;
        while (propagation != null)
        {
            foreach (KeyValuePair<VoxelCellKey, float> pair in propagation.cellBrightness)
            {
                EnqueueDirtyBrightnessCell(pair.Key);
            }

            propagation = propagation.previousPropagation;
        }
    }

    private bool ValidateConfiguration()
    {
        if (terrainManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager is not assigned.", this);
            return false;
        }

        if (terrainManager.VoxelManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager.VoxelManager is not assigned.", this);
            return false;
        }

        if (terrainManager.BlockManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager.BlockManager is not assigned.", this);
            return false;
        }

        if (terrainManager.TerrainDataManager == null)
        {
            Debug.LogError("MiningLightManager: TerrainManager.TerrainDataManager is not assigned.", this);
            return false;
        }

        if (playerTransform == null)
        {
            Debug.LogError("MiningLightManager: Player Transform is not assigned.", this);
            return false;
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawOnlyWhenSelected)
        {
            DrawLightGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (drawOnlyWhenSelected)
        {
            DrawLightGizmos();
        }
    }

    private void DrawLightGizmos()
    {
        PropagationRun propagation = displayedPropagation;
        if (!drawGizmos ||
            terrainManager == null ||
            terrainManager.VoxelManager == null ||
            propagation == null ||
            propagation.cellBrightness.Count == 0)
        {
            return;
        }

        using (DrawGizmosMarker.Auto())
        {
            int drawn = 0;
            DrawSourceGizmos(propagation, ref drawn);
            DrawBrightnessGizmos(propagation, ref drawn);
            DrawFallbackBrightnessGizmos(propagation.previousPropagation, propagation, ref drawn);
        }
    }

    private void DrawSourceGizmos(PropagationRun propagation, ref int drawn)
    {
        for (int i = 0; i < propagation.sourceCellOrder.Count && drawn < maxGizmoCells; i++)
        {
            VoxelCellKey sourceCell = propagation.sourceCellOrder[i];
            if (!propagation.cellBrightness.TryGetValue(sourceCell, out float brightness))
            {
                continue;
            }

            DrawGizmoCell(propagation, sourceCell, brightness);
            drawn++;
        }
    }

    private void DrawBrightnessGizmos(PropagationRun propagation, ref int drawn)
    {
        foreach (KeyValuePair<VoxelCellKey, float> pair in propagation.cellBrightness)
        {
            if (drawn >= maxGizmoCells)
            {
                break;
            }

            if (propagation.sourceCells.Contains(pair.Key) || pair.Value <= minBrightness)
            {
                continue;
            }

            DrawGizmoCell(propagation, pair.Key, pair.Value);
            drawn++;
        }
    }

    private void DrawFallbackBrightnessGizmos(PropagationRun fallback, PropagationRun newestPropagation, ref int drawn)
    {
        while (fallback != null && drawn < maxGizmoCells)
        {
            foreach (KeyValuePair<VoxelCellKey, float> pair in fallback.cellBrightness)
            {
                if (drawn >= maxGizmoCells)
                {
                    return;
                }

                if (HasNewerBrightness(newestPropagation, fallback, pair.Key) || pair.Value <= minBrightness)
                {
                    continue;
                }

                DrawGizmoCell(fallback, pair.Key, pair.Value);
                drawn++;
            }

            fallback = fallback.previousPropagation;
        }
    }

    private bool HasNewerBrightness(PropagationRun newestPropagation, PropagationRun stopBeforePropagation, VoxelCellKey key)
    {
        PropagationRun propagation = newestPropagation;
        while (propagation != null && propagation != stopBeforePropagation)
        {
            if (propagation.cellBrightness.ContainsKey(key))
            {
                return true;
            }

            propagation = propagation.previousPropagation;
        }

        return false;
    }

    private void DrawGizmoCell(PropagationRun propagation, VoxelCellKey key, float brightness)
    {
        Bounds bounds = terrainManager.VoxelManager.GetVoxelCellWorldBounds(key);
        Color color = GetGizmoColor(propagation, key, brightness);
        Gizmos.color = color;
        Gizmos.DrawCube(bounds.center, bounds.size * gizmoCellScale);
    }

    private Color GetGizmoColor(PropagationRun propagation, VoxelCellKey key, float brightness)
    {
        Color color = propagation.sourceCells.Contains(key)
            ? sourceCellGizmoColor
            : (terrainManager.VoxelManager.IsVoxelCellSolid(key) ? solidCellGizmoColor : airCellGizmoColor);

        float alphaScale = Mathf.InverseLerp(minBrightness, Mathf.Max(minBrightness, sourceBrightness), brightness);
        color.a *= Mathf.Clamp01(alphaScale);
        return color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        sourceBrightness = Mathf.Clamp01(sourceBrightness);
        airCellTransmission = Mathf.Clamp01(airCellTransmission);
        solidCellTransmission = Mathf.Clamp01(solidCellTransmission);
        minBrightness = Mathf.Clamp(minBrightness, 0.001f, Mathf.Max(0.001f, sourceBrightness));
        playerSourceRadiusCells = Mathf.Max(0, playerSourceRadiusCells);
        maxCalculatedCells = Mathf.Max(1, maxCalculatedCells);
        maxPropagationCellsPerPropagationPerFrame = Mathf.Max(1, maxPropagationCellsPerPropagationPerFrame);
        maxPropagationCellsPerFrame = Mathf.Max(1, maxPropagationCellsPerFrame);
        maxGizmoCells = Mathf.Max(1, maxGizmoCells);
        gizmoCellScale = Mathf.Clamp(gizmoCellScale, 0.05f, 1f);
    }
#endif
}
