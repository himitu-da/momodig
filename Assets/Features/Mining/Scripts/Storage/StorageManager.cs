using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 地上の貯蔵庫を管理するクラス
/// トロッコからリソースを受け取り、総量を保持する
/// </summary>
public class StorageManager : MonoBehaviour
{
    [SerializeField] private GameDataPersistenceManager persistenceManager;

    // シングルトンインスタンス
    private static StorageManager _instance;
    public static StorageManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("StorageManager.Instance is not initialized. Place StorageManager in the persistent scene.");
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
        
        persistenceManager = ResolvePersistenceManager();
        if (persistenceManager != null && persistenceManager.storedResources != null)
        {
            storedResources = new Dictionary<ResourceType, int>(persistenceManager.storedResources);
        }

        NormalizeStoredResources();
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
            if (!CanAddResource(resource.Key, resource.Value))
            {
                return;
            }
        }

        foreach (var resource in resourcesToAdd)
        {
            storedResources[resource.Key] += resource.Value;
        }
        
        // 永続化データも更新
        PersistStoredResources();

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
        if (!CanAddResource(type, amount))
        {
            return;
        }

        storedResources[type] += amount;
        PersistStoredResources();

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

        PersistStoredResources();
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

    private void NormalizeStoredResources()
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (!storedResources.ContainsKey(type))
            {
                storedResources[type] = 0;
                continue;
            }

            if (storedResources[type] < 0)
            {
                Debug.LogError($"StorageManager: stored amount for '{type}' was negative and has been clamped to zero.");
                storedResources[type] = 0;
            }
        }

        PersistStoredResources();
    }

    private GameDataPersistenceManager ResolvePersistenceManager()
    {
        if (persistenceManager != null)
        {
            return persistenceManager;
        }

        persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager == null)
        {
            Debug.LogError("StorageManager: GameDataPersistenceManager is not assigned.", this);
        }

        return persistenceManager;
    }

    private void PersistStoredResources()
    {
        GameDataPersistenceManager resolvedPersistenceManager = ResolvePersistenceManager();
        if (resolvedPersistenceManager == null)
        {
            return;
        }

        resolvedPersistenceManager.storedResources = new Dictionary<ResourceType, int>(storedResources);
    }

    private bool CanAddResource(ResourceType type, int amount)
    {
        if (storedResources == null)
        {
            Debug.LogError("StorageManager: storedResources is not initialized.");
            return false;
        }

        if (!storedResources.ContainsKey(type))
        {
            Debug.LogError($"StorageManager: resource '{type}' is not initialized.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogError($"StorageManager: add amount for '{type}' must be greater than zero. Use TrySpendResources for spending.");
            return false;
        }

        return true;
    }
}
