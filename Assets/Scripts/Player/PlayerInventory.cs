using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// プレイヤーのインベントリ管理クラス
/// リソースの追加、削除、容量管理を担当
/// </summary>
[System.Serializable]
public class PlayerInventory
{
    [Header("インベントリ設定")]
    public int maxCapacity = 200; // プレイヤーが持てる最大数
    
    [SerializeField] private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    
    // イベント通知用
    public event Action<ResourceType, int> OnResourceAdded;
    public event Action<ResourceType, int> OnResourceRemoved;
    public event Action<int> OnTotalCountChanged;
    public event Action<bool> OnInventoryFullStateChanged;
    
    public PlayerInventory()
    {
        // 全リソースタイプを初期化
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
    }
    
    /// <summary>
    /// リソースを追加できるかチェック
    /// </summary>
    public bool CanAddResource(ResourceType type, int amount = 1)
    {
        return GetTotalItemCount() + amount <= maxCapacity;
    }
    
    /// <summary>
    /// リソースを追加
    /// </summary>
    public bool AddResource(ResourceType type, int amount = 1)
    {
        if (!CanAddResource(type, amount)) return false;
        
        bool wasEmpty = IsEmpty();
        int oldCount = GetTotalItemCount();
        
        resources[type] += amount;
        
        // イベント通知
        OnResourceAdded?.Invoke(type, amount);
        OnTotalCountChanged?.Invoke(GetTotalItemCount());
        
        if (wasEmpty && !IsEmpty())
        {
            OnInventoryFullStateChanged?.Invoke(false);
        }
        
        return true;
    }
    
    /// <summary>
    /// リソースを削除（戻り値は実際に削除した数）
    /// </summary>
    public int RemoveResource(ResourceType type, int amount = 1)
    {
        int currentAmount = resources[type];
        int removeAmount = Mathf.Min(currentAmount, amount);
        
        if (removeAmount > 0)
        {
            bool wasFull = GetTotalItemCount() >= maxCapacity;
            resources[type] -= removeAmount;
            
            // イベント通知
            OnResourceRemoved?.Invoke(type, removeAmount);
            OnTotalCountChanged?.Invoke(GetTotalItemCount());
            
            if (wasFull && GetTotalItemCount() < maxCapacity)
            {
                OnInventoryFullStateChanged?.Invoke(false);
            }
        }
        
        return removeAmount;
    }
    
    /// <summary>
    /// 総アイテム数を取得
    /// </summary>
    public int GetTotalItemCount()
    {
        int total = 0;
        foreach (var kvp in resources)
        {
            total += kvp.Value;
        }
        return total;
    }
    
    /// <summary>
    /// 特定リソースの数を取得
    /// </summary>
    public int GetResourceCount(ResourceType type)
    {
        return resources.ContainsKey(type) ? resources[type] : 0;
    }
    
    /// <summary>
    /// 全リソース情報を取得
    /// </summary>
    public Dictionary<ResourceType, int> GetAllResources()
    {
        return new Dictionary<ResourceType, int>(resources);
    }
    
    /// <summary>
    /// インベントリが空かチェック
    /// </summary>
    public bool IsEmpty()
    {
        return GetTotalItemCount() == 0;
    }
    
    /// <summary>
    /// インベントリが満杯かチェック
    /// </summary>
    public bool IsFull()
    {
        return GetTotalItemCount() >= maxCapacity;
    }
    
    /// <summary>
    /// インベントリをリセット
    /// </summary>
    public void Clear()
    {
        bool wasEmpty = IsEmpty();
        
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
        
        if (!wasEmpty)
        {
            OnTotalCountChanged?.Invoke(0);
            OnInventoryFullStateChanged?.Invoke(false);
        }
    }
    
    /// <summary>
    /// デバッグ用文字列表現
    /// </summary>
    public override string ToString()
    {
        var result = $"インベントリ ({GetTotalItemCount()}/{maxCapacity}): ";
        foreach (var kvp in resources)
        {
            if (kvp.Value > 0)
            {
                result += $"{kvp.Key}:{kvp.Value} ";
            }
        }
        return result;
    }
}
