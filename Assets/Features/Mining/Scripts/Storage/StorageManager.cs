using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 地上の貯蔵庫を管理するクラス
/// トロッコからリソースを受け取り、総量を保持する
/// </summary>
public class StorageManager : MonoBehaviour
{
    // シングルトンインスタンス
    private static StorageManager _instance;
    public static StorageManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<StorageManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("StorageManager");
                    _instance = go.AddComponent<StorageManager>();
                }
            }
            return _instance;
        }
    }

    // 貯蔵されているリソース
    private Dictionary<ResourceType, int> storedResources = new Dictionary<ResourceType, int>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            StorageManager previousInstance = _instance;
            if (previousInstance.storedResources != null)
            {
                storedResources = new Dictionary<ResourceType, int>(previousInstance.storedResources);
            }

            if (previousInstance.gameObject == gameObject)
            {
                Destroy(previousInstance);
            }
            else
            {
                Destroy(previousInstance.gameObject);
            }
        }
        _instance = this;
        
        // GameDataPersistenceManagerからリソースをロード
        if (GameDataPersistenceManager.Instance.storedResources != null)
        {
            storedResources = new Dictionary<ResourceType, int>(GameDataPersistenceManager.Instance.storedResources);
        }

        // 全てのリソースタイプを0で初期化（もし永続化データになければ）
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (!storedResources.ContainsKey(type))
            {
                storedResources[type] = 0;
            }
        }
    }

    /// <summary>
    /// 貯蔵庫にリソースを追加する
    /// </summary>
    /// <param name="resourcesToAdd">追加するリソースのDictionary</param>
    public void AddResources(Dictionary<ResourceType, int> resourcesToAdd)
    {
        if (resourcesToAdd == null) return;

        foreach (var resource in resourcesToAdd)
        {
            if (storedResources.ContainsKey(resource.Key))
            {
                storedResources[resource.Key] += resource.Value;
            }
        }
        
        // 永続化データも更新
        GameDataPersistenceManager.Instance.storedResources = new Dictionary<ResourceType, int>(storedResources);

        // 現在の貯蔵量を表示
        string storageInfo = "[StorageManager] 現在の貯蔵量: ";
        foreach (var resource in storedResources)
        {
            if (resource.Value > 0)
            {
                storageInfo += $"{resource.Key}: {resource.Value} ";
            }
        }
        Debug.Log(storageInfo);
    }

    /// <summary>
    /// 貯蔵庫に単一のリソースを追加する
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <param name="amount">追加する量</param>
    public void AddResource(ResourceType type, int amount)
    {
        if (storedResources.ContainsKey(type))
        {
            storedResources[type] += amount;
        }
        else
        {
            storedResources[type] = amount;
        }
        GameDataPersistenceManager.Instance.storedResources = new Dictionary<ResourceType, int>(storedResources);

        // 現在の貯蔵量を表示
        string storageInfo = "[StorageManager] 現在の貯蔵量: ";
        foreach (var resource in storedResources)
        {
            if (resource.Value > 0)
            {
                storageInfo += $"{resource.Key}: {resource.Value} ";
            }
        }
        Debug.Log(storageInfo);
    }

    /// <summary>
    /// 指定されたリソースの現在の貯蔵量を取得する
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <returns>貯蔵量</returns>
    public bool CanSpendResources(Dictionary<ResourceType, int> resourcesToSpend)
    {
        if (!ValidateSpendRequest(resourcesToSpend))
        {
            return false;
        }

        foreach (KeyValuePair<ResourceType, int> resource in resourcesToSpend)
        {
            if (!storedResources.ContainsKey(resource.Key))
            {
                Debug.LogError($"StorageManager: resource '{resource.Key}' is not initialized.");
                return false;
            }

            if (storedResources[resource.Key] < resource.Value)
            {
                return false;
            }
        }

        return true;
    }

    public bool TrySpendResources(Dictionary<ResourceType, int> resourcesToSpend)
    {
        if (!CanSpendResources(resourcesToSpend))
        {
            return false;
        }

        foreach (KeyValuePair<ResourceType, int> resource in resourcesToSpend)
        {
            storedResources[resource.Key] -= resource.Value;
        }

        GameDataPersistenceManager.Instance.storedResources = new Dictionary<ResourceType, int>(storedResources);
        return true;
    }

    public int GetResourceAmount(ResourceType type)
    {
        if (storedResources.ContainsKey(type))
        {
            return storedResources[type];
        }
        return 0;
    }

    /// <summary>
    /// 全てのリソースの貯蔵量を取得する
    /// </summary>
    /// <returns>リソースと量のDictionary</returns>
    public Dictionary<ResourceType, int> GetAllStoredResources()
    {
        return new Dictionary<ResourceType, int>(storedResources);
    }

    private bool ValidateSpendRequest(Dictionary<ResourceType, int> resourcesToSpend)
    {
        if (storedResources == null)
        {
            Debug.LogError("StorageManager: storedResources is not initialized.");
            return false;
        }

        if (resourcesToSpend == null || resourcesToSpend.Count == 0)
        {
            Debug.LogError("StorageManager: resourcesToSpend is not configured.");
            return false;
        }

        foreach (KeyValuePair<ResourceType, int> resource in resourcesToSpend)
        {
            if (resource.Value <= 0)
            {
                Debug.LogError($"StorageManager: spend amount for '{resource.Key}' must be greater than zero.");
                return false;
            }
        }

        return true;
    }
}
