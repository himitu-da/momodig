using UnityEngine;
using System.Collections.Generic;

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

        // diggingAreaのワールド中心を計算
        Vector3 worldCenter = diggingArea.transform.TransformPoint(diggingArea.center);

        // OverlapBoxで範囲内のすべてのコライダーを取得（中心を正しく使用）
        Collider[] hitColliders = Physics.OverlapBox(
            worldCenter,
            diggingArea.size / 2,
            diggingArea.transform.rotation
        );

        // ユニークなチャンクを収集（複数ヒット回避）
        HashSet<Block> hitChunks = new HashSet<Block>();
        foreach (var hitCollider in hitColliders)
        {
            Block chunk = hitCollider.GetComponent<Block>();
            if (chunk != null)
                hitChunks.Add(chunk);
        }

        // BoxColliderのワールド空間での8つの頂点を計算し、それらを完全に含むAABB (Axis-Aligned Bounding Box) を作成します。
        // これにより、回転したBoxColliderも正確に表現できます。
        var points = new Vector3[8];
        var center = diggingArea.center;
        var size = diggingArea.size / 2;
        points[0] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z));
        points[1] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z));
        points[2] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z));
        points[3] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z));
        points[4] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z));
        points[5] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z));
        points[6] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, size.y, size.z));
        points[7] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z));

        foreach (var chunk in hitChunks)
        {
            // BoxCollider自体を渡して、より正確な判定をチャンク側で行う
            chunk.DigVoxels(diggingArea);
        }

        // 掘削範囲内のドロップアイテムを起床させる
        if (DroppedItemManager.Instance != null)
        {
            Vector3 expandedSize = diggingArea.size + new Vector3(2, 2, 2);
            DroppedItemManager.Instance.WakeUpItemsInRadius(worldCenter, expandedSize, diggingArea.transform.rotation);
        }
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
