using UnityEngine;
using System.Collections.Generic;

public class DroppedItemManager : MonoBehaviour
{
    public static DroppedItemManager Instance { get; private set; }

    [SerializeField] private int defaultPoolSize = 1000;

    // Prefabをキーとして、オブジェクトのキューを管理するDictionary
    private Dictionary<GameObject, Queue<GameObject>> itemPools = new Dictionary<GameObject, Queue<GameObject>>();
    // 生成されたインスタンスがどのPrefabから作られたかを記録するDictionary
    private Dictionary<GameObject, GameObject> instancePrefabMap = new Dictionary<GameObject, GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    // 指定されたPrefabのプールを初期化（必要に応じて呼び出す）
    public void PrewarmPool(GameObject prefab, int size)
    {
        if (prefab == null) return;
        if (!itemPools.ContainsKey(prefab))
        {
            itemPools[prefab] = new Queue<GameObject>();
        }

        for (int i = 0; i < size; i++)
        {
            GameObject item = Instantiate(prefab);
            item.SetActive(false);
            itemPools[prefab].Enqueue(item);
            instancePrefabMap[item] = prefab;
        }
    }

    public GameObject GetItem(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Cannot get an item from pool for a null prefab.");
            return null;
        }

        // 対応するプールが存在しない場合は作成
        if (!itemPools.ContainsKey(prefab))
        {
            itemPools[prefab] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = itemPools[prefab];

        if (pool.Count > 0)
        {
            GameObject item = pool.Dequeue();
            item.SetActive(true);
            return item;
        }
        else
        {
            // プールが空の場合、新しいアイテムを生成
            GameObject item = Instantiate(prefab);
            instancePrefabMap[item] = prefab; // 新規生成時もマップに追加
            return item;
        }
    }

    public void ReturnItem(GameObject item)
    {
        if (item == null) return;

        // どのPrefabから作られたインスタンスかを取得
        if (instancePrefabMap.TryGetValue(item, out GameObject prefab))
        {
            if (itemPools.ContainsKey(prefab))
            {
                item.SetActive(false);
                itemPools[prefab].Enqueue(item);
            }
            else
            {
                Debug.LogWarning("Returned item's prefab pool does not exist. Destroying item.");
                Destroy(item);
            }
        }
        else
        {
            Debug.LogWarning("Returned item was not created by the pool manager. Destroying item.");
            Destroy(item);
        }
    }
}
