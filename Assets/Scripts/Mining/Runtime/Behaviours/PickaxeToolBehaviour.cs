using UnityEngine;

public class PickaxeToolBehaviour : MiningToolBehaviour
{
    [Header("Animator Params")]
    [SerializeField] private string mineTriggerName = "Mine";
    [SerializeField] private string isFacingRightBool = "IsFacingRight";

    private Animator playerAnimator;

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        playerAnimator = user != null ? user.GetComponentInChildren<Animator>() : null;
    }

    public override void Use()
    {
        if (ToolData == null)
        {
            Debug.LogWarning("ToolData is not bound on PickaxeToolBehaviour.");
            return;
        }
        if (digger == null)
        {
            Debug.LogError("Digger component not found on user or its children.");
            return;
        }

        if (ToolData.miningModule == null)
        {
            Debug.LogWarning("MiningModule is not set on tool data.");
            return;
        }

        // 掘削モジュールをセット（アニメイベントでExecuteDigFromAnimationが呼ばれる想定）
        digger.SetPendingMiningModule(ToolData.miningModule);

        // アニメーションを再生
        if (playerAnimator != null)
        {
            // 向き（横スク用）: PlayerControllerに依存する場合はUpdateAimで更新
            playerAnimator.SetTrigger(mineTriggerName);
        }
    }

    public override void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        base.UpdateAim(direction, moveMode);
        if (playerAnimator == null) return;

        // 横スク用の簡易向きフラグ
        if (moveMode == PlayerController.MoveMode.SideScroller)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                bool facingRight = direction.x >= 0f;
                playerAnimator.SetBool(isFacingRightBool, facingRight);
            }
        }
    }

    /// <summary>
    /// アニメーションイベントから呼び出されるメソッド。
    /// Diggerに処理を委譲します。
    /// </summary>
    public void ExecuteDigFromAnimation()
    {
        if (digger != null)
        {
            digger.ExecuteDigFromAnimation();
        }
        else
        {
            Debug.LogError("Digger is not set on PickaxeToolBehaviour. Cannot execute dig from animation.");
        }
    }
}
