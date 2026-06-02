using System.Collections.Generic;
using UnityEngine;

public partial class FluidManager
{
    internal void StepSimulationCore(float deltaTime)
    {
        using var stepScope = StepSimulationMarker.Auto();
        dynamicObstacleCache.Clear();
        terrainSolidCache.Clear();

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

        using (BuildProcessingBufferMarker.Auto())
        {
            int stepBudget = queuedCells.Count <= fullSolveCellThreshold ? queuedCells.Count : maxCellsPerStep;
            int processCount = Mathf.Min(queuedCells.Count, Mathf.Max(16, stepBudget));
            if (processCount >= queuedCells.Count)
            {
                processingBuffer.AddRange(queuedCells);
                queuedCells.Clear();
            }
            else
            {
                CopyQueuedCellsForBudget(processCount);
            }
        }

        using (SortProcessingBufferMarker.Auto())
        {
            processingBuffer.Sort(CompareCellsByGravity);
        }

        using (ProcessCellsMarker.Auto())
        {
            for (int i = 0; i < processingBuffer.Count; i++)
            {
                changed |= SimulateCell(processingBuffer[i], deltaTime);
            }
        }

        if (changed)
        {
            MarkSimulationChanged();
        }
    }

    private void CopyQueuedCellsForBudget(int processCount)
    {
        using var copyScope = CopyQueuedCellsForBudgetMarker.Auto();
        foreach (Vector3Int cell in queuedCells)
        {
            processingBuffer.Add(cell);
            if (processingBuffer.Count >= processCount)
            {
                break;
            }
        }

        for (int i = 0; i < processingBuffer.Count; i++)
        {
            queuedCells.Remove(processingBuffer[i]);
        }
    }

    private bool ApplyPendingImpulses()
    {
        using var impulseScope = ApplyPendingImpulsesMarker.Auto();
        FluidSplash splashPrefabComponent = null;
        if (fluidSplashPrefab != null && !fluidSplashPrefab.TryGetComponent(out splashPrefabComponent))
        {
            Debug.LogError("FluidManager: Fluid splash prefab must have a FluidSplash component on its root GameObject.", this);
            enabled = false;
            return false;
        }

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

                        // ------ HYBRID FEATURE (Debug): 1 Cell = 1 Splash -----
                        if (fluidSplashPrefab != null && cell.Liters > 0.01f)
                        {
                            Vector3 outward = cellWorld - impulse.Center;
                            if (outward.sqrMagnitude < 0.0001f)
                            {
                                outward = -GetGravityDirectionVector() + Vector3.up * 0.5f;
                            }

                            // Add a little upward tweak so ground explosions look better
                            outward.y += 0.5f;
                            // Add slight X randomness, but restrict Z entirely to keep it 2.5D
                            outward += new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0, 0);
                            outward.z = 0f;

                            float forceMultiplier = cell.Definition != null ? cell.Definition.explosionImpulseMultiplier : 1f;
                            
                            // The grid system dampens velocity immediately, but rigidbodies will fly forever.
                            // We drastically scale down the raw impulse Force to a sensible physical speed (m/s), constrained to the screen size.
                            float launchSpeed = (impulse.Force * forceMultiplier) * 0.04f; 
                            launchSpeed = Mathf.Clamp(launchSpeed, 2f, 18f); // Minimum 2m/s, maximum 18m/s (prevents km drops)

                            Vector3 launchVelocity = outward.normalized * (launchSpeed * UnityEngine.Random.Range(0.8f, 1.2f));

                            // Extract the fluid from the grid completely
                            float extractedLiters = cell.Liters;
                            cell.Liters = 0f;
                            cell.Velocity = Vector3.zero;

                            // Create physics splash directly (1 per cell)
                            GameObject splashGo = Instantiate(fluidSplashPrefab, cellWorld, Quaternion.identity);
                            FluidSplash splash = splashGo.GetComponent<FluidSplash>();
                            if (splash == null)
                            {
                                Debug.LogError("FluidManager: Instantiated fluid splash is missing FluidSplash. Check the assigned prefab.", this);
                                Destroy(splashGo);
                                enabled = false;
                                return changed;
                            }

                            splash.Initialize(this, cell.Definition, extractedLiters, launchVelocity);

                            QueueCellNeighborhood(cellPos, 1);
                            changed = true;
                        }
                        else
                        {
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
        }

