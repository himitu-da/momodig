using UnityEngine;

/// <summary>
/// 強化の種類（加算か乗算か）
/// </summary>
public enum EnhancementType
{
    Additive,
    Multiplicative
}

/// <summary>
/// 個々の強化内容を定義するScriptableObject。
/// 例：「移動速度を5上げる」「攻撃力を10%上げる」など。
/// </summary>
[CreateAssetMenu(fileName = "NewEnhancement", menuName = "Enhancement System/Enhancement")]
public class Enhancement : ScriptableObject
{
    [Tooltip("強化を適用する対象のStatの名前")]
    public string TargetStatName;

    [Tooltip("強化の種類（加算または乗算）")]
    public EnhancementType Type;

    [Tooltip("強化する値。乗算の場合は1.1で10%増、0.9で10%減。")]
    public float Value;
}
