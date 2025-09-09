using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class DroppedItemManager : MonoBehaviour, IItemManager, ISaveable
{
    [Header("システム参照")]
    [SerializeField] private TerrainManager terrainManager; // ロード時に使用

    [Header("アイテム管理設定")]
    [SerializeField] private float _wakeUpRadiusMultiplier = 3f; // アイテムの半径に対する起床範囲の倍率
    
    // インターフェース実装用プロパティ
    public float WakeUpRadiusMultiplier => _wakeUpRadiusMultiplier;
    
    private static DroppedItemManager _instance;
    public static DroppedItemManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DroppedItemManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    // オブジェクトプーリング関連
    private Dictionary<GameObject, Queue<GameObject>> itemPools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, GameObject> instancePrefabMap = new Dictionary<GameObject, GameObject>();

    // アクティブなアイテムの管理リスト
    private List<DroppedItem> activeItems = new List<DroppedItem>();

    // 各アイテムの状態を管理
    private class ItemState
    {
        public bool isSleeping = false;
        public Queue<float> velocityHistory = new Queue<float>();
        public int surroundingObjectCount = 0;
        public int objectCountChangeCounter = 0;
        public float sleepCooldownTimer = 0f;
    }
    private Dictionary<DroppedItem, ItemState> itemStates = new Dictionary<DroppedItem, ItemState>();

    // 静止・起床ロジックの定数
    public int maxWakeUpsPerUpdate = 10; // 1フレームあたりに起床させる最大数
    private Queue<DroppedItem> wakeUpRequestQueue = new Queue<DroppedItem>();
    private HashSet<DroppedItem> itemsInWakeUpQueue = new HashSet<DroppedItem>();

    private const float SleepCheckInterval = 0.2f; // 0.1秒ごとにチェック
    private const float SleepVelocityThreshold = 0.1f;
    public int MaxWakeUpPerStep = 12; // 1ステップで起床させる最大数
    public float WakeUpStepDelay = 0.1f; // 次のステップまでの待機時間
    public int MaxDownwardChain = 7; // 下方向への連鎖回数の上限
    private const int VelocityHistorySize = 1;
    private const int WakeUpCheckCount = 1;
    private const float SleepCooldownDuration = 5.0f; // 5秒のクールダウン

    private float sleepCheckTimer = 0f;

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

    void Start()
    {
        // TerrainManagerが設定されていなければ検索
        if (terrainManager == null)
        {
            terrainManager = FindFirstObjectByType<TerrainManager>();
        }
    }

    void Update()
    {
        ProcessWakeUpRequests();

        sleepCheckTimer += Time.deltaTime;
        if (sleepCheckTimer < SleepCheckInterval)
        {
            return;
        }
        sleepCheckTimer = 0f;

        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            DroppedItem item = activeItems[i];

            if (item == null || !item.gameObject.activeInHierarchy || !itemStates.ContainsKey(item))
            {
                if (item != null)
                {
                    itemStates.Remove(item);
                }
                activeItems.RemoveAt(i);
                continue;
            }
            
            // コンポーネントが無効になっていたら、強制的に有効化する
            if (!item.enabled)
            {
                item.enabled = true;
            }

            if (item.rb == null) continue;

            ItemState state = itemStates[item];

            // スリープクールダウンの処理
            if (state.sleepCooldownTimer > 0)
            {
                state.sleepCooldownTimer -= SleepCheckInterval;
            }

            if (!state.isSleeping)
            {
                // スリープ可能かどうかの判定
                if (state.sleepCooldownTimer <= 0)
                {
                    state.velocityHistory.Enqueue(item.rb.linearVelocity.magnitude);
                    if (state.velocityHistory.Count > VelocityHistorySize)
                    {
                        state.velocityHistory.Dequeue();
                    }

                    float averageVelocity = 0f;
                    if (state.velocityHistory.Count > 0)
                    {
                        foreach (float v in state.velocityHistory) averageVelocity += v;
                        averageVelocity /= state.velocityHistory.Count;
                    }

                    if (!item.rb.isKinematic && state.velocityHistory.Count == VelocityHistorySize && averageVelocity < SleepVelocityThreshold)
                    {
                        var itemCollider = item.GetComponent<Collider>();
                        if (itemCollider != null)
                        {
                            float radius = itemCollider.bounds.extents.magnitude;
                            state.surroundingObjectCount = Physics.OverlapSphere(item.transform.position, radius * WakeUpRadiusMultiplier).Length;
                        }
                        else
                        {
                            state.surroundingObjectCount = 0;
                        }
                        item.rb.isKinematic = true;
                        state.isSleeping = true;
                        state.objectCountChangeCounter = 0;
                    }
                }
            }
            else
            {
                // 起床判定はイベントベースに変更するため、ここのロジックは削除
            }
        }
    }

    void ProcessWakeUpRequests()
    {
        int processedCount = 0;
        while (processedCount < maxWakeUpsPerUpdate && wakeUpRequestQueue.Count > 0)
        {
            DroppedItem itemToWakeUp = wakeUpRequestQueue.Dequeue();
            itemsInWakeUpQueue.Remove(itemToWakeUp);

            if (itemToWakeUp != null && itemToWakeUp.gameObject.activeInHierarchy && itemStates.ContainsKey(itemToWakeUp))
            {
                ItemState state = itemStates[itemToWakeUp];
                if (state.isSleeping)
                {
                    itemToWakeUp.rb.isKinematic = false;
                    state.isSleeping = false;
                    state.sleepCooldownTimer = SleepCooldownDuration;
                    state.velocityHistory.Clear();
                    processedCount++;
                }
            }
        }
    }

    public void WakeUpItemsInRadius(Vector3 center, Vector3 size, Quaternion rotation)
    {
        Collider[] hitColliders = Physics.OverlapBox(center, size / 2, rotation);
        HashSet<DroppedItem> itemsToWakeUp = new HashSet<DroppedItem>();

        foreach (var hitCollider in hitColliders)
        {
            DroppedItem item = hitCollider.GetComponent<DroppedItem>();
            if (item != null && itemStates.ContainsKey(item) && itemStates[item].isSleeping)
            {
                itemsToWakeUp.Add(item);
            }
        }
        StartCoroutine(ProcessWakeUpQueueCoroutine(itemsToWakeUp));
    }

    public void ApplyForceToItemsInRadius(Vector3 center, Vector3 size, Quaternion rotation, MiningInfo info)
    {
        if (info.Force <= 0) return;

        Collider[] hitColliders = Physics.OverlapBox(center, size / 2, rotation);
        foreach (var hitCollider in hitColliders)
        {
            DroppedItem item = hitCollider.GetComponent<DroppedItem>();
            if (item != null && item.rb != null)
            {
                // アイテムがスリープ状態なら起床させる
                if (itemStates.TryGetValue(item, out ItemState state) && state.isSleeping)
                {
                    item.rb.isKinematic = false;
                    state.isSleeping = false;
                    state.sleepCooldownTimer = SleepCooldownDuration;
                    state.velocityHistory.Clear();
                }

                // 力を加える
                Vector3 velocity = Vector3.zero;
                switch (info.Type)
                {
                    case MiningType.Directional:
                        velocity = info.Direction.normalized * info.Force;
                        break;
                    case MiningType.ArcSwing:
                        Vector3 itemDirection = item.transform.position - info.SourcePoint;
                        itemDirection.z = 0; // 2D平面で計算
                        Vector3 tangentDirection;
                        if (info.IsFacingRight) // 時計回り
                        {
                            tangentDirection = new Vector3(itemDirection.y, -itemDirection.x, 0);
                        }
                        else // 反時計回り
                        {
                            tangentDirection = new Vector3(-itemDirection.y, itemDirection.x, 0);
                        }
                        velocity = (tangentDirection.normalized + Vector3.up * 0.3f).normalized * info.Force; // 少し上向きの力を加える
                        break;
                    case MiningType.Explosive:
                        Vector3 directionFromExplosion = (item.transform.position - info.SourcePoint).normalized;
                        directionFromExplosion = (directionFromExplosion + Vector3.up * 0.5f).normalized;
                        velocity = directionFromExplosion * info.Force;
                        break;
                }
                item.rb.AddForce(velocity, ForceMode.Impulse);
            }
        }
    }

    public void WakeUpItemsNearPosition(Vector3 position, float radius)
    {
        Collider[] hitColliders = Physics.OverlapSphere(position, radius);
        HashSet<DroppedItem> itemsToWakeUp = new HashSet<DroppedItem>();

        foreach (var hitCollider in hitColliders)
        {
            DroppedItem item = hitCollider.GetComponent<DroppedItem>();
            if (item != null && itemStates.ContainsKey(item) && itemStates[item].isSleeping)
            {
                itemsToWakeUp.Add(item);
            }
        }
        StartCoroutine(ProcessWakeUpQueueCoroutine(itemsToWakeUp));
    }

    private IEnumerator ProcessWakeUpQueueCoroutine(HashSet<DroppedItem> initialItems)
    {
        if (initialItems != null && initialItems.Count > 0)
        {
            // キューにはタプル(アイテム, 下方向への連鎖回数)を格納
            Queue<Tuple<DroppedItem, int>> processingQueue = new Queue<Tuple<DroppedItem, int>>();
            HashSet<DroppedItem> processedItems = new HashSet<DroppedItem>();
            
            // 初期アイテムをキューに追加
            foreach (var item in initialItems)
            {
                if (item != null && itemStates.ContainsKey(item) && itemStates[item].isSleeping && !itemsInWakeUpQueue.Contains(item))
                {
                    processingQueue.Enqueue(new Tuple<DroppedItem, int>(item, 0));
                    processedItems.Add(item); // Coroutine内で重複して処理しないように
                }
            }

            int processedCountInStep = 0;

            while (processingQueue.Count > 0)
            {
                var queueElement = processingQueue.Dequeue();
                DroppedItem currentItem = queueElement.Item1;
                int downwardChainCount = queueElement.Item2;

                // 起床リクエストのキューに追加
                if (!itemsInWakeUpQueue.Contains(currentItem))
                {
                    wakeUpRequestQueue.Enqueue(currentItem);
                    itemsInWakeUpQueue.Add(currentItem);
                }

                processedCountInStep++;

                // 周囲のアイテムをチェックしてキューに追加
                var currentItemCollider = currentItem.GetComponent<Collider>();
                if (currentItemCollider != null)
                {
                    float radius = currentItemCollider.bounds.extents.magnitude;
                    Collider[] surroundingColliders = Physics.OverlapSphere(currentItem.transform.position, radius * WakeUpRadiusMultiplier);
                    foreach (var surroundingCollider in surroundingColliders)
                    {
                        DroppedItem nearbyItem = surroundingCollider.GetComponent<DroppedItem>();
                        if (nearbyItem != null && itemStates.ContainsKey(nearbyItem) && itemStates[nearbyItem].isSleeping && !processedItems.Contains(nearbyItem) && !itemsInWakeUpQueue.Contains(nearbyItem))
                        {
                            // 上方向か同じ高さの場合
                            if (nearbyItem.transform.position.y >= currentItem.transform.position.y)
                            {
                                // 下方向連鎖カウントをリセットしてキューに追加
                                processingQueue.Enqueue(new Tuple<DroppedItem, int>(nearbyItem, 0));
                                processedItems.Add(nearbyItem);
                            }
                            // 下方向の場合
                            else
                            {
                                // 下方向連鎖の上限に達していない場合のみ
                                if (downwardChainCount < MaxDownwardChain)
                                {
                                    // カウントを増やしてキューに追加
                                    processingQueue.Enqueue(new Tuple<DroppedItem, int>(nearbyItem, downwardChainCount + 1));
                                    processedItems.Add(nearbyItem);
                                }
                            }
                        }
                    }
                }

                // ステップの上限に達したら待機
                if (processedCountInStep >= MaxWakeUpPerStep)
                {
                    processedCountInStep = 0;
                    yield return new WaitForSeconds(WakeUpStepDelay);
                }
            }
        }
    }

    public GameObject GetItem(GameObject prefab)
    {
        if (prefab == null) return null;

        if (!itemPools.ContainsKey(prefab))
        {
            itemPools[prefab] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = itemPools[prefab];
        GameObject itemInstance;

        if (pool.Count > 0)
        {
            itemInstance = pool.Dequeue();
            itemInstance.SetActive(true);
        }
        else
        {
            itemInstance = Instantiate(prefab);
            itemInstance.layer = LayerMask.NameToLayer("DroppedItem");
            instancePrefabMap[itemInstance] = prefab;
        }

        var droppedItemComponent = itemInstance.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            // 管理リストに追加
            if (!activeItems.Contains(droppedItemComponent))
            {
                activeItems.Add(droppedItemComponent);
            }
            // 状態を初期化して追加
            if (!itemStates.ContainsKey(droppedItemComponent))
            {
                itemStates.Add(droppedItemComponent, new ItemState());
            }
            itemStates[droppedItemComponent].sleepCooldownTimer = SleepCooldownDuration; // 初出現時にクールダウンを設定
            itemStates[droppedItemComponent].isSleeping = false;
            itemStates[droppedItemComponent].velocityHistory.Clear();
            itemStates[droppedItemComponent].objectCountChangeCounter = 0;
        }

        return itemInstance;
    }

    public void ReturnItem(GameObject itemInstance)
    {
        if (itemInstance == null) return;

        var droppedItemComponent = itemInstance.GetComponent<DroppedItem>();
        if (droppedItemComponent != null)
        {
            // 状態とリストから削除
            itemStates.Remove(droppedItemComponent);
            activeItems.Remove(droppedItemComponent);
        }

        if (instancePrefabMap.TryGetValue(itemInstance, out GameObject prefab))
        {
            if (itemPools.ContainsKey(prefab))
            {
                itemInstance.SetActive(false);
                itemPools[prefab].Enqueue(itemInstance);
            }
            else
            {
                Destroy(itemInstance);
            }
        }
        else
        {
            Destroy(itemInstance);
        }
    }

    #region SaveSystem
    public string SaveFileName => "dropped_items";

    public object CaptureState()
    {
        var saveData = new DroppedItemsSaveData();
        foreach (var item in activeItems)
        {
            if (item == null) continue;
            var itemData = new DroppedItemSaveData
            {
                position = new SerializableVector3(item.transform.position),
                itemId = (int)item.resourceType
            };
            saveData.droppedItems.Add(itemData);
        }
        return saveData;
    }

    public void RestoreState(object state)
    {
        var saveData = state as DroppedItemsSaveData;
        if (saveData == null) return;

        // 既存の全アイテムをプールに戻す
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            if (activeItems[i] != null)
            {
                ReturnItem(activeItems[i].gameObject);
            }
        }
        activeItems.Clear();
        itemStates.Clear();

        if (terrainManager == null)
        {
            Debug.LogError("TerrainManager is not assigned to DroppedItemManager. Cannot restore items.");
            return;
        }

        // セーブデータからアイテムを復元
        foreach (var itemData in saveData.droppedItems)
        {
            ResourceType resourceType = (ResourceType)itemData.itemId;
            BlockData blockData = terrainManager.BlockDataManager.GetBlockData(resourceType);
            if (blockData == null || blockData.droppedItemPrefab == null)
            {
                Debug.LogWarning($"Prefab for item ID {itemData.itemId} not found. Skipping.");
                continue;
            }

            GameObject itemInstance = GetItem(blockData.droppedItemPrefab);
            if (itemInstance != null)
            {
                itemInstance.transform.position = itemData.position.ToVector3();
                
                // 状態をスリープにしておく
                var droppedItem = itemInstance.GetComponent<DroppedItem>();
                if (droppedItem != null && itemStates.ContainsKey(droppedItem))
                {
                    droppedItem.rb.isKinematic = true;
                    itemStates[droppedItem].isSleeping = true;
                }
            }
        }
    }
    #endregion
}
