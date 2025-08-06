using UnityEngine;

public class Digger : MonoBehaviour
{
    private BoxCollider diggingArea; // 掘削範囲のBoxCollider

    // SphereGeneratorから呼び出される
    public void SetDiggingArea(BoxCollider area)
    {
        diggingArea = area;
    }

    void Update()
    {
        // 左クリックされたら
        if (Input.GetMouseButtonDown(0))
        {
            Dig();
        }
    }

    void Dig()
    {
        if (diggingArea == null)
        {
            Debug.LogError("Digging Area is not set.");
            return;
        }

        // OverlapBoxで範囲内のすべてのコライダーを取得
        Collider[] hitColliders = Physics.OverlapBox(
            diggingArea.transform.position, 
            diggingArea.size / 2, 
            diggingArea.transform.rotation
        );

        // 取得したコライダーをループ
        foreach (var hitCollider in hitColliders)
        {
            // "Block"タグが付いているオブジェクトを破壊
            if (hitCollider.CompareTag("Block"))
            {
                // アイテムをドロップ
                DropItem(hitCollider.transform.position);
                Destroy(hitCollider.gameObject);
            }
        }
    }

    void DropItem(Vector3 position)
    {
        // Cubeを生成してアイテムとする
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.transform.position = position;
        item.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        // Rigidbodyを追加
        item.AddComponent<Rigidbody>();

        // 回転スクリプトを追加
        item.AddComponent<DroppedItem>();

        // タグを設定
        item.tag = "DroppedItem";
    }
}
