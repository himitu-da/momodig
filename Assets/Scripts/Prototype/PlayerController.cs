using UnityEngine;
using UnityEngine.InputSystem; // Input Systemを使うために必要
using UnityEngine.UI; // UIを使うために必要

public class PlayerController : MonoBehaviour
{
    public enum MoveMode
    {
        Vertical,
        Horizonal
    }

    public float moveSpeed = 5f; // 移動速度
    public Text scoreText; // スコア表示用のText
    public MoveMode currentMoveMode { get; set; }
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
        // 子オブジェクトからDiggerコンポーネントを取得
        digger = GetComponentInChildren<Digger>();
        controls = new InputSystem_Actions();

        // "Move" アクションが実行された時(キーが押された/離された時)に呼ばれる処理を登録
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Textコンポーネントを探して、それをscoreTextに追加
        scoreText = GameObject.Find("ScoreText").GetComponent<Text>();
        UpdateScoreText();
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
            case MoveMode.Vertical:
                moveDirection = new Vector3(moveInput.x, moveInput.y, 0f);
                break;
            case MoveMode.Horizonal:
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
            if (currentMoveMode == MoveMode.Horizonal)
            {
                // 進行方向を向く回転を計算
                targetRotation = Quaternion.LookRotation(lastMoveDirection);
            }
            else // Vertical
            {
                // XY平面での2Dの回転。オブジェクトの「上」が進行方向を向くようにする
                float angle = Mathf.Atan2(lastMoveDirection.y, lastMoveDirection.x) * Mathf.Rad2Deg;
                targetRotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
            }
            // Rigidbodyを使って回転させる
            rb.MoveRotation(targetRotation);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // 衝突したオブジェクトが "DroppedItem" タグを持っているか確認
        if (collision.gameObject.CompareTag("DroppedItem"))
        {
            // アイテムを破壊
            Destroy(collision.gameObject);
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
}
