using UnityEngine;

[CreateAssetMenu(fileName = "PickaxeMiningModule", menuName = "MomoDig/Mining/Modules/Pickaxe")]
public class PickaxeMiningModule : MiningModule
{
    public override void Execute(GameObject user)
    {
        // Playerの子オブジェクトからDiggerコンポーネントを探して実行
        Digger digger = user.GetComponentInChildren<Digger>();
        if (digger != null)
        {
            digger.Dig();
        }
        else
        {
            Debug.LogError("Digger component not found on the user or its children.");
        }
    }
}