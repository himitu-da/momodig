using Unity.Profiling;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class FrameRateLimiter : MonoBehaviour
{
    private const int GlobalTargetFrameRate = 60;
    private const int GlobalVSyncCount = 0;
    private static readonly ProfilerMarker ApplyFrameRateLimitMarker = new ProfilerMarker("FrameRateLimiter.ApplyFrameRateLimit");

    [SerializeField, Min(1)] private int targetFrameRate = 60;
    [SerializeField, Range(0, 4)] private int vSyncCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyGlobalFrameRateLimit()
    {
        ApplyFrameRateLimit(GlobalTargetFrameRate, GlobalVSyncCount, null, "global runtime initialization");
    }

    private void Awake()
    {
        ApplyFrameRateLimit(targetFrameRate, vSyncCount, this, "scene component");
    }

    private static void ApplyFrameRateLimit(int targetFrameRate, int vSyncCount, Object context, string source)
    {
        using (ApplyFrameRateLimitMarker.Auto())
        {
            if (string.IsNullOrEmpty(source))
            {
                Debug.LogError($"{nameof(FrameRateLimiter)}: Apply source is not set.", context);
                return;
            }

            if (vSyncCount < 0 || vSyncCount > 4)
            {
                Debug.LogError($"{nameof(FrameRateLimiter)}: vSyncCount must be between 0 and 4. source={source}", context);
                return;
            }

            if (targetFrameRate < 1)
            {
                Debug.LogError($"{nameof(FrameRateLimiter)}: Target frame rate must be 1 or higher. source={source}", context);
                return;
            }

            QualitySettings.vSyncCount = vSyncCount;
            Application.targetFrameRate = targetFrameRate;
        }

        Debug.Log($"{nameof(FrameRateLimiter)}: Target frame rate set to {targetFrameRate} fps. vSyncCount={vSyncCount}. source={source}", context);
    }
}
