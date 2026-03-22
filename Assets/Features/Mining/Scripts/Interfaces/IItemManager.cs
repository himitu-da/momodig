using UnityEngine;

/// <summary>
/// アイテム管理機能を定義するインターフェース
/// </summary>
public interface IItemManager
{
    /// <summary>
    /// アイテム起動時の半径倍率
    /// </summary>
    float WakeUpRadiusMultiplier { get; }
    
    /// <summary>
    /// アイテムをプールに返却
    /// </summary>
    /// <param name="itemObject">返却するアイテム</param>
    void ReturnItem(GameObject itemObject);
    
    /// <summary>
    /// 指定位置周辺のアイテムを起床させる
    /// </summary>
    /// <param name="position">中心位置</param>
    /// <param name="radius">起床半径</param>
    void WakeUpItemsNearPosition(Vector3 position, float radius);
}
