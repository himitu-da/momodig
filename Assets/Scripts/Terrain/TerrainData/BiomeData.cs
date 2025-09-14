using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New BiomeData", menuName = "Momodig/Biome Data")]
public class BiomeData : ScriptableObject
{
    [Header("Biome Settings")]
    public string biomeName;      // バイオームの名前（例：「地表」「ジャングル」）
    public BiomeType biomeType;   // バイオームのタイプ

    [Header("Generation Rules")]
    // 主にAltitudeBasedタイプで使用
    public int maxHeight;         // このバイオームが適用される最大高度
    public int minHeight;         // このバイオームが適用される最小高度


    [Header("Block Composition")]
    // このバイオームで生成されるブロックのリスト
    public List<BlockDistribution> availableBlocks;
}

/// <summary>
/// 特定のバイオーム内でのブロックの分布設定
/// </summary>
[System.Serializable]
public class BlockDistribution
{
    public BlockData blockData;
    
    [Tooltip("バイオーム内の相対的な深さ（0=maxHeight, 1=minHeight）に応じた生成の重みを定義します。")]
    public AnimationCurve distributionCurve = AnimationCurve.Linear(0, 1, 1, 1); // デフォルトは常に重み1
}
