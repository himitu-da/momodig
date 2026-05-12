using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New BiomeData", menuName = "Momodig/Biome Data")]
public class BiomeData : ScriptableObject
{
    [Header("Biome Settings")]
    public string biomeName;
    public BiomeType biomeType;

    [Header("Generation Rules")]
    public int maxHeight;
    public int minHeight;

    [Header("Generation Rule Stack")]
    public List<TerrainGenerationRule> generationRules;

    [Header("Visuals")]
    public Texture2D backgroundTexture;
}

public enum TerrainGenerationResultType
{
    Block,
    Clear,
    NoOp
}

public enum TerrainSpecialWeightFunction
{
    None,
    CurvedBoundarySigmoid,
    CurvedBoundaryInverseSigmoid
}

[System.Serializable]
public class TerrainGenerationRule
{
    public string ruleName;
    public List<TerrainGenerationEntry> entries;
}

[System.Serializable]
public class TerrainGenerationEntry
{
    public TerrainGenerationResultType resultType;
    public BlockData blockData;
    public float baseWeight = 1f;
    public AnimationCurve depthCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public AnimationCurve horizontalCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public TerrainSpecialWeightFunction specialWeightFunction = TerrainSpecialWeightFunction.None;
    public float specialFunctionK = 1f;

    public float EvaluateWeight(Vector3Int blockPosition)
    {
        float depthWeight = depthCurve != null ? depthCurve.Evaluate(blockPosition.y) : 1f;
        float horizontalWeight = horizontalCurve != null ? horizontalCurve.Evaluate(blockPosition.x) : 1f;
        float specialWeight = TerrainSpecialWeightFunctions.Evaluate(specialWeightFunction, blockPosition, specialFunctionK);
        return Mathf.Max(0f, baseWeight * depthWeight * horizontalWeight * specialWeight);
    }
}

public static class TerrainSpecialWeightFunctions
{
    public static float Evaluate(TerrainSpecialWeightFunction function, Vector3Int blockPosition, float k)
    {
        switch (function)
        {
            case TerrainSpecialWeightFunction.CurvedBoundarySigmoid:
                return EvaluateCurvedBoundarySigmoid(blockPosition.x, blockPosition.y, k);
            case TerrainSpecialWeightFunction.CurvedBoundaryInverseSigmoid:
                return EvaluateCurvedBoundaryInverseSigmoid(blockPosition.x, blockPosition.y, k);
            default:
                return 1f;
        }
    }

    private static float EvaluateCurvedBoundarySigmoid(float x, float y, float k)
    {
        return ApplyThreshold(EvaluateCurvedBoundarySigmoidRaw(x, y, k));
    }

    private static float EvaluateCurvedBoundaryInverseSigmoid(float x, float y, float k)
    {
        return ApplyThreshold(1f - EvaluateCurvedBoundarySigmoidRaw(x, y, k));
    }

    private static float EvaluateCurvedBoundarySigmoidRaw(float x, float y, float k)
    {
        float normalizedAbsX = Mathf.Abs(x / 50f);
        float poweredAbsX = Mathf.Pow(normalizedAbsX, 2.5f);
        float denominator = 1f + poweredAbsX;
        float boundaryY = 1000f / denominator - 990f;

        float slopeNumerator = -x * Mathf.Pow(normalizedAbsX, 0.5f);
        float slopeDenominator = denominator * denominator;
        float slope = slopeDenominator > 0f ? slopeNumerator / slopeDenominator : 0f;
        float signedDistance = (boundaryY - y) / Mathf.Sqrt(1f + slope * slope);

        float exponent = Mathf.Clamp(k * signedDistance, -60f, 60f);
        return 1f / (1f + Mathf.Pow(2f, exponent));
    }

    private static float ApplyThreshold(float value)
    {
        if (value <= 0.1f)
        {
            return 0f;
        }

        if (value >= 0.9f)
        {
            return 1f;
        }

        return value;
    }
}
