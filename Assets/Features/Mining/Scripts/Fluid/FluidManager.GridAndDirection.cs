using UnityEngine;

public partial class FluidManager
{
    private static readonly Vector3Int[] FillDirectionsNegX =
    {
        Vector3Int.left,
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        Vector3Int.right
    };
    private static readonly Vector3Int[] FillDirectionsNegZ =
    {
        new Vector3Int(0, 0, -1),
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1)
    };
    private static readonly Vector3Int[] FillDirectionsNegY =
    {
        Vector3Int.down,
        Vector3Int.right, Vector3Int.left,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        Vector3Int.up
    };

    private Vector3Int[] GetPreferredFillDirections()
    {
        switch (gravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return FillDirectionsNegX;
            case FluidGravityAxis.NegativeZ:
                return FillDirectionsNegZ;
            default:
                return FillDirectionsNegY;
        }
    }

    private static readonly Vector3Int[] AllNeighborDirections =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    private Vector3Int[] GetAllNeighborDirections()
    {
        return AllNeighborDirections;
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

    private static readonly Vector3Int[] LateralDirectionsNegX =
    {
        Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };
    private static readonly Vector3Int[] LateralDirectionsNegZ =
    {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up, Vector3Int.down
    };
    private static readonly Vector3Int[] LateralDirectionsNegY =
    {
        Vector3Int.right, Vector3Int.left,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    private Vector3Int[] GetLateralDirections()
    {
        switch (gravityAxis)
        {
            case FluidGravityAxis.NegativeX:
                return LateralDirectionsNegX;
            case FluidGravityAxis.NegativeZ:
                return LateralDirectionsNegZ;
            default:
                return LateralDirectionsNegY;
        }
    }

    private void QueueCellNeighborhood(Vector3Int center, int radius)
    {
        using var queueScope = QueueCellNeighborhoodMarker.Auto();
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

    private void InvalidateTerrainSolidCacheNeighborhood(Vector3Int center, int radius)
    {
        if (terrainSolidCache.Count == 0)
        {
            return;
        }

        int safeRadius = Mathf.Max(0, radius);
        for (int x = -safeRadius; x <= safeRadius; x++)
        {
            for (int y = -safeRadius; y <= safeRadius; y++)
            {
                for (int z = -safeRadius; z <= safeRadius; z++)
                {
                    terrainSolidCache.Remove(new Vector3Int(center.x + x, center.y + y, center.z + z));
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
            Debug.Log($"FluidManager: version updated to {Version}, active cells={cells.Count}");
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
}
