using UnityEngine;

public class MinecartMovement : MonoBehaviour
{
    public Vector3 targetPosition;
    [SerializeField] private float baseMoveSpeed = 5f;
    public Stat moveSpeed = new Stat();

    void Start()
    {
        moveSpeed.BaseValue = baseMoveSpeed;
        // 初期位置をターゲット位置に設定
        targetPosition = transform.position;
    }

    void Update()
    {
        // 目標位置と現在の位置が十分に離れていたら移動する
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            // MoveTowardsを使って一定の速度で移動
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed.Value * Time.deltaTime);
        }
    }
}
