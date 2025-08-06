using UnityEngine;

public class Digger : MonoBehaviour
{
    private BoxCollider diggingArea; // 掘削範囲のBoxCollider

    // SphereGeneratorから呼び出される
    public void SetDiggingArea(BoxCollider area)
    {
        diggingArea = area;
    }

    // PlayerControllerから呼び出される
    public void UpdateDiggingAreaTransform(Vector3 position, Quaternion rotation)
    {
        if (diggingArea != null)
        {
            // Player自体が回転するため、Diggerのローカル回転はリセットし、
            // 位置のオフセットのみを設定する
            diggingArea.transform.localPosition = position;
            diggingArea.transform.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        // 左クリックされたら
        if (Input.GetMouseButtonDown(0))
        {
            Dig();
        }

        // ゲームビューにデバッグ用のボックスを描画
        DrawDebugBox();
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

    // Gizmoを描画する
    void OnDrawGizmos()
    {
        // OnDrawGizmosはシーンビューでのみ表示されるため、
        // ゲームビューでの表示はUpdate内のDrawDebugBoxで行う
        if (diggingArea != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = diggingArea.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(diggingArea.center, diggingArea.size);
        }
    }

    // ゲームビューにデバッグ用のボックスを描画する
    void DrawDebugBox()
    {
        if (diggingArea == null) return;

        Vector3 center = diggingArea.transform.TransformPoint(diggingArea.center);
        Vector3 size = diggingArea.size;
        Quaternion rotation = diggingArea.transform.rotation;

        Vector3 halfSize = size / 2;
        Vector3[] points = new Vector3[8];
        points[0] = rotation * new Vector3(-halfSize.x, -halfSize.y, -halfSize.z) + center;
        points[1] = rotation * new Vector3( halfSize.x, -halfSize.y, -halfSize.z) + center;
        points[2] = rotation * new Vector3( halfSize.x, -halfSize.y,  halfSize.z) + center;
        points[3] = rotation * new Vector3(-halfSize.x, -halfSize.y,  halfSize.z) + center;
        points[4] = rotation * new Vector3(-halfSize.x,  halfSize.y, -halfSize.z) + center;
        points[5] = rotation * new Vector3( halfSize.x,  halfSize.y, -halfSize.z) + center;
        points[6] = rotation * new Vector3( halfSize.x,  halfSize.y,  halfSize.z) + center;
        points[7] = rotation * new Vector3(-halfSize.x,  halfSize.y,  halfSize.z) + center;

        Color color = Color.green;
        Debug.DrawLine(points[0], points[1], color);
        Debug.DrawLine(points[1], points[2], color);
        Debug.DrawLine(points[2], points[3], color);
        Debug.DrawLine(points[3], points[0], color);

        Debug.DrawLine(points[4], points[5], color);
        Debug.DrawLine(points[5], points[6], color);
        Debug.DrawLine(points[6], points[7], color);
        Debug.DrawLine(points[7], points[4], color);

        Debug.DrawLine(points[0], points[4], color);
        Debug.DrawLine(points[1], points[5], color);
        Debug.DrawLine(points[2], points[6], color);
        Debug.DrawLine(points[3], points[7], color);
    }
}
