using UnityEngine;
using UnityEngine.InputSystem; // Input Systemを使うために必要
using UnityEngine.UI; // UIを使うために必要
using System.Collections.Generic; // MinecartManager用
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor; // Serializable用

public class PlayerController : MonoBehaviour
{

    public enum MoveMode
    {
        SideScroller,
        TopDown
    }

    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 5f; // 移動速度
    [SerializeField] private float acceleration = 0.1f; // 加速のスムーズさ
    [SerializeField] private float deceleration = 0.2f; // 減速のスムーズさ
    [SerializeField] private float fallSpeedMultiplier = 0.5f; // 最大落下速度の倍率
    [SerializeField] private float fallAcceleration = 1f; // 落下加速度
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
    [SerializeField] private Text scoreText; // スコア表示用のText
    [SerializeField] private Text depthText; // 深度表示用のText
    [SerializeField] private Text inventoryText; // インベントリ表示用UI

    [Header("インベントリ設定")]
    private IInventory inventory;
    
    [Header("アイテムマネージャー設定")]
    private IItemManager itemManager;
    
    // 外部からのアクセス用プロパティ
    public IInventory Inventory => inventory;
    
    // マウス位置を外部から取得するためのプロパティ
    public Vector2 MousePosition => mousePosition;
    
    /// <summary>
    /// マウスのスクリーン座標をワールド座標に変換する
    /// </summary>
    /// <param name="screenPosition">スクリーン座標</param>
    /// <param name="distance">カメラからの距離（SideScrollerモードで使用）</param>
    /// <returns>ワールド座標</returns>
    public Vector3 ScreenToWorldPoint(Vector2 screenPosition, float distance = 10f)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("Main camera not found!");
            return Vector3.zero;
        }

        Vector3 screenPos = new Vector3(screenPosition.x, screenPosition.y, distance);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }
    
    /// <summary>
    /// 現在のマウス位置をワールド座標で取得
    /// </summary>
    /// <param name="distance">カメラからの距離（SideScrollerモードで使用）</param>
    /// <returns>マウス位置のワールド座標</returns>
    public Vector3 GetMouseWorldPosition(float distance = 10f)
    {
        return ScreenToWorldPoint(mousePosition, distance);
    }
    
    [Header("アイテム回収設定")]
    [SerializeField] private float itemPickupRetryInterval = 0.5f; // 回収リトライ間隔（秒）
    
    private int score = 0;
    private Rigidbody rb;
    private InputSystem_Actions controls; // 自動生成されたクラス
    private Vector2 moveInput;
    private Vector2 mousePosition; // マウスのスクリーン座標
    private float currentFallSpeed = 0f; // 現在の落下速度
    public Vector3 lastMoveDirection = Vector3.forward; // 最後に移動した方向
    public bool IsFacingRight { get; private set; } = true; // 現在の向きを保持 (true: 右, false: 左)
    private Vector3 currentVelocity; // SmoothDamp用の現在速度
    private Animator _animator; // Player自身のAnimator
    
    // 接触中のアイテム管理用
    private List<GameObject> contactItems = new List<GameObject>(); // 接触中のアイテムリスト
    private CancellationTokenSource pickupRetryCancellationTokenSource;
    
    // MinecartPlayerInteractionSystemへの参照
    [SerializeField] private MinecartPlayerInteractionSystem minecartInteraction;

    // MiningToolsControllerへの参照
    private MiningToolsController miningToolsController;

    // スクリプトがロードされたときに一度だけ呼ばれる
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        if (rb != null)
        {
            rb.useGravity = false; // Rigidbodyの重力を無効にする
        }
        
        // プレイヤーの初期向きをX正方向に設定
        if (currentMoveMode == MoveMode.SideScroller)
        {
            transform.rotation = Quaternion.identity; // X正方向を向く
            lastMoveDirection = Vector3.right; // X正方向
            IsFacingRight = true;
        }
        else // TopDown
        {
            transform.rotation = Quaternion.identity; // Z正方向を向く
            lastMoveDirection = Vector3.forward; // Z正方向
        }

        controls = new InputSystem_Actions();

        // "Move" アクションが実行された時(キーが押された/離された時)に呼ばれる処理を登録
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // "MousePosition" アクションの登録
        controls.Player.MousePosition.performed += ctx => mousePosition = ctx.ReadValue<Vector2>();
        controls.Player.MousePosition.canceled += ctx => mousePosition = Vector2.zero;

    // "MainMine" アクションの登録
    controls.Player.MainMine.performed += OnMainMine;

    // "SubMine" アクションの登録
    controls.Player.SubMine.performed += OnSubMine;

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

        // MinecartInteractionSystemの参照はインスペクタから設定される
        
        // MiningToolsControllerの参照を取得
        miningToolsController = GetComponentInChildren<MiningToolsController>();
        if (miningToolsController == null)
        {
            Debug.LogError("MiningToolsControllerが見つかりません。Playerの子オブジェクトにアタッチしてください。");
        }
        
        // 依存関係の初期化（インターフェース経由）
        inventory = new PlayerInventory();
        itemManager = DroppedItemManager.Instance;
        
        // インベントリイベントの購読
        if (inventory != null)
        {
            inventory.OnResourceAdded += OnInventoryResourceAdded;
            inventory.OnTotalCountChanged += OnInventoryTotalCountChanged;
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
        if (controls == null)
        {
            controls = new InputSystem_Actions();
        }
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
        if (inventory != null)
        {
            inventory.OnResourceAdded -= OnInventoryResourceAdded;
            inventory.OnTotalCountChanged -= OnInventoryTotalCountChanged;
        }
        
        if (controls != null)
        {
            controls.Player.MainMine.performed -= OnMainMine;
            controls.Player.SubMine.performed -= OnSubMine;
            controls.Player.MousePosition.performed -= ctx => mousePosition = ctx.ReadValue<Vector2>();
            controls.Player.MousePosition.canceled -= ctx => mousePosition = Vector2.zero;
        }
        
        pickupRetryCancellationTokenSource?.Cancel();
        pickupRetryCancellationTokenSource?.Dispose();
    }

    // フレームごとに呼ばれる
    void Update()
    {
        UpdateDepthText();
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

        // SideScrollerモードで左右の入力があった場合、向きを更新
        if (currentMoveMode == MoveMode.SideScroller && moveInput.x != 0)
        {
            IsFacingRight = moveInput.x > 0;
        }

        // 移動入力がある場合、その方向を保存
        if (moveDirection.sqrMagnitude > 0.1f)
        {
            lastMoveDirection = moveDirection.normalized;
        }
        else
        {
            // 入力がない（停止中）の扱い
            if (currentMoveMode == MoveMode.SideScroller)
            {
                // 横スクでは停止中でも向きフラグに基づいて水平方向をlastMoveDirectionに反映
                lastMoveDirection = new Vector3(IsFacingRight ? 1f : -1f, 0f, 0f);
            }
            else
            {
                // TopDownでは停止中は直前の向きを維持（何もしない）
            }
        }

        // MiningToolsControllerに回転処理を委譲
        if (miningToolsController != null)
        {
            miningToolsController.UpdateRotation(lastMoveDirection, currentMoveMode);
        }
    }

    private void OnMainMine(InputAction.CallbackContext context)
    {
        // 道具自身のBehaviourを呼び出すだけにする
        if (miningToolsController != null)
        {
            miningToolsController.UseMainMineTool(this.gameObject, lastMoveDirection);
        }
    }

    private void OnSubMine(InputAction.CallbackContext context)
    {
        if (miningToolsController != null)
        {
            miningToolsController.UseSubMineTool(this.gameObject, lastMoveDirection);
        }
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
            
            // リトライタスクが実行されていない場合は開始
            if (pickupRetryCancellationTokenSource == null)
            {
                pickupRetryCancellationTokenSource = new CancellationTokenSource();
                PickupRetryAsync(pickupRetryCancellationTokenSource.Token).Forget();
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
            
            // 接触中のアイテムがなくなったらリトライタスクを停止
            if (contactItems.Count == 0 && pickupRetryCancellationTokenSource != null)
            {
                pickupRetryCancellationTokenSource.Cancel();
                pickupRetryCancellationTokenSource.Dispose();
                pickupRetryCancellationTokenSource = null;
            }
        }
    }
    
    /// <summary>
    /// 定期的に接触中のアイテムの回収を再試行する
    /// </summary>
    private async UniTask PickupRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && contactItems.Count > 0)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(itemPickupRetryInterval), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            
            // 接触中の全てのアイテムに対して回収を試行
            for (int i = contactItems.Count - 1; i >= 0; i--)
            {
                if (i < contactItems.Count && contactItems[i] != null)
                {
                    TryPickupItem(contactItems[i]);
                }
            }
        }
        
        pickupRetryCancellationTokenSource?.Dispose();
        pickupRetryCancellationTokenSource = null;
    }
    
    /// <summary>
    /// アイテムの回収を試行する
    /// </summary>
    private void TryPickupItem(GameObject itemObject)
    {
        // 周辺アイテムを起床させる（インターフェース経由）
        if (itemManager != null)
        {
            var itemCollider = itemObject.GetComponent<Collider>();
            if (itemCollider != null)
            {
                float radius = itemCollider.bounds.extents.magnitude;
                itemManager.WakeUpItemsNearPosition(itemObject.transform.position, radius * itemManager.WakeUpRadiusMultiplier);
            }
        }

        // 資源情報を取得
        DroppedItem itemComponent = itemObject.GetComponent<DroppedItem>();
        ResourceType resourceType = itemComponent != null ? itemComponent.resourceType : ResourceType.Stone;

        // プレイヤーインベントリに追加を試行（インターフェース経由）
        if (inventory.CanAddResource(resourceType))
        {
            if (inventory.AddResource(resourceType))
            {
                // アイテムをプールに返却（インターフェース経由）
                itemManager.ReturnItem(itemObject);
                
                // 接触リストからも削除
                contactItems.Remove(itemObject);
                
                // スコアを更新
                score++;
                UpdateScoreText();
                
                // インベントリUI更新
                UpdateInventoryUI();
                
                Debug.Log($"プレイヤーが{resourceType}を回収しました。持ち物: {inventory.GetTotalItemCount()}/{inventory.maxCapacity}");
                
                // インベントリの詳細を出力（インターフェース経由）
                var allRes = inventory.GetAllResources();
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
            var resources = inventory.GetAllResources();
            string inventoryInfo = $"持ち物 ({inventory.GetTotalItemCount()}/{inventory.maxCapacity}):\n";
            
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

    /// <summary>
    /// MiningToolsControllerから呼び出され、Animatorに道具の種類を教える
    /// </summary>
    public void SetToolAnimationType(int toolId)
    {
        if (_animator != null)
        {
            // HACK: パラメータが存在しないエラーが出るため、一時的に無効化。
            //       Animatorに "ToolType" (Float) パラメータが存在することを確認してください。
            // // Blend TreeはFloatしか受け付けないため、intをfloatにキャストして設定する
            // _animator.SetFloat("ToolType", (float)toolId);
        }
    }

    /// <summary>
    /// 外部（MiningToolBehaviour）から呼び出され、採掘アニメーションを開始する
    /// </summary>
    public void TriggerMineAnimation(Vector3 direction)
    {
        if (_animator != null && miningToolsController != null)
        {
            string stateName = miningToolsController.GetCurrentToolStateName();
            if (!string.IsNullOrEmpty(stateName))
            {
                // 8方向入力を4方向（上、下、左、右）に変換
                Vector2 animDirection = GetAnimationDirection(direction); // TODO: これは汎用性がないので、ポリモーフィズムで個々のToolごとに振る舞いを変えるようにするなどが必要

                // 向きを設定
                _animator.SetFloat("DirectionX", animDirection.x);
                _animator.SetFloat("DirectionY", animDirection.y);
                
                // アニメーションを強制的に最初から再生
                _animator.Play(stateName, 0, 0f);
            }
        }
    }

    /// <summary>
    /// 8方向のベクトルを、アニメーション用の4方向（上下左右）のベクトルに変換する
    /// </summary>
    /// <param name="direction">入力方向ベクトル</param>
    /// <returns>アニメーション用の(x, y)ベクトル</returns>
    private Vector2 GetAnimationDirection(Vector3 direction)
    {
        // TopDownモードではZ軸をYとして扱う
        float y = (currentMoveMode == MoveMode.TopDown) ? direction.z : direction.y;
        float x = direction.x;

        // 非常に小さい入力は無視
        if (x * x + y * y < 0.1f)
        {
            // ゼロベクトルに近い場合は、デフォルトの向き（例：右）を返すか、ゼロを返す
            return new Vector2(1, 0); 
        }

        // 垂直方向の入力が水平方向より明らかに強い場合のみ上下と判定
        // これにより、斜め入力は左右に分類される
        if (Mathf.Abs(y) > Mathf.Abs(x) * 2) // yがxの2倍以上大きい場合
        {
            if (y > 0)
            {
                return Vector2.up; // 上
            }
            else
            {
                return Vector2.down; // 下
            }
        }
        else // それ以外はすべて左右で判定
        {
            if (x > 0)
            {
                return Vector2.right; // 右
            }
            else
            {
                return Vector2.left; // 左
            }
        }
    }
}
