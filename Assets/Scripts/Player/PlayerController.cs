using UnityEngine;
using UnityEngine.InputSystem; // Input Systemを使うために必要
using UnityEngine.UI; // UIを使うために必要

public class PlayerController : MonoBehaviour
{
    public enum MoveMode
    {
        SideScroller,
        TopDown
    }

    [Header("移動設定")]
    public float moveSpeed = 5f; // 移動速度
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
    
    [Header("参照")]
    public Digger digger; // Diggerへの参照
    private int score = 0;
    private Rigidbody rb;
    private InputSystem_Actions controls; // 自動生成されたクラス
    private Vector2 moveInput;
    private Vector3 lastMoveDirection = Vector3.forward; // 最後に移動した方向

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

        // Rigidbodyの制約を更新
        UpdateConstraints();
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

    // 物理演算の更新タイミングで呼ばれる
    void FixedUpdate()
    {
        Vector3 moveDirection;
        switch (currentMoveMode)
        {
            case MoveMode.SideScroller:
                moveDirection = new Vector3(moveInput.x, moveInput.y, 0f);
                break;
            case MoveMode.TopDown:
                moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
                break;
            default:
                moveDirection = Vector3.zero;
                break;
        }

        // 移動ベクトルを計算
        Vector3 newVelocity = moveDirection.normalized * moveSpeed;
        rb.linearVelocity = newVelocity;

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
            // アイテムをプールに返却
            DroppedItemManager.Instance.ReturnItem(collision.gameObject);
            // スコアを更新
            score++;
            UpdateScoreText();
        }
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
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
}
