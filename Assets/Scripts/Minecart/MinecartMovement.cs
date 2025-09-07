using UnityEngine;

public class MinecartMovement : MonoBehaviour
{
    public Vector3 targetPosition;
    [SerializeField] private float moveSpeed = 5f;

    void Start()
    {
        // 初期位置をターゲット位置に設定
        targetPosition = transform.position;
    }

    void Update()
    {
        // 目標位置と現在の位置が十分に離れていたら移動する
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            // Lerp（線形補間）を使って滑らかに移動
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
        }
    }
}
