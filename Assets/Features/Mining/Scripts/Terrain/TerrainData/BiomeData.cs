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

    public float EvaluateWeight(Vector3Int blockPosition)
    {
        float depthWeight = depthCurve != null ? depthCurve.Evaluate(blockPosition.y) : 1f;
        float horizontalWeight = horizontalCurve != null ? horizontalCurve.Evaluate(blockPosition.x) : 1f;
        return Mathf.Max(0f, baseWeight * depthWeight * horizontalWeight);
    }
}
