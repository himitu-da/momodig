using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "Pickaxe", menuName = "MomoDig/Mining/Tools/Pickaxe")]
public class Pickaxe : MiningTool
{
    public override void Use(GameObject user)
    {
        // userオブジェクトとその子オブジェクトからAnimatorコンポーネントを取得
        Animator animator = user.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // PlayerControllerから向きの情報を取得
            PlayerController playerController = user.GetComponent<PlayerController>();
            if (playerController != null)
            {
                animator.SetBool("IsFacingRight", playerController.IsFacingRight);
            }

            // "Mine"という名前のトリガーをセットして、アニメーションを再生
            animator.SetTrigger("Mine");
        }
        else
        {
            Debug.LogWarning($"Animator component not found on {user.name} or its children.");
        }

        if (miningModule != null)
        {
            miningModule.Execute(user);
        }
        else
        {
            Debug.LogWarning($"MiningModule is not set for {toolName}.");
        }
    }
}