        return changed;
    }

    private bool SimulateCell(Vector3Int cellPosition, float deltaTime)
    {
        using var simulateScope = SimulateCellMarker.Auto();
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
        using var velocityScope = ApplyVelocityTransferMarker.Auto();
        if (source.Velocity.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 velocity = source.Velocity;
        Vector3 normalized = velocity.normalized;
        Vector3Int direction = new Vector3Int(
            Mathf.RoundToInt(normalized.x),
            Mathf.RoundToInt(normalized.y),
            Mathf.RoundToInt(normalized.z)
        );

        float dominantComponent = velocity.magnitude;

        if (direction == Vector3Int.zero)
        {
            Vector3 absVelocity = new Vector3(Mathf.Abs(velocity.x), Mathf.Abs(velocity.y), Mathf.Abs(velocity.z));
            if (absVelocity.x >= absVelocity.y && absVelocity.x >= absVelocity.z)
            {
                direction = velocity.x >= 0f ? Vector3Int.right : Vector3Int.left;
            }
            else if (absVelocity.y >= absVelocity.z)
            {
                direction = velocity.y >= 0f ? Vector3Int.up : Vector3Int.down;
            }
            else
            {
                direction = velocity.z >= 0f ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 0, -1);
            }
        }

        float calculatedTransfer = InternalCellCapacityLiters * dominantComponent * flowRateMultiplier * deltaTime;
        float remainingTransfer = Mathf.Min(calculatedTransfer, InternalCellCapacityLiters * Mathf.Max(1f, maxVelocityCascadeSteps));
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
        using var gravityScope = ApplyGravityTransferMarker.Auto();
        float rate = Mathf.Max(0.1f, source.Definition.downwardCellVolumesPerSecond) / Mathf.Max(0.01f, source.Definition.viscosity);
        float calculatedTransfer = InternalCellCapacityLiters * rate * flowRateMultiplier * deltaTime;
        // 1回のTickで伝播できる最大量を制限し、一瞬で水が抜け落ちる現象を防止
        float maxTransfer = InternalCellCapacityLiters * Mathf.Max(1f, maxVerticalCascadeSteps);
        float remainingTransfer = Mathf.Min(calculatedTransfer, maxTransfer);

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
        using var lateralScope = ApplyLateralTransferMarker.Auto();
        if (HasDownwardCapacity(sourcePos, source.Definition))
        {
            return false;
        }

        Vector3Int[] lateralDirections = GetLateralDirections();
        if (lateralDirections.Length == 0)
        {
            return false;
        }

        lateralCandidateBuffer.Clear();
        List<LateralCandidate> candidates = lateralCandidateBuffer;
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

        candidates.Sort(CompareLateralCandidateByFill);

        bool changed = false;
        float rate = Mathf.Max(0f, source.Definition.lateralCellVolumesPerSecond) / Mathf.Max(0.01f, source.Definition.viscosity);
        float calculatedTransfer = capacity * rate * flowRateMultiplier * deltaTime;
        // 1Tickでの横移動の広がり過ぎを制限
        float remainingTransfer = Mathf.Min(calculatedTransfer, capacity * Mathf.Max(1f, maxVelocityCascadeSteps));

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
        using var transferScope = TransferLitersMarker.Auto();
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

        // Freeze-prevention: restrict how wide the BFS can spread trying to find an empty cell
        int maxSearchCells = 2000; 
        int searchedCount = 0;

        while (fillQueue.Count > 0 && remaining > MinLitersEpsilon && searchedCount < maxSearchCells)
        {
            Vector3Int current = fillQueue.Dequeue();
            searchedCount++;

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
}
