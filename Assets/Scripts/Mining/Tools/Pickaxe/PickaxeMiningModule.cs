using UnityEngine;

[CreateAssetMenu(fileName = "PickaxeMiningModule", menuName = "MomoDig/Mining/Modules/Pickaxe")]
public class PickaxeMiningModule : MiningModule
{
    [Header("Horizontal Dig Settings")]
    [Tooltip("掘削範囲の中心のオフセット")]
    [SerializeField] private Vector3 centerOffset = new Vector3(1, 0, 0);

    [Tooltip("掘削範囲のサイズ")]
    [SerializeField] private Vector3 size = new Vector3(2, 1.8f, 1);

    [Tooltip("水平方向でアイテムに与える力の強さ")]
    [SerializeField] private float miningForce = 5f;

    [Header("Vertical Dig Settings")]
    [Tooltip("上下方向の掘削範囲の中心のオフセット")]
    [SerializeField] private Vector3 verticalCenterOffset = new Vector3(0, 1.5f, 0);

    [Tooltip("上下方向の掘削範囲のサイズ")]
    [SerializeField] private Vector3 verticalSize = new Vector3(1, 2, 1);

    [Tooltip("上下方向でアイテムに与える力の強さ")]
    [SerializeField] private float verticalMiningForce = 10f;

    [Header("Animator Params")]
    [Tooltip("アニメーションを発火させるトリガー名")]
    [SerializeField] private string mineTriggerName = "Mine";
    
    [Tooltip("プレイヤーの向きをAnimatorに伝えるためのBoolパラメータ名")]
    [SerializeField] private string isFacingRightBool = "IsFacingRight";

    [Tooltip("プレイヤーの向きの状態をAnimatorに伝えるためのIntegerパラメータ名")]
    [SerializeField] private string directionStateName = "DirectionState";

    // Horizontal
    public override Vector3 DiggingCenter => centerOffset;
    public override Vector3 DiggingSize => size;
    public float MiningForce => miningForce;

    // Vertical
    public Vector3 VerticalDiggingCenter => verticalCenterOffset;
    public Vector3 VerticalDiggingSize => verticalSize;
    public float VerticalMiningForce => verticalMiningForce;

    // Animator
    public string MineTriggerName => mineTriggerName;
    public string IsFacingRightBool => isFacingRightBool;
    public string DirectionStateName => directionStateName;

    public override void Execute(GameObject user)
    {
        // このメソッドはアニメーションイベント経由で実行されるようになったため、
        // ここでの処理は不要になります。
        // Digger.ExecuteDigFromAnimation() が実際の処理を担当します。
    }
}
