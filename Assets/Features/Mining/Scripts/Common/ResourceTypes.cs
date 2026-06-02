using UnityEngine;

/// <summary>
/// 資源の種類を定義するenum
/// プロジェクト全体で共通して使用される
/// </summary>
public enum ResourceType
{
    Soil = 0,
    Stone = 1,
    Iron = 2,
    Gold = 3,
    Diamond = 4,
    Copper = 5,
    Tin = 6,
    Nickel = 7,
    Silicon = 8,
    Cobalt = 9,
    Titanium = 10,
    DragonGem = 11,
    Coal = 12
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
            case ResourceType.Soil: 
                return Color.brown;
            case ResourceType.Stone: 
                return Color.gray;
            case ResourceType.Iron: 
                return new Color(0.8f, 0.4f, 0.2f); // 鉄の色
            case ResourceType.Gold: 
                return Color.yellow;
            case ResourceType.Diamond: 
                return Color.cyan;
            case ResourceType.Copper:
                return new Color(0.7f, 0.3f, 0.1f); // 銅色
            case ResourceType.Tin:
                return new Color(0.8f, 0.8f, 0.85f); // 銀白色
            case ResourceType.Nickel:
                return new Color(0.75f, 0.75f, 0.7f); // やや暗い銀白色
            case ResourceType.Silicon:
                return new Color(0.5f, 0.5f, 0.6f); // 金属光沢のある濃い灰色
            case ResourceType.Cobalt:
                return new Color(0.2f, 0.4f, 0.8f); // 青みがかった銀色
            case ResourceType.Titanium:
                return new Color(0.6f, 0.6f, 0.65f); // 銀灰色の金属色
            case ResourceType.DragonGem:
                return Color.white; // 虹色表現はシェーダー等で別途対応
            case ResourceType.Coal:
                return new Color(0.08f, 0.08f, 0.08f);
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
            case ResourceType.Soil: return "土";
            case ResourceType.Stone: return "石";
            case ResourceType.Iron: return "鉄";
            case ResourceType.Gold: return "金";
            case ResourceType.Diamond: return "ダイヤモンド";
            case ResourceType.Copper: return "銅";
            case ResourceType.Tin: return "スズ";
            case ResourceType.Nickel: return "ニッケル";
            case ResourceType.Silicon: return "シリコン";
            case ResourceType.Cobalt: return "コバルト";
            case ResourceType.Titanium: return "チタン";
            case ResourceType.DragonGem: return "龍珠";
            case ResourceType.Coal: return "石炭";
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
            case ResourceType.Copper: return 2;
            case ResourceType.Soil: return 1;
            case ResourceType.Tin: return 2;
            case ResourceType.Nickel: return 4;
            case ResourceType.Silicon: return 5;
            case ResourceType.Cobalt: return 15;
            case ResourceType.Titanium: return 25;
            case ResourceType.DragonGem: return 200;
            case ResourceType.Coal: return 2;
            default: return 1;
        }
    }
}
