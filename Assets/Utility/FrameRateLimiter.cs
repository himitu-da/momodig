using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [SerializeField, Min(1)] private int targetFrameRate = 60;
    [SerializeField, Range(0, 4)] private int vSyncCount = 0;

    private void Awake()
    {
        if (targetFrameRate < 1)
        {
            Debug.LogError($"{nameof(FrameRateLimiter)}: Target frame rate must be 1 or higher.", this);
            return;
        }

        QualitySettings.vSyncCount = vSyncCount;
        Application.targetFrameRate = targetFrameRate;

        Debug.Log($"{nameof(FrameRateLimiter)}: Target frame rate set to {targetFrameRate} fps. vSyncCount={vSyncCount}.", this);
    }
}
