using UnityEngine;

public class Block : MonoBehaviour
{
    private int maxHp = 3;
    private int currentHp;
    private Renderer blockRenderer;
    private Color initialColor;

    void Awake()
    {
        blockRenderer = GetComponent<Renderer>();
    }

    void Start()
    {
        currentHp = maxHp;
        initialColor = blockRenderer.material.color;
        UpdateVisuals();
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        UpdateVisuals();
        if (currentHp <= 0)
        {
            // アイテムをドロップ
            DropItem(transform.position);
            Destroy(gameObject);
        }
    }

    void UpdateVisuals()
    {
        float healthPercentage = (float)currentHp / maxHp;

        // HPの割合に応じて、初期色から黒へ線形補間します。
        Color healthColor = Color.Lerp(Color.black, initialColor, healthPercentage);

        // 色の変化に加えて、透明度もHPに応じて変更します。(メモ: URPでリアルタイムに透明度を更新するには、マテリアルのSurface TypeをTransparentにし、シェーダーがアルファ値に対応している必要がありますが、現状うまく動作しないためコメントアウトしています)
        // healthColor.a = healthPercentage;

        // マテリアルの色（透明度含む）を直接更新します。
        // これにより、このオブジェクト専用のマテリアルインスタンスが作成されます。
        blockRenderer.material.color = healthColor;
    }

    void DropItem(Vector3 position)
    {
        // Cubeを生成してアイテムとする
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.transform.position = position;
        item.transform.localScale = transform.localScale * 0.5f;

        // Rigidbodyを追加
        item.AddComponent<Rigidbody>();

        // 回転スクリプトを追加
        item.AddComponent<DroppedItem>();

        // タグを設定
        item.tag = "DroppedItem";
    }
}
