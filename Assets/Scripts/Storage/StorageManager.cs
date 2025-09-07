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
                _instance = FindObjectOfType<StorageManager>();
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
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 全てのリソースタイプを0で初期化
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
}
