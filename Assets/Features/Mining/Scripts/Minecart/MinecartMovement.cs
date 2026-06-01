using UnityEngine;

public class MinecartMovement : MonoBehaviour
{
    public Vector3 targetPosition;
    public float moveSpeed = 5f;
    public bool IsMovementPaused { get; private set; }

    void Start()
    {
        // 初期位置をターゲット位置に設定
        targetPosition = transform.position;
    }

    void Update()
    {
        if (IsMovementPaused)
        {
            return;
        }

        // 目標位置と現在の位置が十分に離れていたら移動する
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            // MoveTowardsを使って一定の速度で移動
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
    }

    public void SetMovementPaused(bool paused)
    {
        IsMovementPaused = paused;
        if (paused)
        {
            targetPosition = transform.position;
        }
    }
}
