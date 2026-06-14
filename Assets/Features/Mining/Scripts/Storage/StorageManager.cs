using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 地上の貯蔵庫を管理するクラス
/// トロッコからリソースを受け取り、総量を保持する
/// </summary>
public class StorageManager : MonoBehaviour
{
    [SerializeField] private GameDataPersistenceManager persistenceManager;
    [SerializeField] private List<MiningTool> knownTools = new List<MiningTool>();
    [SerializeField] private List<MiningTool> starterOwnedTools = new List<MiningTool>();

    // シングルトンインスタンス
    private static StorageManager _instance;
    public static StorageManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("StorageManager.Instance is not initialized. Place StorageManager in the active scene.");
            }

            return _instance;
        }
    }

    // 貯蔵されているリソース
    private Dictionary<ResourceType, int> storedResources = new Dictionary<ResourceType, int>();
    private readonly List<string> ownedToolIds = new List<string>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (_instance.gameObject.scene == gameObject.scene)
            {
                Debug.LogError("Multiple StorageManager instances exist in the same scene. Remove the duplicate from the scene.", this);
                Destroy(gameObject);
                return;
            }
        }

        _instance = this;
        
        persistenceManager = ResolvePersistenceManager();
        if (persistenceManager != null && persistenceManager.storedResources != null)
        {
            storedResources = new Dictionary<ResourceType, int>(persistenceManager.storedResources);
        }

        NormalizeStoredResources();
        LoadOwnedToolsFromPersistence();
        SeedStarterOwnedToolsIfNeeded();
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
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

    public List<MiningTool> GetOwnedTools()
    {
        List<MiningTool> tools = new List<MiningTool>();
        for (int i = 0; i < ownedToolIds.Count; i++)
        {
            MiningTool tool = ResolveKnownTool(ownedToolIds[i]);
            if (tool == null)
            {
                Debug.LogError($"StorageManager: owned toolId '{ownedToolIds[i]}' is not registered in knownTools.", this);
                continue;
            }

            tools.Add(tool);
        }

        return tools;
    }

    public bool OwnsTool(MiningTool tool)
    {
        string toolId = GameDataPersistenceManager.GetToolId(tool);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            Debug.LogError("StorageManager: tool is not configured.", this);
            return false;
        }

        return ownedToolIds.Contains(toolId);
    }

    public bool AddOwnedTool(MiningTool tool)
    {
        if (!ValidateKnownTool(tool))
        {
            return false;
        }

        string toolId = GameDataPersistenceManager.GetToolId(tool);
        if (ownedToolIds.Contains(toolId))
        {
            return true;
        }

        ownedToolIds.Add(toolId);
        PersistOwnedTools();
        return true;
    }

    public void PersistOwnedTools()
    {
        GameDataPersistenceManager resolvedPersistenceManager = ResolvePersistenceManager();
        if (resolvedPersistenceManager == null)
        {
            return;
        }

        resolvedPersistenceManager.ownedToolIds = new List<string>(ownedToolIds);
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

    private void LoadOwnedToolsFromPersistence()
    {
        ownedToolIds.Clear();
        GameDataPersistenceManager resolvedPersistenceManager = ResolvePersistenceManager();
        if (resolvedPersistenceManager == null)
        {
            return;
        }

        if (resolvedPersistenceManager.ownedToolIds == null)
        {
            resolvedPersistenceManager.ownedToolIds = new List<string>();
        }

        for (int i = 0; i < resolvedPersistenceManager.ownedToolIds.Count; i++)
        {
            string toolId = resolvedPersistenceManager.ownedToolIds[i];
            if (string.IsNullOrWhiteSpace(toolId))
            {
                Debug.LogError($"StorageManager: persisted ownedToolIds contains an empty value at index {i}.", this);
                continue;
            }

            if (ownedToolIds.Contains(toolId))
            {
                Debug.LogError($"StorageManager: persisted ownedToolIds contains duplicate value '{toolId}'.", this);
                continue;
            }

            if (ResolveKnownTool(toolId) == null)
            {
                Debug.LogError($"StorageManager: persisted owned toolId '{toolId}' is not registered in knownTools.", this);
                continue;
            }

            ownedToolIds.Add(toolId);
        }
    }

    private void SeedStarterOwnedToolsIfNeeded()
    {
        if (ownedToolIds.Count > 0)
        {
            return;
        }

        if (starterOwnedTools == null || starterOwnedTools.Count == 0)
        {
            Debug.LogError("StorageManager: starterOwnedTools is not configured.", this);
            return;
        }

        bool changed = false;
        for (int i = 0; i < starterOwnedTools.Count; i++)
        {
            MiningTool tool = starterOwnedTools[i];
            if (!ValidateKnownTool(tool))
            {
                continue;
            }

            string toolId = GameDataPersistenceManager.GetToolId(tool);
            if (!ownedToolIds.Contains(toolId))
            {
                ownedToolIds.Add(toolId);
                changed = true;
            }
        }

        if (changed)
        {
            PersistOwnedTools();
        }
    }

    private bool ValidateKnownTool(MiningTool tool)
    {
        if (tool == null)
        {
            Debug.LogError("StorageManager: tool is not configured.", this);
            return false;
        }

        string toolId = GameDataPersistenceManager.GetToolId(tool);
        if (string.IsNullOrWhiteSpace(toolId))
        {
            Debug.LogError("StorageManager: toolId is not configured.", tool);
            return false;
        }

        if (ResolveKnownTool(toolId) != tool)
        {
            Debug.LogError($"StorageManager: tool '{toolId}' is not registered in knownTools.", this);
            return false;
        }

        return true;
    }

    private MiningTool ResolveKnownTool(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId) || knownTools == null)
        {
            return null;
        }

        MiningTool resolved = null;
        for (int i = 0; i < knownTools.Count; i++)
        {
            MiningTool candidate = knownTools[i];
            if (candidate == null)
            {
                Debug.LogError($"StorageManager: knownTools contains a null tool at index {i}.", this);
                continue;
            }

            string candidateId = GameDataPersistenceManager.GetToolId(candidate);
            if (candidateId != toolId)
            {
                continue;
            }

            if (resolved != null)
            {
                Debug.LogError($"StorageManager: knownTools contains duplicate toolId '{toolId}'.", this);
                return null;
            }

            resolved = candidate;
        }

        return resolved;
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
