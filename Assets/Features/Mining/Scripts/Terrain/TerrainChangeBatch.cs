using System.Collections.Generic;

public enum TerrainChangeReason
{
    Unknown,
    Digging,
    Explosion,
    Solidification,
    Restore
}

public sealed class TerrainChangeBatch
{
    public readonly int version;
    public readonly TerrainChangeReason reason;
    public readonly List<VoxelCellKey> removedSolidCells = new List<VoxelCellKey>();
    public readonly List<VoxelCellKey> addedSolidCells = new List<VoxelCellKey>();

    public TerrainChangeBatch(int version, TerrainChangeReason reason)
    {
        this.version = version;
        this.reason = reason;
    }

    public bool HasChanges => removedSolidCells.Count > 0 || addedSolidCells.Count > 0;
}
