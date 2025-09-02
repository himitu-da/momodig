using UnityEngine;
using UnityEngine.InputSystem; // Input Systemを使うために必要
using UnityEngine.UI; // UIを使うために必要
using System.Collections.Generic; // MinecartManager用
using System.Collections; // Coroutine用
using System; // Serializable用

public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInventory
    {
        [Header("インベントリ設定")]
        public int maxCapacity = 200; // プレイヤーが持てる最大数
        
        private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
        
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
            
            resources[type] += amount;
            return true;
        }
        
        /// <summary>
        /// リソースを削除（戻り値は実際に削除した数）
        /// </summary>
        public int RemoveResource(ResourceType type, int amount = 1)
        {
            int currentAmount = resources[type];
            int removeAmount = Mathf.Min(currentAmount, amount);
            resources[type] -= removeAmount;
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
    }

    public enum MoveMode
    {
        SideScroller,
        TopDown
    }

    [Header("移動設定")]
    public float moveSpeed = 5f; // 移動速度
    public float acceleration = 0.1f; // 加速のスムーズさ
    public float deceleration = 0.2f; // 減速のスムーズさ
    public float fallSpeedMultiplier = 0.5f; // 最大落下速度の倍率
    public float fallAcceleration = 1f; // 落下加速度
    [SerializeField] private MoveMode _currentMoveMode;
    public MoveMode currentMoveMode
    {
        get => _currentMoveMode;
        set
        {
            _currentMoveMode = value;
            UpdateConstraints();
        }
    }

    [Header("UI設定")]
    public Text scoreText; // スコア表示用のText
    public Text depthText; // 深度表示用のText
    public Text inventoryText; // インベントリ表示用UI
    
    [Header("参照")]
    public Digger digger; // Diggerへの参照
    public MinecartManager minecartManager; // MinecartManagerへの参照
    public GameObject minecartPrefab; // マインカードプレハブ
    private List<GameObject> spawnedMinecarts = new List<GameObject>(); // 生成したトロッコのリスト

    [Header("インベントリ設定")]
    public PlayerInventory playerInventory = new PlayerInventory();

    [Header("近接システム設定")]
    public float minecartDetectionRange = 3f; // トロッコ検出範囲
    public float itemTransferSpeed = 2f; // アイテム転送速度（個/秒）
    private bool isTransferringItems = false; // アイテム転送中フラグ

    [Header("アニメーション設定")]
    public float itemMoveSpeed = 5f; // アイテムの移動速度
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 移動カーブ
    
    [Header("アイテム回収設定")]
    public float itemPickupRetryInterval = 0.5f; // 回収リトライ間隔（秒）
    
    private int score = 0;
    private Rigidbody rb;
    private InputSystem_Actions controls; // 自動生成されたクラス
    private Vector2 moveInput;
    private float currentFallSpeed = 0f; // 現在の落下速度
    private Vector3 lastMoveDirection = Vector3.forward; // 最後に移動した方向
    private Vector3 currentVelocity; // SmoothDamp用の現在速度
    
    // 接触中のアイテム管理用
    private List<GameObject> contactItems = new List<GameObject>(); // 接触中のアイテムリスト
    private Coroutine pickupRetryCoroutine; // リトライコルーチン

    // スクリプトがロードされたときに一度だけ呼ばれる
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false; // Rigidbodyの重力を無効にする
        }
        
        // プレイヤーの初期向きをX正方向に設定
        if (currentMoveMode == MoveMode.SideScroller)
        {
            transform.rotation = Quaternion.identity; // X正方向を向く
            lastMoveDirection = Vector3.right; // X正方向
        }
        else // TopDown
        {
            transform.rotation = Quaternion.identity; // Z正方向を向く
            lastMoveDirection = Vector3.forward; // Z正方向
        }
        
        // DiggingAreaのDiggerコンポーネントを取得（手動設定を尊重）
        Transform diggingAreaTransform = this.transform.Find("DiggingArea");
        if (diggingAreaTransform != null)
        {
            digger = diggingAreaTransform.GetComponent<Digger>();
            if (digger == null)
            {
                digger = diggingAreaTransform.gameObject.AddComponent<Digger>();
            }
            
            BoxCollider diggingAreaCollider = diggingAreaTransform.GetComponent<BoxCollider>();
            if (diggingAreaCollider != null)
            {
                digger.SetDiggingArea(diggingAreaCollider);
            }
        }

        controls = new InputSystem_Actions();

        // "Move" アクションが実行された時(キーが押された/離された時)に呼ばれる処理を登録
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Textコンポーネントを探して、それをscoreTextに追加
        if (scoreText == null)
        {
            var scoreTextObject = GameObject.Find("ScoreText");
            if (scoreTextObject != null)
            {
                scoreText = scoreTextObject.GetComponent<Text>();
            }
        }
        UpdateScoreText();

        // depthTextを探して設定
        if (depthText == null)
        {
            var depthTextObject = GameObject.Find("DepthText");
            if (depthTextObject != null)
            {
                depthText = depthTextObject.GetComponent<Text>();
            }
        }

        // inventoryTextを探して設定
        if (inventoryText == null)
        {
            var inventoryTextObject = GameObject.Find("InventoryText");
            if (inventoryTextObject != null)
            {
                inventoryText = inventoryTextObject.GetComponent<Text>();
            }
        }
        UpdateInventoryUI(); // 初期化

        // Rigidbodyの制約を更新
        UpdateConstraints();

        // テスト用: 1つプレハブを生成（シーンに配置）
        if (minecartPrefab != null)
        {
            GameObject testMinecart = Instantiate(minecartPrefab, new Vector3(0, 0, 0), Quaternion.identity);
            spawnedMinecarts.Add(testMinecart);
            Debug.Log("テスト用マインカード生成完了");
        }
        else
        {
            Debug.LogWarning("minecartPrefabがアタッチされていません");
        }

        // MinecartManagerの初期化確認
        if (minecartManager != null)
        {
            Debug.Log("MinecartManagerが参照されています");
        }
        else
        {
            Debug.LogWarning("MinecartManagerがアタッチされていません");
        }
    }

    // インスペクターで値が変更されたときに呼ばれる（エディタのみ）
    void OnValidate()
    {
        // 移動モードが変更された場合の制約更新のみ実行
        if (Application.isPlaying)
        {
            UpdateConstraints();
        }
        else if (rb != null)
        {
            // エディタ時でもRigidbodyの制約を更新
            UpdateConstraints();
        }
    }

    // オブジェクトが有効になったときに呼ばれる
    void OnEnable()
    {
        controls.Player.Enable();
    }

    // オブジェクトが無効になったときに呼ばれる
    void OnDisable()
    {
        controls.Player.Disable();
    }

    // フレームごとに呼ばれる
    void Update()
    {
        UpdateDepthText();
        CheckMinecartProximity(); // トロッコとの近接チェック
    }

    // 物理演算の更新タイミングで呼ばれる
    void FixedUpdate()
    {
        Vector3 moveDirection;
        Vector3 targetVelocity;

        switch (currentMoveMode)
        {
            case MoveMode.SideScroller:
                moveDirection = new Vector3(moveInput.x, moveInput.y, 0f);

                if (moveInput == Vector2.zero)
                {
                    // 無操作時は徐々に落下速度を上げる
                    currentFallSpeed += fallAcceleration * Time.fixedDeltaTime;
                    float maxFallSpeed = moveSpeed * fallSpeedMultiplier;
                    currentFallSpeed = Mathf.Min(currentFallSpeed, maxFallSpeed);
                    targetVelocity = new Vector3(0, -currentFallSpeed, 0);
                }
                else
                {
                    currentFallSpeed = 0f; // 操作中は落下速度をリセット
                    if (moveInput.x != 0 && moveInput.y == 0)
                    {
                        // 左右のみの入力の場合は落下しない
                        targetVelocity = new Vector3(moveInput.x, 0, 0).normalized * moveSpeed;
                    }
                    else
                    {
                        // それ以外の入力（上下含む）
                        targetVelocity = moveDirection.normalized * moveSpeed;
                    }
                }
                break;
            case MoveMode.TopDown:
                moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
                targetVelocity = moveDirection.normalized * moveSpeed;
                break;
            default:
                moveDirection = Vector3.zero;
                targetVelocity = Vector3.zero;
                break;
        }

        // 慣性を適用する時間を決定
        float smoothTime = moveDirection.sqrMagnitude > 0 ? acceleration : deceleration;

        // SmoothDampを使用して速度を滑らかに変化させる
        rb.linearVelocity = Vector3.SmoothDamp(rb.linearVelocity, targetVelocity, ref currentVelocity, smoothTime);

        // 移動入力がある場合、その方向を保存
        if (moveDirection.sqrMagnitude > 0.1f)
        {
            lastMoveDirection = moveDirection.normalized;
        }

        // Playerの向きとDiggerの位置を更新
        if (lastMoveDirection.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation;
            if (currentMoveMode == MoveMode.TopDown)
            {
                // TopDownモードでは、入力のX軸を基準とした回転を計算
                // moveInput.x（右方向入力）がワールドのX軸、moveInput.y（上方向入力）がワールドのZ軸
                // プレイヤーの「右」方向（ローカルX軸）が移動方向を向くようにする
                // Z軸の符号を反転して上下方向を修正
                float angle = Mathf.Atan2(-lastMoveDirection.z, lastMoveDirection.x) * Mathf.Rad2Deg;
                targetRotation = Quaternion.AngleAxis(angle, Vector3.up);
            }
            else // SideScroller
            {
                // XY平面での2Dの回転。プレイヤーの右方向が進行方向を向くようにする
                float angle = Mathf.Atan2(lastMoveDirection.y, lastMoveDirection.x) * Mathf.Rad2Deg;
                targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            // Rigidbodyを使って回転させる
            rb.MoveRotation(targetRotation);
        }

        // Diggerの掘削エリアは手動設定に従う（自動的な位置変更は行わない）
    }

    void OnCollisionEnter(Collision collision)
    {
        // 衝突したオブジェクトが "DroppedItem" タグを持っているか確認
        if (collision.gameObject.CompareTag("DroppedItem"))
        {
            // 接触中のアイテムリストに追加
            if (!contactItems.Contains(collision.gameObject))
            {
                contactItems.Add(collision.gameObject);
            }
            
            // アイテム回収を試行
            TryPickupItem(collision.gameObject);
            
            // リトライコルーチンが実行されていない場合は開始
            if (pickupRetryCoroutine == null)
            {
                pickupRetryCoroutine = StartCoroutine(PickupRetryCoroutine());
            }
        }
    }
    
    void OnCollisionExit(Collision collision)
    {
        // 衝突したオブジェクトが "DroppedItem" タグを持っているか確認
        if (collision.gameObject.CompareTag("DroppedItem"))
        {
            // 接触中のアイテムリストから削除
            contactItems.Remove(collision.gameObject);
            
            // 接触中のアイテムがなくなったらリトライコルーチンを停止
            if (contactItems.Count == 0 && pickupRetryCoroutine != null)
            {
                StopCoroutine(pickupRetryCoroutine);
                pickupRetryCoroutine = null;
            }
        }
    }
    
    /// <summary>
    /// 定期的に接触中のアイテムの回収を再試行する
    /// </summary>
    private IEnumerator PickupRetryCoroutine()
    {
        while (contactItems.Count > 0)
        {
            yield return new WaitForSeconds(itemPickupRetryInterval);
            
            // 接触中の全てのアイテムに対して回収を試行
            for (int i = contactItems.Count - 1; i >= 0; i--)
            {
                if (i < contactItems.Count && contactItems[i] != null)
                {
                    TryPickupItem(contactItems[i]);
                }
            }
        }
        
        pickupRetryCoroutine = null;
    }
    
    /// <summary>
    /// アイテムの回収を試行する
    /// </summary>
    private void TryPickupItem(GameObject itemObject)
    {
        // 周辺アイテムを起床させる（既存処理）
        if (DroppedItemManager.Instance != null)
        {
            var itemCollider = itemObject.GetComponent<Collider>();
            if (itemCollider != null)
            {
                float radius = itemCollider.bounds.extents.magnitude;
                DroppedItemManager.Instance.WakeUpItemsNearPosition(itemObject.transform.position, radius * DroppedItemManager.Instance.WakeUpRadiusMultiplier);
            }
        }

        // 資源情報を取得
        DroppedItem itemComponent = itemObject.GetComponent<DroppedItem>();
        ResourceType resourceType = itemComponent != null ? itemComponent.resourceType : ResourceType.Stone;

        // プレイヤーインベントリに追加を試行
        if (playerInventory.CanAddResource(resourceType))
        {
            if (playerInventory.AddResource(resourceType))
            {
                // アイテムをプールに返却
                DroppedItemManager.Instance.ReturnItem(itemObject);
                
                // 接触リストからも削除
                contactItems.Remove(itemObject);
                
                // スコアを更新
                score++;
                UpdateScoreText();
                
                // インベントリUI更新
                UpdateInventoryUI();
                
                Debug.Log($"プレイヤーが{resourceType}を回収しました。持ち物: {playerInventory.GetTotalItemCount()}/{playerInventory.maxCapacity}");
                
                // インベントリの詳細を出力
                var allRes = playerInventory.GetAllResources();
                string detailInfo = "インベントリ詳細: ";
                foreach (var kvp in allRes)
                {
                    if (kvp.Value > 0) detailInfo += $"{kvp.Key}:{kvp.Value} ";
                }
                Debug.Log(detailInfo);
            }
        }
        else
        {
            Debug.Log("インベントリが満杯です！");
            // TODO: 満杯時のフィードバック（UI表示、音声など）
        }
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    void UpdateDepthText()
    {
        if (depthText != null)
        {
            // プレイヤーのY座標を整数に変換して深度として表示
            int depth = Mathf.FloorToInt(transform.position.y);
            depthText.text = "Depth: " + depth;
        }
    }

    void UpdateInventoryUI()
    {
        if (inventoryText != null)
        {
            var resources = playerInventory.GetAllResources();
            string inventoryInfo = $"持ち物 ({playerInventory.GetTotalItemCount()}/{playerInventory.maxCapacity}):\n";
            
            foreach (var kvp in resources)
            {
                if (kvp.Value > 0)
                {
                    inventoryInfo += $"{kvp.Key}: {kvp.Value}個 ";
                }
            }
            
            inventoryText.text = inventoryInfo;
        }
    }

    private void UpdateConstraints()
    {
        if (rb == null) return;

        // すべての物理的な回転を凍結
        rb.freezeRotation = true;

        // MoveModeに応じてRigidbodyのConstraintsを設定
        if (_currentMoveMode == MoveMode.SideScroller)
        {
            // Z位置を固定
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        }
        else // TopDown
        {
            // Y位置を固定
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
    }

    /// <summary>
    /// トロッコとの近接をチェックしてアイテム転送開始
    /// </summary>
    void CheckMinecartProximity()
    {
        if (isTransferringItems || playerInventory.IsEmpty()) return;
        
        // 最も近いトロッコを検索
        GameObject nearestMinecart = GetNearestMinecart();
        if (nearestMinecart != null)
        {
            float distance = Vector3.Distance(transform.position, nearestMinecart.transform.position);
            
            // 範囲内に入った時だけログを出力
            if (distance <= minecartDetectionRange)
            {
                Debug.Log($"最寄りトロッコとの距離: {distance:F2}m (検出範囲: {minecartDetectionRange}m)");
                Debug.Log("トロッコが検出範囲内に入りました！");
                
                // MinecartManagerの状態をデバッグ
                if (minecartManager != null)
                {
                    Debug.Log($"MinecartManager状態 - digable: {minecartManager.digable}, トロッコ数: {minecartManager.minecarts.Count}");
                    if (minecartManager.minecarts.Count > 0)
                    {
                        var cart = minecartManager.minecarts[0];
                        Debug.Log($"トロッコ0の資源状況 - Stone:{cart.resources[ResourceType.Stone]}, Iron:{cart.resources[ResourceType.Iron]}, Gold:{cart.resources[ResourceType.Gold]}, Diamond:{cart.resources[ResourceType.Diamond]}");
                    }
                }
                else
                {
                    Debug.LogError("MinecartManagerがnullです！");
                }
                
                // アイテム転送開始
                Debug.Log("アイテム転送を開始しようとしています...");
                StartCoroutine(TransferItemsToMinecart(nearestMinecart));
            }
        }
        else
        {
            Debug.Log("近くにトロッコが見つかりません");
        }
    }

    /// <summary>
    /// 最も近いトロッコを取得
    /// </summary>
    GameObject GetNearestMinecart()
    {
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (GameObject minecart in spawnedMinecarts)
        {
            if (minecart != null)
            {
                float distance = Vector3.Distance(transform.position, minecart.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = minecart;
                }
            }
        }
        
        return nearest;
    }

    /// <summary>
    /// プレイヤーからトロッコにアイテムを転送
    /// </summary>
    private IEnumerator TransferItemsToMinecart(GameObject targetMinecart)
    {
        isTransferringItems = true;
        Debug.Log("アイテム転送開始");
        
        // 初期状態をログ出力
        Debug.Log($"プレイヤーインベントリ総数: {playerInventory.GetTotalItemCount()}");
        
        while (!playerInventory.IsEmpty())
        {
            // トロッコが離れた場合は中断
            float currentDistance = Vector3.Distance(transform.position, targetMinecart.transform.position);
            if (currentDistance > minecartDetectionRange)
            {
                Debug.Log($"トロッコが離れたため転送中断 (距離: {currentDistance:F2}m)");
                break;
            }
            
            // MinecartManagerが利用可能かチェック
            if (minecartManager == null)
            {
                Debug.LogError("MinecartManagerがnullです");
                break;
            }
            
            if (!minecartManager.digable)
            {
                Debug.Log($"トロッコが利用できません (digable: {minecartManager.digable})");
                break;
            }
            
            if (minecartManager.minecarts.Count == 0)
            {
                Debug.LogError("利用可能なトロッコがありません");
                break;
            }
            
            // 転送するリソースタイプを選択（最初に見つかったもの）
            ResourceType transferType = ResourceType.Stone;
            bool foundResource = false;
            
            var allResources = playerInventory.GetAllResources();
            foreach (var kvp in allResources)
            {
                if (kvp.Value > 0)
                {
                    transferType = kvp.Key;
                    foundResource = true;
                    Debug.Log($"転送予定リソース: {transferType} (持ち数: {kvp.Value})");
                    break;
                }
            }
            
            if (!foundResource) 
            {
                Debug.Log("転送可能なリソースが見つかりません");
                break;
            }
            
            // トロッコの容量チェックを改善
            var targetCart = minecartManager.minecarts[0];
            int currentAmount = targetCart.resources[transferType];
            int capacity = minecartManager.CartCapacity;
            
            Debug.Log($"トロッコ容量チェック - {transferType}: {currentAmount}/{capacity}");
            
            if (currentAmount >= capacity)
            {
                Debug.Log($"トロッコの{transferType}が満載です ({currentAmount}/{capacity})");
                // 他のリソースタイプをチェック
                bool canTransferOther = false;
                foreach (ResourceType otherType in System.Enum.GetValues(typeof(ResourceType)))
                {
                    if (otherType != transferType && 
                        playerInventory.GetResourceCount(otherType) > 0 && 
                        targetCart.resources[otherType] < capacity)
                    {
                        transferType = otherType;
                        canTransferOther = true;
                        Debug.Log($"別のリソースタイプに切り替え: {transferType}");
                        break;
                    }
                }
                
                if (!canTransferOther)
                {
                    Debug.Log("全てのリソースタイプで満載のため転送終了");
                    break;
                }
            }
            
            // プレイヤーから1つ削除
            int removedAmount = playerInventory.RemoveResource(transferType, 1);
            if (removedAmount > 0)
            {
                Debug.Log($"プレイヤーから{transferType}を{removedAmount}個削除");
                
                // アニメーション付きでトロッコに移動
                StartCoroutine(AnimateItemTransfer(transform.position, targetMinecart.transform.position, transferType));
                
                // トロッコに追加
                minecartManager.updatevalue(0, transferType, removedAmount);
                Debug.Log($"{transferType}をトロッコに{removedAmount}個転送完了");
                
                // UI更新
                UpdateInventoryUI();
                
                // 満載チェック
                int newAmount = minecartManager.minecarts[0].resources[transferType];
                if (newAmount >= minecartManager.CartCapacity)
                {
                    minecartManager.settime(0, minecartManager.cartcooltime);
                    Debug.Log($"トロッコの{transferType}が満載、送信開始 ({newAmount}/{minecartManager.CartCapacity})");
                }
            }
            else
            {
                Debug.LogWarning($"プレイヤーから{transferType}の削除に失敗");
            }
            
            // 転送速度に応じて待機
            yield return new WaitForSeconds(1f / itemTransferSpeed);
        }
        
        isTransferringItems = false;
        Debug.Log("アイテム転送終了");
    }

    /// <summary>
    /// アイテム転送のアニメーション
    /// </summary>
    private IEnumerator AnimateItemTransfer(Vector3 startPos, Vector3 endPos, ResourceType resourceType)
    {
        // 簡単なキューブを作成
        GameObject animItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        animItem.transform.position = startPos;
        animItem.transform.localScale = Vector3.one * 0.3f;
        
        // 当たり判定無効化
        var collider = animItem.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        // 色を資源タイプに応じて変更（ResourceTypeUtilityを使用）
        var renderer = animItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = ResourceTypeUtility.GetResourceColor(resourceType);
        }
        
        // トロッコの中央にランダムなバラツキを追加
        Vector3 randomOffset = new Vector3(
            UnityEngine.Random.Range(-0.5f, 0.5f), // X軸方向のバラツキ
            UnityEngine.Random.Range(0.2f, 0.8f),  // Y軸方向のバラツキ（トロッコの中に入るように）
            UnityEngine.Random.Range(-0.5f, 0.5f)  // Z軸方向のバラツキ
        );
        Vector3 targetPos = endPos + randomOffset;
        
        yield return StartCoroutine(MoveItemCoroutine(animItem, startPos, targetPos));
        
        // 完了後削除
        Destroy(animItem);
    }

    /// <summary>
    /// アイテムをアニメーション付きでトロッコに移動
    /// </summary>
    private IEnumerator AnimateItemToMinecart(Vector3 startPosition, ResourceType resourceType, GameObject originalItem)
    {
        // ターゲットトロッコ（最初のトロッコ）
        GameObject targetMinecart = spawnedMinecarts[0];
        if (targetMinecart == null)
        {
            AddResourceToMinecart(resourceType);
            yield break;
        }

        // アニメーション用のアイテムコピーを作成
        GameObject animItem = CreateAnimationItem(originalItem, startPosition);
        if (animItem == null)
        {
            AddResourceToMinecart(resourceType);
            yield break;
        }

        // アニメーション実行
        Vector3 targetPosition = targetMinecart.transform.position + Vector3.up * 1f; // トロッコの少し上
        yield return StartCoroutine(MoveItemCoroutine(animItem, startPosition, targetPosition));

        // アニメーション完了後、アイテムを削除してトロッコに格納
        Destroy(animItem);
        AddResourceToMinecart(resourceType);
    }

    /// <summary>
    /// アニメーション用のアイテムコピーを作成
    /// </summary>
    private GameObject CreateAnimationItem(GameObject original, Vector3 position)
    {
        if (original == null) return null;

        // シンプルなキューブを作成（または元のアイテムをコピー）
        GameObject animItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        animItem.transform.position = position;
        animItem.transform.localScale = Vector3.one * 0.5f; // 小さめに

        // 当たり判定を無効化
        Collider collider = animItem.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // 物理挙動を無効化
        Rigidbody rb = animItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // マテリアルやテクスチャを元のアイテムから取得（オプション）
        Renderer originalRenderer = original.GetComponent<Renderer>();
        Renderer animRenderer = animItem.GetComponent<Renderer>();
        if (originalRenderer != null && animRenderer != null)
        {
            animRenderer.material = originalRenderer.material;
        }

        return animItem;
    }

    /// <summary>
    /// アイテムを指定位置に移動させるコルーチン
    /// </summary>
    private IEnumerator MoveItemCoroutine(GameObject item, Vector3 startPos, Vector3 endPos)
    {
        float elapsedTime = 0f;
        float duration = Vector3.Distance(startPos, endPos) / itemMoveSpeed;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            // AnimationCurveを使用してスムーズな移動
            float curveValue = movementCurve.Evaluate(progress);
            Vector3 currentPosition = Vector3.Lerp(startPos, endPos, curveValue);
            
            if (item != null)
            {
                item.transform.position = currentPosition;
                // 回転アニメーション（オプション）
                item.transform.Rotate(0, 360f * Time.deltaTime, 0);
            }
            else
            {
                break; // アイテムが破棄された場合
            }

            yield return null;
        }

        // 最終位置に設定
        if (item != null)
        {
            item.transform.position = endPos;
        }
    }

    /// <summary>
    /// トロッコに資源を追加
    /// </summary>
    private void AddResourceToMinecart(ResourceType resourceType)
    {
        if (minecartManager != null && minecartManager.digable)
        {
            // トロッコの収容制限をチェック
            if (minecartManager.minecarts[0].resources[resourceType] < minecartManager.CartCapacity)
            {
                minecartManager.updatevalue(0, resourceType, 1); // 0番目のトロッコに1つ追加
                Debug.Log($"資源 {resourceType} をトロッコに積載");

                // 満載チェック
                if (minecartManager.minecarts[0].resources[resourceType] >= minecartManager.CartCapacity)
                {
                    minecartManager.settime(0, minecartManager.cartcooltime);
                    Debug.Log("トロッコが満載、送信開始");
                }
            }
            else
            {
                Debug.Log($"トロッコの{resourceType}が満載です");
            }
        }
    }
}
