using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 個々のステータス（移動速度、攻撃力など）を管理するクラス。
/// 基本値に加算・乗算の補正を適用して最終的な値を計算します。
/// </summary>
public class Stat
{
    /// <summary>
    /// ステータスの基本値。
    /// </summary>
    public float BaseValue { get; set; }

    private readonly List<float> _additives = new List<float>();
    private readonly List<float> _multiplicatives = new List<float>();

    /// <summary>
    /// すべての補正を適用した後の最終的な値。
    /// 計算式: (BaseValue + ΣAdditives) * ΠMultiplicatives
    /// </summary>
    public float Value
    {
        get
        {
            float finalValue = BaseValue;
            finalValue += _additives.Sum();
            // 乗算モディファイアを適用
            // 例: 1.1 (10%増), 0.9 (10%減)
            _multiplicatives.ForEach(m => finalValue *= m);
            return finalValue;
        }
    }

    /// <summary>
    /// Valueを整数に丸めた値。ダメージ計算などに使用します。
    /// </summary>
    public int IntValue => Mathf.RoundToInt(Value);

    /// <summary>
    /// 加算補正を追加します。
    /// </summary>
    public void AddAdditiveModifier(float value)
    {
        _additives.Add(value);
    }

    /// <summary>
    /// 加算補正を削除します。
    /// </summary>
    public bool RemoveAdditiveModifier(float value)
    {
        return _additives.Remove(value);
    }

    /// <summary>
    /// 乗算補正を追加します。1.1fで10%増加、0.9fで10%減少。
    /// </summary>
    public void AddMultiplicativeModifier(float value)
    {
        _multiplicatives.Add(value);
    }

    /// <summary>
    /// 乗算補正を削除します。
    /// </summary>
    public bool RemoveMultiplicativeModifier(float value)
    {
        return _multiplicatives.Remove(value);
    }
}
