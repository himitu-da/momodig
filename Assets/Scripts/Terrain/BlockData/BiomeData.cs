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
    public int minHeight;         // このバイオームが適用される最小高度
    public int maxHeight;         // このバイオームが適用される最大高度

    [Header("Block Composition")]
    // このバイオームで生成されるブロックのリスト
    public List<BlockData> availableBlocks;
}
