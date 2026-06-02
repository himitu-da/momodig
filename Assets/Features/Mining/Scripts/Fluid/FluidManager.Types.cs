using UnityEngine;

public partial class FluidManager
{
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

    private static int CompareLateralCandidatesByFill(LateralCandidate a, LateralCandidate b)
    {
        return a.TargetFill.CompareTo(b.TargetFill);
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
