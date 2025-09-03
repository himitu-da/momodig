using System;
using System.Collections.Generic;

/// <summary>
/// インベントリ機能を定義するインターフェース
/// </summary>
public interface IInventory
{
    /// <summary>
    /// 最大容量
    /// </summary>
    int maxCapacity { get; }
    
    /// <summary>
    /// リソースを追加できるかチェック
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <param name="amount">追加する数量</param>
    /// <returns>追加可能な場合true</returns>
    bool CanAddResource(ResourceType type, int amount = 1);
    
    /// <summary>
    /// リソースを追加
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <param name="amount">追加する数量</param>
    /// <returns>追加に成功した場合true</returns>
    bool AddResource(ResourceType type, int amount = 1);
    
    /// <summary>
    /// 総アイテム数を取得
    /// </summary>
    /// <returns>総アイテム数</returns>
    int GetTotalItemCount();
    
    /// <summary>
    /// 特定リソースの数を取得
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <returns>リソース数</returns>
    int GetResourceCount(ResourceType type);
    
    /// <summary>
    /// リソースを削除
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <param name="amount">削除する数量</param>
    /// <returns>実際に削除された数量</returns>
    int RemoveResource(ResourceType type, int amount = 1);
    
    /// <summary>
    /// インベントリが空かどうかをチェック
    /// </summary>
    /// <returns>空の場合true</returns>
    bool IsEmpty();
    
    /// <summary>
    /// 全リソース情報を取得
    /// </summary>
    /// <returns>リソースタイプと数量の辞書</returns>
    Dictionary<ResourceType, int> GetAllResources();
    
    /// <summary>
    /// リソースが追加されたときのイベント
    /// </summary>
    event Action<ResourceType, int> OnResourceAdded;
    
    /// <summary>
    /// 総数が変更されたときのイベント
    /// </summary>
    event Action<int> OnTotalCountChanged;
}
