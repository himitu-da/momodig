using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "Pickaxe", menuName = "MomoDig/Mining/Tools/Pickaxe")]
public class Pickaxe : MiningTool
{
    public override void Use(GameObject user)
    {
        // Diggerコンポーネントを取得
        Digger digger = user.GetComponentInChildren<Digger>();
        if (digger == null)
        {
            Debug.LogError("Digger component not found on the user or its children.");
            return;
        }

        // MiningModuleが設定されているか確認
        if (miningModule == null)
        {
            Debug.LogWarning($"MiningModule is not set for {toolName}.");
            return;
        }

        // Diggerに掘削モジュールをセット
        digger.SetPendingMiningModule(miningModule);

        // Animatorコンポーネントを取得してアニメーションを再生
        Animator animator = user.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // PlayerControllerから向きの情報を取得
            PlayerController playerController = user.GetComponent<PlayerController>();
            if (playerController != null)
            {
                animator.SetBool("IsFacingRight", playerController.IsFacingRight);
            }

            // "Mine"トリガーでアニメーションを再生
            animator.SetTrigger("Mine");
        }
        else
        {
            Debug.LogWarning($"Animator component not found on {user.name} or its children.");
        }
    }
}