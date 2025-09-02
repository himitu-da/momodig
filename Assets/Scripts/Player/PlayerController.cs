using UnityEngine;
using UnityEngine.InputSystem; // Input Systemを使うために必要
using UnityEngine.UI; // UIを使うために必要
using System.Collections.Generic; // MinecartManager用
using System.Collections; // Coroutine用
using System; // Serializable用

public class PlayerController : MonoBehaviour
{

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

    [Header("インベントリ設定")]
    public PlayerInventory playerInventory = new PlayerInventory();
    
    [Header("アイテム回収設定")]
    public float itemPickupRetryInterval = 0.5f; // 回収リトライ間隔（秒）
    
    private int score = 0;
    private Rigidbody rb;
    private InputSystem_Actions controls; // 自動生成されたクラス
    private Vector2 moveInput;
    private float currentFallSpeed = 0f; // 現在の落下速度
    public Vector3 lastMoveDirection = Vector3.forward; // 最後に移動した方向
    private Vector3 currentVelocity; // SmoothDamp用の現在速度
    
    // 接触中のアイテム管理用
    private List<GameObject> contactItems = new List<GameObject>(); // 接触中のアイテムリスト
    private Coroutine pickupRetryCoroutine; // リトライコルーチン
    
    // MinecartInteractionSystemへの参照
    private MinecartInteractionSystem minecartInteraction;

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

        // MinecartInteractionSystemの参照を取得
        minecartInteraction = GetComponent<MinecartInteractionSystem>();
        if (minecartInteraction == null)
        {
            Debug.LogWarning("MinecartInteractionSystemが見つかりません");
        }
        
        // インベントリイベントの購読
        if (playerInventory != null)
        {
            playerInventory.OnResourceAdded += OnInventoryResourceAdded;
            playerInventory.OnTotalCountChanged += OnInventoryTotalCountChanged;
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
    
    // オブジェクトが破棄されるときに呼ばれる
    void OnDestroy()
    {
        // イベント購読解除
        if (playerInventory != null)
        {
            playerInventory.OnResourceAdded -= OnInventoryResourceAdded;
            playerInventory.OnTotalCountChanged -= OnInventoryTotalCountChanged;
        }
    }

    // フレームごとに呼ばれる
    void Update()
    {
        UpdateDepthText();
        
        // トロッコとの近接チェックをMinecartInteractionSystemに委譲
        if (minecartInteraction != null)
        {
            minecartInteraction.CheckMinecartProximity();
        }
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
    /// インベントリにリソースが追加されたときの処理
    /// </summary>
    private void OnInventoryResourceAdded(ResourceType type, int amount)
    {
        // リソース追加時の追加処理があれば実装
        UpdateInventoryUI(); // UI更新を呼び出し
    }
    
    /// <summary>
    /// インベントリの総数が変更されたときの処理
    /// </summary>
    private void OnInventoryTotalCountChanged(int newTotal)
    {
        // 総数変更時の処理（必要に応じて）
        // 例: 満杯状態の通知、パフォーマンス調整など
    }
}
