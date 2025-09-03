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
        // Playerの子オブジェクトからDiggerコンポーネントを探して実行
        Digger digger = user.GetComponentInChildren<Digger>();
        if (digger != null)
        {
            // 掘削範囲を設定してから掘削を実行
            digger.SetDiggingAreaParameters(centerOffset, size);
            digger.Dig();
        }
        else
        {
            Debug.LogError("Digger component not found on the user or its children.");
        }
    }
}
