using UnityEngine;

public static class FluidSubmersionSampler
{
    public static float SampleBounds(
        FluidManager fluidManager,
        Bounds bounds,
        int horizontalSampleCount,
        int verticalSampleCount,
        int depthSampleCount,
        float sampleInset)
    {
        if (fluidManager == null)
        {
            return 0f;
        }

        Vector3 min = bounds.min + Vector3.one * Mathf.Max(0f, sampleInset);
        Vector3 max = bounds.max - Vector3.one * Mathf.Max(0f, sampleInset);
        ClampInvertedAxisToCenter(bounds, ref min, ref max);

        int safeHorizontalSamples = Mathf.Max(1, horizontalSampleCount);
        int safeVerticalSamples = Mathf.Max(1, verticalSampleCount);
        int safeDepthSamples = Mathf.Max(1, depthSampleCount);

        float totalFillRatio = 0f;
        int sampleCount = 0;

        for (int x = 0; x < safeHorizontalSamples; x++)
        {
            float sampleX = Mathf.Lerp(min.x, max.x, GetSampleLerp(x, safeHorizontalSamples));
            for (int y = 0; y < safeVerticalSamples; y++)
            {
                float sampleY = Mathf.Lerp(min.y, max.y, GetSampleLerp(y, safeVerticalSamples));
                for (int z = 0; z < safeDepthSamples; z++)
                {
                    float sampleZ = Mathf.Lerp(min.z, max.z, GetSampleLerp(z, safeDepthSamples));
                    totalFillRatio += fluidManager.GetFluidFillRatioAtWorldPosition(new Vector3(sampleX, sampleY, sampleZ));
                    sampleCount++;
                }
            }
        }

        return sampleCount > 0 ? Mathf.Clamp01(totalFillRatio / sampleCount) : 0f;
    }

    private static void ClampInvertedAxisToCenter(Bounds bounds, ref Vector3 min, ref Vector3 max)
    {
        if (min.x > max.x)
        {
            min.x = bounds.center.x;
            max.x = bounds.center.x;
        }

        if (min.y > max.y)
        {
            min.y = bounds.center.y;
            max.y = bounds.center.y;
        }

        if (min.z > max.z)
        {
            min.z = bounds.center.z;
            max.z = bounds.center.z;
        }
    }

    private static float GetSampleLerp(int index, int sampleCount)
    {
        if (sampleCount <= 1)
        {
            return 0.5f;
        }

        return index / (float)(sampleCount - 1);
    }
}
