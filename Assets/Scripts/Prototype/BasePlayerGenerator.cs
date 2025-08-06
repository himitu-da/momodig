using UnityEngine;

public abstract class BasePlayerGenerator : MonoBehaviour
{
    [SerializeField] protected Vector3 startPosition;
    [SerializeField] protected Vector3 diggingAreaSize = new Vector3(1, 1, 1);
    // プレイヤーの前方(Z軸)にオフセットするように変更
    [SerializeField] protected Vector3 diggingAreaOffset = new Vector3(0, 0, 1.0f);

    protected abstract PlayerController.MoveMode moveMode { get; }

    void Start()
    {
        // 1. 親となるPlayerオブジェクトを作成
        GameObject player = new GameObject("Player");
        player.transform.position = startPosition;
        player.transform.parent = this.transform;

        // 2. Playerオブジェクトにコンポーネントを追加
        var rb = player.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 不要な回転を防ぐ
        var playerController = player.AddComponent<PlayerController>();

        // PlayerControllerに移動モードを設定
        playerController.currentMoveMode = moveMode;

        // 3. 見た目用の球(Sphere)を作成し、Playerの子にする
        GameObject sphereVisuals = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereVisuals.transform.parent = player.transform;
        sphereVisuals.transform.localPosition = Vector3.zero;
        
        // 4. 掘削範囲用のオブジェクトを作成し、Playerの子にする
        GameObject diggingAreaObject = new GameObject("DiggingArea");
        diggingAreaObject.transform.parent = player.transform;
        diggingAreaObject.transform.localPosition = diggingAreaOffset;

        // 5. 掘削範囲にBoxColliderとDiggerを追加
        var boxCollider = diggingAreaObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = diggingAreaSize;
        var digger = diggingAreaObject.AddComponent<Digger>();

        // 6. Diggerスクリプトに掘削範囲のColliderを渡す
        digger.SetDiggingArea(boxCollider);
    }
}
