using UnityEngine;

/// <summary>
/// 資源の種類を定義するenum
/// プロジェクト全体で共通して使用される
/// </summary>
public enum ResourceType
{
    Stone,
    Iron,
    Gold,
    Diamond
}

/// <summary>
/// ResourceTypeに関連するユーティリティメソッド
/// </summary>
public static class ResourceTypeUtility
{
    /// <summary>
    /// ResourceTypeに対応する色を取得
    /// </summary>
    /// <param name="resourceType">資源タイプ</param>
    /// <returns>対応する色</returns>
    public static Color GetResourceColor(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.Stone: 
                return Color.gray;
            case ResourceType.Iron: 
                return new Color(0.8f, 0.4f, 0.2f); // 鉄の色
            case ResourceType.Gold: 
                return Color.yellow;
            case ResourceType.Diamond: 
                return Color.cyan;
            default: 
                return Color.white;
        }
    }

    /// <summary>
    /// ResourceTypeの表示名を取得
    /// </summary>
    /// <param name="resourceType">資源タイプ</param>
    /// <returns>表示名</returns>
    public static string GetDisplayName(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.Stone: return "石";
            case ResourceType.Iron: return "鉄";
            case ResourceType.Gold: return "金";
            case ResourceType.Diamond: return "ダイヤモンド";
            default: return resourceType.ToString();
        }
    }

    /// <summary>
    /// ResourceTypeの基本価値を取得
    /// </summary>
    /// <param name="resourceType">資源タイプ</param>
    /// <returns>基本価値</returns>
    public static int GetBaseValue(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.Stone: return 1;
            case ResourceType.Iron: return 3;
            case ResourceType.Gold: return 10;
            case ResourceType.Diamond: return 50;
            default: return 1;
        }
    }
}
