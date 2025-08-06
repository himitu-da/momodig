using UnityEngine;

// Note: This script is now a PlayerGenerator. You might want to rename the file to PlayerGenerator.cs
public class SphereGenerator : MonoBehaviour
{
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private Vector3 diggingAreaSize = new Vector3(1, 1, 1);
    [SerializeField] private Vector3 diggingAreaOffset = new Vector3(0, -0.5f, 0);

    void Start()
    {
        // 1. 親となるPlayerオブジェクトを作成
        GameObject player = new GameObject("Player");
        player.transform.position = startPosition;
        player.transform.parent = this.transform;

        // 2. Playerオブジェクトにコンポーネントを追加
        var rb = player.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 不要な回転を防ぐ
        player.AddComponent<PlayerController>();
        var digger = player.AddComponent<Digger>();

        // 3. 見た目用の球(Sphere)を作成し、Playerの子にする
        GameObject sphereVisuals = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereVisuals.transform.parent = player.transform;
        sphereVisuals.transform.localPosition = Vector3.zero;
        // 球自身のコライダーは物理挙動に使うのでトリガーではない
        
        // 4. 掘削範囲用のオブジェクトを作成し、Playerの子にする
        GameObject diggingAreaObject = new GameObject("DiggingArea");
        diggingAreaObject.transform.parent = player.transform;
        diggingAreaObject.transform.localPosition = diggingAreaOffset;
        
        // 5. 掘削範囲にBoxColliderを追加し、トリガーに設定
        var boxCollider = diggingAreaObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = diggingAreaSize;

        // 6. Diggerスクリプトに掘削範囲のColliderを渡す
        digger.SendMessage("SetDiggingArea", boxCollider);
    }
}
