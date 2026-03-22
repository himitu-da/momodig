using UnityEngine;

/// <summary>
/// Vector3型のステータスを管理するクラス。
/// X, Y, Zそれぞれを個別のStatとして持ち、個別に強化できるようにします。
/// </summary>
[System.Serializable]
public class StatVector3
{
    public Stat X = new Stat();
    public Stat Y = new Stat();
    public Stat Z = new Stat();

    /// <summary>
    /// すべての補正を適用した後の最終的なVector3値。
    /// </summary>
    public Vector3 Value => new Vector3(X.Value, Y.Value, Z.Value);

    /// <summary>
    /// 基本値を設定します。
    /// </summary>
    public void SetBaseValue(Vector3 baseValue)
    {
        X.BaseValue = baseValue.x;
        Y.BaseValue = baseValue.y;
        Z.BaseValue = baseValue.z;
    }

    /// <summary>
    /// すべての補正（加算・乗算）をクリアします。
    /// </summary>
    public void RemoveAllModifiers()
    {
        X.RemoveAllModifiers();
        Y.RemoveAllModifiers();
        Z.RemoveAllModifiers();
    }
}
