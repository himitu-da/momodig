using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 5f; // 移動速度
    [SerializeField] private float acceleration = 0.1f; // 加速のスムーズさ
    [SerializeField] private float deceleration = 0.2f; // 減速のスムーズさ
    [SerializeField] private float fallSpeedMultiplier = 0.5f; // 最大落下速度の倍率
    [SerializeField] private float fallAcceleration = 1f; // 落下加速度

    [Header("UI設定")]
    [SerializeField] private TextMeshProUGUI scoreText; // スコア表示用のText
    [SerializeField] private TextMeshProUGUI depthText; // 深度表示用のText
    [SerializeField] private TextMeshProUGUI inventoryText; // インベントリ表示用UI
    [SerializeField] private TextMeshProUGUI inventoryCapacityText; // インベントリ容量表示用UI

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
    /// <param name="distance">カメラからの距離（プレイ面で使用）</param>
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
    /// <param name="distance">カメラからの距離（プレイ面で使用）</param>
    /// <returns>マウス位置のワールド座標</returns>
    public Vector3 GetMouseWorldPosition(float distance = 10f)
    {
        return ScreenToWorldPoint(mousePosition, distance);
    }
    
    [Header("アイテム回収設定")]
    [SerializeField] private float itemPickupRetryInterval = 0.5f; // 回収リトライ間隔（秒）

    [Header("流体抵抗設定")]
    [SerializeField, InspectorName("流体シミュレーション"), Tooltip("同じシーンの FluidManager を割り当てます。未設定なら TerrainManager から自動取得を試みます。")] private FluidManager fluidManager;
    [SerializeField, InspectorName("抵抗判定に使う Collider"), Tooltip("プレイヤーのどの範囲で水量を測るかに使う Collider です。通常は PlayerCollider を指定します。")] private Collider fluidResistanceCollider;
    [SerializeField, InspectorName("流体抵抗を有効にする"), Tooltip("オフにすると、水による移動抵抗を無効にします。")] private bool enableFluidResistance = true;
    [SerializeField, InspectorName("横方向サンプル数"), Tooltip("Collider 内を横方向に何点読むかです。大きいほど正確ですが少し重くなります。")] private int fluidHorizontalSampleCount = 2;
    [SerializeField, InspectorName("縦方向サンプル数"), Tooltip("Collider 内を高さ方向に何点読むかです。水位差に対する精度に効きます。")] private int fluidVerticalSampleCount = 3;
    [SerializeField, InspectorName("奥行きサンプル数"), Tooltip("Collider 内を奥行き方向に何点読むかです。プレイ面では 1 でも構いません。")] private int fluidDepthSampleCount = 1;
    [SerializeField, InspectorName("サンプルの内側オフセット"), Tooltip("Collider の端から少し内側を読む量です。境界の誤判定を減らします。")] private float fluidSampleInset = 0.05f;
    [SerializeField, InspectorName("抵抗の強さ"), Tooltip("水に浸かったときにどれくらい移動が重くなるかの基本倍率です。")] private float fluidResistanceStrength = 0.85f;
    [SerializeField, InspectorName("最低移動速度倍率"), Tooltip("最大まで抵抗が効いたときでも残す移動速度の割合です。"), Range(0.05f, 1f)] private float minimumFluidMoveSpeedMultiplier = 0.35f;
    [SerializeField, InspectorName("加速の鈍さ倍率"), Tooltip("水中で加速をどれだけ鈍くするかです。大きいほどもっさりします。")] private float fluidAccelerationPenaltyMultiplier = 2f;
    [SerializeField, InspectorName("追加ドラッグ"), Tooltip("水中で速度を落ち着かせる補正量です。大きいほど抵抗感が強まります。")] private float fluidDrag = 4f;

    private int score = 0;
    private Rigidbody rb;
    private InputSystem_Actions controls; // 自動生成されたクラス
    private Vector2 moveInput;
    public Vector2 MoveInput => moveInput; // PassageControllerから入力を取得するため
    private Vector2 mousePosition; // マウスのスクリーン座標
    private float currentFallSpeed = 0f; // 現在の落下速度
    public Vector3 lastMoveDirection = Vector3.forward; // 最後に移動した方向
    public bool IsFacingRight { get; private set; } = true; // 現在の向きを保持 (true: 右, false: 左)
    private Vector3 currentVelocity; // SmoothDamp用の現在速度
    private PlayerVisualsController playerVisualsController; // ビジュアル担当
    
    // PassageControllerからの制御用
    public bool IsInPassage { get; set; } = false;
    
    // 接触中のアイテム管理用
    private List<GameObject> contactItems = new List<GameObject>(); // 接触中のアイテムリスト
    private CancellationTokenSource pickupRetryCancellationTokenSource;
    
    // MinecartPlayerInteractionSystemへの参照
    [SerializeField] private MinecartPlayerInteractionSystem minecartInteraction;

    // MiningToolsControllerへの参照
    private MiningToolsController miningToolsController;
    private PlayerInventory playerInventory;
    private MiningLogSystem miningLogSystem;

    // スクリプトがロードされたときに一度だけ呼ばれる
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerVisualsController = GetComponentInChildren<PlayerVisualsController>();
        if (rb != null)
        {
            rb.useGravity = false; // Rigidbodyの重力を無効にする
        }

        if (fluidManager == null)
        {
            TerrainManager terrainManager = FindFirstObjectByType<TerrainManager>();
            if (terrainManager != null)
            {
                fluidManager = terrainManager.FluidManager;
            }
        }

        ResolveFluidResistanceCollider();        
        transform.rotation = Quaternion.identity;
        lastMoveDirection = Vector3.right;
        IsFacingRight = true;

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
                scoreText = scoreTextObject.GetComponent<TextMeshProUGUI>();
            }
        }
        UpdateScoreText();

        // depthTextを探して設定
        if (depthText == null)
        {
            var depthTextObject = GameObject.Find("DepthText");
            if (depthTextObject != null)
            {
                depthText = depthTextObject.GetComponent<TextMeshProUGUI>();
            }
        }

        // inventoryTextを探して設定
        if (inventoryText == null)
        {
            var inventoryTextObject = GameObject.Find("InventoryText");
            if (inventoryTextObject != null)
            {
                inventoryText = inventoryTextObject.GetComponent<TextMeshProUGUI>();
            }
        }

        // inventoryCapacityTextを探して設定
        if (inventoryCapacityText == null)
        {
            var inventoryCapacityTextObject = GameObject.Find("InventoryCapacityText");
            if (inventoryCapacityTextObject != null)
            {
                inventoryCapacityText = inventoryCapacityTextObject.GetComponent<TextMeshProUGUI>();
            }
        }
        
        // 依存関係の初期化（インターフェース経由）
        playerInventory = new PlayerInventory();
        inventory = playerInventory;
        itemManager = DroppedItemManager.Instance;
        miningLogSystem = FindFirstObjectByType<MiningLogSystem>();
        
        // インベントリイベントの購読
        if (inventory != null)
        {
            inventory.OnTotalCountChanged += OnInventoryTotalCountChanged;
        }

        if (playerInventory != null)
        {
            playerInventory.OnInventoryFullStateChanged += OnInventoryFullStateChanged;
        }

        UpdateInventoryUI(); // 初期化
        UpdateInventoryCapacityUI(); // 初期化

        // Rigidbodyの制約を更新
        UpdateConstraints();

        // MinecartInteractionSystemの参照はインスペクタから設定される
        
        // MiningToolsControllerの参照を取得
        miningToolsController = GetComponentInChildren<MiningToolsController>();
        if (miningToolsController == null)
        {
            Debug.LogError("MiningToolsControllerが見つかりません。Playerの子オブジェクトにアタッチしてください。");
        }
    }

    // インスペクターで値が変更されたときに呼ばれる（エディタのみ）
    void OnValidate()
    {
        fluidHorizontalSampleCount = Mathf.Max(1, fluidHorizontalSampleCount);
        fluidVerticalSampleCount = Mathf.Max(1, fluidVerticalSampleCount);
        fluidDepthSampleCount = Mathf.Max(1, fluidDepthSampleCount);
        fluidSampleInset = Mathf.Max(0f, fluidSampleInset);
        fluidResistanceStrength = Mathf.Max(0f, fluidResistanceStrength);
        minimumFluidMoveSpeedMultiplier = Mathf.Clamp(minimumFluidMoveSpeedMultiplier, 0.05f, 1f);
        fluidAccelerationPenaltyMultiplier = Mathf.Max(1f, fluidAccelerationPenaltyMultiplier);
        fluidDrag = Mathf.Max(0f, fluidDrag);
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
            inventory.OnTotalCountChanged -= OnInventoryTotalCountChanged;
        }

        if (playerInventory != null)
        {
            playerInventory.OnInventoryFullStateChanged -= OnInventoryFullStateChanged;
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

        // 慣性を適用する時間を決定
        float smoothTime = moveDirection.sqrMagnitude > 0 ? acceleration : deceleration;
        float fluidSubmersion = GetFluidSubmersionRatio();
        float fluidResistance = GetFluidResistanceFactor(fluidSubmersion);
        if (fluidResistance > 0f)
        {
            targetVelocity *= Mathf.Lerp(1f, minimumFluidMoveSpeedMultiplier, fluidResistance);
            smoothTime *= Mathf.Lerp(1f, fluidAccelerationPenaltyMultiplier, fluidResistance);
        }

        // SmoothDampを使用して速度を滑らかに変化させる
        // Passage中も通常移動は維持し、採掘だけPassageController側で止める
        Vector3 nextVelocity = Vector3.SmoothDamp(rb.linearVelocity, targetVelocity, ref currentVelocity, smoothTime);
        if (fluidResistance > 0f)
        {
            float dragFactor = 1f - Mathf.Exp(-fluidDrag * fluidResistance * Time.fixedDeltaTime);
            nextVelocity = Vector3.Lerp(nextVelocity, targetVelocity, dragFactor);
        }

        rb.linearVelocity = nextVelocity;

        // 左右の入力があった場合、向きを更新
        if (moveInput.x != 0)
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
            // 停止中でも向きフラグに基づいて水平方向をlastMoveDirectionに反映
            lastMoveDirection = new Vector3(IsFacingRight ? 1f : -1f, 0f, 0f);
        }

        // MiningToolsControllerに回転処理を委譲
        if (miningToolsController != null)
        {
            miningToolsController.UpdateRotation(lastMoveDirection);
        }

        // PlayerVisualsControllerに移動アニメーションの更新を委譲
        if (playerVisualsController != null)
        {
            playerVisualsController.UpdateMovementAnimation(lastMoveDirection);
        }
    }

    private void OnMainMine(InputAction.CallbackContext context)
    {
        // UI要素上をクリックした場合は、採掘処理を行わない
        if (IsPointerOverNonMineableUI())
        {
            return;
        }
        
        // Passageに入っている間は掘削を無効化
        if (IsInPassage) return;

        // 道具自身のBehaviourを呼び出すだけにする
        if (miningToolsController != null)
        {
            miningToolsController.UseMainMineTool(this.gameObject, lastMoveDirection);
        }
    }

    private void OnSubMine(InputAction.CallbackContext context)
    {
        // UI要素上をクリックした場合は、採掘処理を行わない
        if (IsPointerOverNonMineableUI())
        {
            return;
        }
        
        // Passageに入っている間は掘削を無効化
        if (IsInPassage) return;
        
        if (miningToolsController != null)
        {
            miningToolsController.UseSubMineTool(this.gameObject, lastMoveDirection);
        }
    }

    /// <summary>
    /// マウスカーソルが特定のタグを持たないUI要素上にあるかを判定する
    /// </summary>
    /// <returns>特定のタグを持たないUI上にあればtrue</returns>
    private bool IsPointerOverNonMineableUI()
    {
        // PointerEventDataを作成
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        // 現在のマウス位置を設定
        eventData.position = mousePosition;

        // レイキャスト結果を格納するリスト
        List<RaycastResult> results = new List<RaycastResult>();
        // UIレイキャストを実行
        EventSystem.current.RaycastAll(eventData, results);

        // レイキャストにヒットしたUI要素をチェック
        foreach (RaycastResult result in results)
        {
            // "MineableUI" タグが付いていないUI要素であれば、採掘をキャンセル
            if (!result.gameObject.CompareTag("MineableUI"))
            {
                return true;
            }
        }

        // "MineableUI" タグが付いている、またはUIがない場合は採掘を許可
        return false;
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
                UpdateInventoryCapacityUI();
                
                // Debug.Log($"プレイヤーが{resourceType}を回収しました。持ち物: {inventory.GetTotalItemCount()}/{inventory.maxCapacity}");
                
                // インベントリの詳細を出力（インターフェース経由）
                var allRes = inventory.GetAllResources();
                string detailInfo = "インベントリ詳細: ";
                foreach (var kvp in allRes)
                {
                    if (kvp.Value > 0) detailInfo += $"{kvp.Key}:{kvp.Value} ";
                }
                // Debug.Log(detailInfo);
            }
        }
        else
        {
            // Debug.Log("インベントリが満杯です！");
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
            depthText.text = Math.Abs(depth) + "m";//"Depth: " + depth;
        }
    }

    void UpdateInventoryUI()
    {
        if (inventoryText != null)
        {
            var resources = inventory.GetAllResources();
            string inventoryInfo = "";
            
            foreach (var kvp in resources)
            {
                if (kvp.Value > 0)
                {
                    inventoryInfo += $"{kvp.Key}: {kvp.Value}\n";
                }
            }
            
            inventoryText.text = inventoryInfo;
        }
    }

    void UpdateInventoryCapacityUI()
    {
        if (inventoryCapacityText != null && inventory != null)
        {
            inventoryCapacityText.text = $"Item: {inventory.GetTotalItemCount()}/{inventory.maxCapacity}";
        }
    }

    private float GetFluidSubmersionRatio()
    {
        if (!enableFluidResistance || fluidManager == null)
        {
            return 0f;
        }

        Collider sampleCollider = ResolveFluidResistanceCollider();
        Bounds bounds = sampleCollider != null ? sampleCollider.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        Vector3 min = bounds.min + Vector3.one * fluidSampleInset;
        Vector3 max = bounds.max - Vector3.one * fluidSampleInset;

        if (min.x > max.x)
        {
            float centerX = bounds.center.x;
            min.x = centerX;
            max.x = centerX;
        }

        if (min.y > max.y)
        {
            float centerY = bounds.center.y;
            min.y = centerY;
            max.y = centerY;
        }

        if (min.z > max.z)
        {
            float centerZ = bounds.center.z;
            min.z = centerZ;
            max.z = centerZ;
        }

        float totalFillRatio = 0f;
        int sampleCount = 0;

        for (int x = 0; x < fluidHorizontalSampleCount; x++)
        {
            float sampleX = Mathf.Lerp(min.x, max.x, GetFluidSampleLerp(x, fluidHorizontalSampleCount));
            for (int y = 0; y < fluidVerticalSampleCount; y++)
            {
                float sampleY = Mathf.Lerp(min.y, max.y, GetFluidSampleLerp(y, fluidVerticalSampleCount));
                for (int z = 0; z < fluidDepthSampleCount; z++)
                {
                    float sampleZ = Mathf.Lerp(min.z, max.z, GetFluidSampleLerp(z, fluidDepthSampleCount));
                    totalFillRatio += fluidManager.GetFluidFillRatioAtWorldPosition(new Vector3(sampleX, sampleY, sampleZ));
                    sampleCount++;
                }
            }
        }

        return sampleCount > 0 ? Mathf.Clamp01(totalFillRatio / sampleCount) : 0f;
    }

    private float GetFluidResistanceFactor(float fluidSubmersion)
    {
        if (!enableFluidResistance || fluidSubmersion <= 0f)
        {
            return 0f;
        }

        Collider col = ResolveFluidResistanceCollider();
        Vector3 center = col != null ? col.bounds.center : transform.position;

        FluidDefinition fluidDef = fluidManager != null ? fluidManager.GetFluidDefinitionAtWorldPosition(center) : null;
        float fluidDensity = fluidDef != null ? fluidDef.densityKgPerCubicMeter : 1000f;
        
        float volume = col != null ? (col.bounds.size.x * col.bounds.size.y * col.bounds.size.z) : 1f;

        float displacedFluidMass = volume * fluidDensity * fluidSubmersion;
        float effectiveMass = rb != null ? Mathf.Max(0.1f, rb.mass) : 70f;

        float resistanceFactor = Mathf.Clamp01(displacedFluidMass / effectiveMass);
        return Mathf.Clamp01(resistanceFactor * fluidResistanceStrength);
    }

    private Collider ResolveFluidResistanceCollider()
    {
        if (fluidResistanceCollider != null)
        {
            return fluidResistanceCollider;
        }

        Transform playerColliderTransform = transform.Find("PlayerCollider");
        if (playerColliderTransform != null)
        {
            fluidResistanceCollider = playerColliderTransform.GetComponent<Collider>();
            if (fluidResistanceCollider != null)
            {
                return fluidResistanceCollider;
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
            {
                fluidResistanceCollider = colliders[i];
                return fluidResistanceCollider;
            }
        }

        return null;
    }

    private static float GetFluidSampleLerp(int index, int sampleCount)
    {
        if (sampleCount <= 1)
        {
            return 0.5f;
        }

        return index / (float)(sampleCount - 1);
    }

    private void UpdateConstraints()
    {
        if (rb == null) return;

        // すべての物理的な回転を凍結
        rb.freezeRotation = true;

        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }
    
    /// <summary>
    /// インベントリの総数が変更されたときの処理
    /// </summary>
    private void OnInventoryTotalCountChanged(int newTotal)
    {
        // 総数変更時にUIを更新
        UpdateInventoryUI();
        UpdateInventoryCapacityUI();
    }

    /// <summary>
    /// MiningToolsControllerから呼び出され、Animatorに道具の種類を教える
    /// </summary>
    private void OnInventoryFullStateChanged(bool isFull)
    {
        if (!isFull || miningLogSystem == null)
        {
            return;
        }

        miningLogSystem.ShowLog("Itemがいっぱいです！");
    }

    public void SetToolAnimationType(int toolId)
    {
        if (playerVisualsController != null)
        {
            playerVisualsController.SetToolAnimationType(toolId);
        }
    }

    /// <summary>
    /// 外部（MiningToolBehaviour）から呼び出され、採掘アニメーションを開始する
    /// </summary>
    public void TriggerMineAnimation(Vector3 direction)
    {
        if (playerVisualsController != null && miningToolsController != null)
        {
            string stateName = miningToolsController.GetCurrentToolStateName();
            if (!string.IsNullOrEmpty(stateName))
            {
                playerVisualsController.TriggerMineAnimation(stateName, direction);
            }
        }
    }
}

