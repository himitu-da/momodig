using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

/// <summary>
/// GameObjectにアタッチして、強化可能にするコンポーネント。
/// 関連するコンポーネントが持つStatオブジェクトにEnhancementを適用します。
/// </summary>
public class Enhanceable : MonoBehaviour
{
    [Tooltip("このオブジェクトに適用する強化のリスト")]
    public List<Enhancement> enhancements = new List<Enhancement>();

    void Awake()
    {
        ApplyEnhancements();
    }

    /// <summary>
    /// このコンポーネントに登録されているすべての強化を適用します。
    /// </summary>
    public void ApplyEnhancements()
    {
        foreach (var enhancement in enhancements)
        {
            if (enhancement == null) continue;
            ApplyEnhancement(enhancement);
        }
    }

    /// <summary>
    /// 指定された単一の強化を適用します。
    /// 同じGameObjectにアタッチされている他のコンポーネントから、
    /// 名前に一致するStatフィールドまたはプロパティを探して補正を適用します。
    /// </summary>
    /// <param name="enhancement">適用する強化</param>
    public void ApplyEnhancement(Enhancement enhancement)
    {
        // このGameObjectが持つすべてのコンポーネントを取得
        var components = GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == this) continue; // 自分自身はスキップ

            var type = component.GetType();
            // フィールドを探す
            FieldInfo field = type.GetField(enhancement.TargetStatName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(Stat))
            {
                Stat stat = (Stat)field.GetValue(component);
                ApplyModifier(stat, enhancement);
                return; // 見つかったら終了
            }

            // プロパティを探す
            PropertyInfo prop = type.GetProperty(enhancement.TargetStatName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(Stat))
            {
                Stat stat = (Stat)prop.GetValue(component);
                ApplyModifier(stat, enhancement);
                return; // 見つかったら終了
            }
        }

        Debug.LogWarning($"Stat '{enhancement.TargetStatName}' not found on GameObject '{gameObject.name}'.", this);
    }

    private void ApplyModifier(Stat stat, Enhancement enhancement)
    {
        if (stat == null) return;

        switch (enhancement.Type)
        {
            case EnhancementType.Additive:
                stat.AddAdditiveModifier(enhancement.Value);
                break;
            case EnhancementType.Multiplicative:
                stat.AddMultiplicativeModifier(enhancement.Value);
                break;
        }
    }
}
