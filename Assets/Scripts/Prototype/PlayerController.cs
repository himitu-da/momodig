using UnityEngine;
using UnityEngine.InputSystem; // Input Systemを使うために必要
using UnityEngine.UI; // UIを使うために必要

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f; // 移動速度
    public Text scoreText; // スコア表示用のText

    private int score = 0;
    private Rigidbody rb;
    private InputSystem_Actions controls; // 自動生成されたクラス
    private Vector2 moveInput;

    // スクリプトがロードされたときに一度だけ呼ばれる
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false; // Rigidbodyの重力を無効にする
        }
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
        // 左右(x)と上下(y)の入力を使って移動ベクトルを作成
        Vector3 newVelocity = new Vector3(moveInput.x * moveSpeed, moveInput.y * moveSpeed, 0f);
        rb.linearVelocity = newVelocity;
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
