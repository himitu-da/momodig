using System;
using UnityEngine;

public readonly struct VoxelCellKey : IEquatable<VoxelCellKey>
{
    public readonly Vector3Int blockPosition;
    public readonly Vector3Int localVoxelPosition;

    public VoxelCellKey(Vector3Int blockPosition, Vector3Int localVoxelPosition)
    {
        this.blockPosition = blockPosition;
        this.localVoxelPosition = localVoxelPosition;
    }

    public bool Equals(VoxelCellKey other)
    {
        return blockPosition == other.blockPosition && localVoxelPosition == other.localVoxelPosition;
    }

    public override bool Equals(object obj)
    {
        return obj is VoxelCellKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (blockPosition.GetHashCode() * 397) ^ localVoxelPosition.GetHashCode();
        }
    }
}
