using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class DroppedItemManager : MonoBehaviour, IItemManager
{
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

    private Dictionary<Vector3Int, List<DroppedItemData>> itemsByChunk = new Dictionary<Vector3Int, List<DroppedItemData>>();

    void Start()
    {
        // 永続化データからアイテムをロード
        PrepareItemLoading();
    }

    void OnDestroy()
    {
        // シーン終了時にアイテムをセーブ
        SaveItems();
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
        ProcessWakeUpQueueAsync(itemsToWakeUp).Forget();
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
        ProcessWakeUpQueueAsync(itemsToWakeUp).Forget();
    }

    private async UniTask ProcessWakeUpQueueAsync(HashSet<DroppedItem> initialItems)
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
                    await UniTask.Delay(TimeSpan.FromSeconds(WakeUpStepDelay));
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
            itemInstance = Instantiate(prefab, transform);
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

    public List<DroppedItem> GetActiveItems()
    {
        return new List<DroppedItem>(activeItems);
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

    private void SaveItems()
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        persistenceManager.droppedItems.Clear();

        foreach (var item in activeItems)
        {
            if (item == null) continue;

            DroppedItemData data = new DroppedItemData
            {
                position = item.transform.position,
                rotation = item.transform.rotation,
                scale = item.transform.localScale,
                blockDataName = item.blockDataName,
                uvBase = item.uvBase,
                uvSize = item.uvSize,
                useTexture1 = item.useTexture1,
                isKinematic = item.rb != null && item.rb.isKinematic
            };
            persistenceManager.droppedItems.Add(data);
        }
    }

    private void PrepareItemLoading()
    {
        var persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager.droppedItems == null || persistenceManager.droppedItems.Count == 0) return;

        TerrainManager terrainManager = FindFirstObjectByType<TerrainManager>();
        if (terrainManager == null)
        {
            Debug.LogError("TerrainManager not found. Cannot prepare item loading.");
            return;
        }

        itemsByChunk.Clear();
        var settings = terrainManager.Settings;

        foreach (var itemData in persistenceManager.droppedItems)
        {
            int blockX = Mathf.RoundToInt(itemData.position.x / settings.blockSize);
            int blockY = Mathf.RoundToInt(itemData.position.y / settings.blockSize);
            
            int chunkX = Mathf.FloorToInt((float)blockX / settings.blocksPerChunk.x);
            int chunkY = Mathf.FloorToInt((float)blockY / settings.blocksPerChunk.y);
            Vector3Int chunkPos = new Vector3Int(chunkX, chunkY, 0);

            if (!itemsByChunk.ContainsKey(chunkPos))
            {
                itemsByChunk[chunkPos] = new List<DroppedItemData>();
            }
            itemsByChunk[chunkPos].Add(itemData);
        }
        
        persistenceManager.droppedItems.Clear();
    }

    public void LoadItemsInChunk(Vector3Int chunkPosition)
    {
        if (!itemsByChunk.TryGetValue(chunkPosition, out var itemsToLoad)) return;

        TerrainManager terrainManager = FindFirstObjectByType<TerrainManager>();
        if (terrainManager == null || terrainManager.TerrainDataManager == null)
        {
            Debug.LogError("TerrainManager or TerrainDataManager not found. Cannot load items.");
            return;
        }

        foreach (var data in itemsToLoad)
        {
            BlockData blockData = terrainManager.TerrainDataManager.GetBlockDataByName(data.blockDataName);
            if (blockData == null)
            {
                Debug.LogWarning($"Failed to load BlockData: {data.blockDataName}");
                continue;
            }

            GameObject item = GetItem(blockData.droppedItemPrefab);
            if (item == null) continue;

            item.transform.position = data.position;
            item.transform.rotation = data.rotation;
            item.transform.localScale = data.scale;

            Rigidbody itemRigidbody = item.GetComponent<Rigidbody>();
            if (itemRigidbody == null)
            {
                itemRigidbody = item.AddComponent<Rigidbody>();
            }

            if (terrainManager != null)
            {
                 float voxelWorldSize = terrainManager.Settings.blockSize / terrainManager.Settings.voxelsPerBlock;
                 float volume = Mathf.Pow(voxelWorldSize, 3);
                 itemRigidbody.mass = volume * blockData.density;
            }

            SetDroppedItemConstraints(itemRigidbody);

            if (!item.CompareTag("DroppedItem"))
            {
                item.tag = "DroppedItem";
            }

            DroppedItem droppedItem = item.GetComponent<DroppedItem>();
            if (droppedItem != null)
            {
                droppedItem.blockDataName = data.blockDataName;
                droppedItem.uvBase = data.uvBase;
                droppedItem.uvSize = data.uvSize;
                droppedItem.useTexture1 = data.useTexture1;
                droppedItem.resourceType = blockData.resourceType;

                if (itemRigidbody != null)
                {
                    itemRigidbody.isKinematic = data.isKinematic;
                }
                if (itemStates.TryGetValue(droppedItem, out var state))
                {
                    state.isSleeping = data.isKinematic;
                }
            }

            var itemRenderer = item.GetComponent<Renderer>();
            if (itemRenderer != null)
            {
                Texture2D tex1 = (blockData.textures != null && blockData.textures.Count > 0) ? blockData.textures[0] : null;
                Texture2D tex2 = (blockData.textures != null && blockData.textures.Count > 1) ? blockData.textures[1] : null;
                
                VoxelFaceTextureInfo faceInfo = new VoxelFaceTextureInfo(
                    Vector3.up,
                    data.uvBase,
                    data.uvSize,
                    data.useTexture1 ? tex1 : tex2,
                    true
                );

                VoxelTextureExtractor extractor = new VoxelTextureExtractor();
                Texture2D extractedTexture = extractor.ExtractVoxelTextureRegion(faceInfo);

                if (extractedTexture != null)
                {
                    var material = new Material(Shader.Find("Custom/Default"));
                    material.renderQueue = RenderQueue.Geometry;
                    material.mainTexture = extractedTexture;
                    itemRenderer.material = material;
                }
            }
        }
        
        itemsByChunk.Remove(chunkPosition);
    }

    /// <summary>
    /// ドロップアイテムのRigidbodyに移動モードに応じた制約を設定
    /// </summary>
    private void SetDroppedItemConstraints(Rigidbody itemRigidbody)
    {
        if (itemRigidbody == null) return;

        PlayerController.MoveMode currentMoveMode = GetCurrentMoveMode();

        switch (currentMoveMode)
        {
            case PlayerController.MoveMode.SideScroller:
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionZ | 
                                          RigidbodyConstraints.FreezeRotationX | 
                                          RigidbodyConstraints.FreezeRotationY;
                break;

            case PlayerController.MoveMode.TopDown:
                itemRigidbody.constraints = RigidbodyConstraints.FreezePositionY | 
                                          RigidbodyConstraints.FreezeRotationX | 
                                          RigidbodyConstraints.FreezeRotationZ;
                break;

            default:
                itemRigidbody.constraints = RigidbodyConstraints.None;
                break;
        }
    }

    /// <summary>
    /// 現在のゲームの移動モードを取得
    /// </summary>
    private PlayerController.MoveMode GetCurrentMoveMode()
    {
        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            return playerController.currentMoveMode;
        }
        return PlayerController.MoveMode.TopDown;
    }
}
