using UnityEngine;

public static class PassageTransitionContext
{
    private const float AreaSizeTolerance = 0.01f;

    private static bool hasPendingTransition;
    private static string sourceSceneName;
    private static string targetSceneName;
    private static Vector3 normalizedPosition;
    private static Vector3 sourceAreaSize;

    public static bool HasPendingTransition => hasPendingTransition;

    public static void Begin(string sourceScene, string targetScene, BoxCollider sourceTransitionArea, Vector3 sourceWorldPosition)
    {
        if (string.IsNullOrEmpty(sourceScene) || string.IsNullOrEmpty(targetScene) || sourceTransitionArea == null)
        {
            Debug.LogError("PassageTransitionContext: Transition area context is not configured.");
            hasPendingTransition = false;
            return;
        }

        sourceSceneName = sourceScene;
        targetSceneName = targetScene;
        sourceAreaSize = sourceTransitionArea.size;
        normalizedPosition = WorldToNormalizedBoxPoint(sourceTransitionArea, sourceWorldPosition);
        hasPendingTransition = true;
    }

    public static bool TryConsume(string expectedSourceSceneName, string expectedTargetSceneName, BoxCollider targetTransitionArea, out Vector3 targetWorldPosition)
    {
        targetWorldPosition = Vector3.zero;

        if (!hasPendingTransition ||
            sourceSceneName != expectedSourceSceneName ||
            targetSceneName != expectedTargetSceneName ||
            targetTransitionArea == null)
        {
            return false;
        }

        if (!ApproximatelySameSize(sourceAreaSize, targetTransitionArea.size))
        {
            Debug.LogError(
                $"PassageTransitionContext: Transition areas must have the same size. " +
                $"Source={sourceAreaSize}, Target={targetTransitionArea.size}"
            );
            Clear();
            return false;
        }

        Vector3 targetNormalizedPosition = normalizedPosition;
        targetNormalizedPosition.z = 1f - targetNormalizedPosition.z;
        targetWorldPosition = NormalizedBoxPointToWorld(targetTransitionArea, targetNormalizedPosition);
        Clear();
        return true;
    }

    public static void Clear()
    {
        hasPendingTransition = false;
        sourceSceneName = string.Empty;
        targetSceneName = string.Empty;
        normalizedPosition = Vector3.zero;
        sourceAreaSize = Vector3.zero;
    }

    private static Vector3 WorldToNormalizedBoxPoint(BoxCollider boxCollider, Vector3 worldPosition)
    {
        Vector3 localPosition = boxCollider.transform.InverseTransformPoint(worldPosition);
        Vector3 size = boxCollider.size;
        Vector3 center = boxCollider.center;

        return new Vector3(
            NormalizeAxis(localPosition.x, center.x, size.x),
            NormalizeAxis(localPosition.y, center.y, size.y),
            NormalizeAxis(localPosition.z, center.z, size.z)
        );
    }

    private static Vector3 NormalizedBoxPointToWorld(BoxCollider boxCollider, Vector3 normalized)
    {
        Vector3 size = boxCollider.size;
        Vector3 center = boxCollider.center;
        Vector3 localPosition = new Vector3(
            DenormalizeAxis(normalized.x, center.x, size.x),
            DenormalizeAxis(normalized.y, center.y, size.y),
            DenormalizeAxis(normalized.z, center.z, size.z)
        );

        return boxCollider.transform.TransformPoint(localPosition);
    }

    private static float NormalizeAxis(float value, float center, float size)
    {
        if (Mathf.Approximately(size, 0f))
        {
            Debug.LogError("PassageTransitionContext: Transition area size cannot be zero.");
            return 0.5f;
        }

        return Mathf.Clamp01(((value - center) / size) + 0.5f);
    }

    private static float DenormalizeAxis(float normalized, float center, float size)
    {
        return center + ((Mathf.Clamp01(normalized) - 0.5f) * size);
    }

    private static bool ApproximatelySameSize(Vector3 first, Vector3 second)
    {
        return Mathf.Abs(first.x - second.x) <= AreaSizeTolerance
            && Mathf.Abs(first.y - second.y) <= AreaSizeTolerance
            && Mathf.Abs(first.z - second.z) <= AreaSizeTolerance;
    }
}
