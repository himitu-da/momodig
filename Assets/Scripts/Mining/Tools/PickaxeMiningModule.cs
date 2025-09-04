using UnityEngine;

[CreateAssetMenu(fileName = "PickaxeMiningModule", menuName = "MomoDig/Mining/Modules/Pickaxe")]
public class PickaxeMiningModule : MiningModule
{
    [Header("Box Dig Settings")]
    [Tooltip("掘削範囲の中心のオフセット")]
    [SerializeField] private Vector3 centerOffset = new Vector3(1, 0, 0);

    [Tooltip("掘削範囲のサイズ")]
    [SerializeField] private Vector3 size = new Vector3(2, 1.8f, 1);

    public override Vector3 DiggingCenter => centerOffset;
    public override Vector3 DiggingSize => size;

    public override void Execute(GameObject user)
    {
        // このメソッドはアニメーションイベント経由で実行されるようになったため、
        // ここでの処理は不要になります。
        // Digger.ExecuteDigFromAnimation() が実際の処理を担当します。
    }
}
