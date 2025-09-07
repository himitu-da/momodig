using UnityEngine;

public class PickaxeToolBehaviour : MiningToolBehaviour
{
    private Animator playerAnimator;
    private Vector3 currentAimDirection = Vector3.right; // 現在の照準方向

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
        
        if (!(ToolData.miningModule is PickaxeMiningModule pickaxeModule))
        {
            Debug.LogError("MiningModule on ToolData is not a PickaxeMiningModule.");
            return;
        }

        // 掘削情報を作成
        MiningInfo miningInfo;
        float verticalDot = Vector3.Dot(currentAimDirection.normalized, Vector3.up);

        // 照準がほぼ真上または真下を向いているか判定 (cos(18 deg) ~= 0.95)
        if (Mathf.Abs(verticalDot) > 0.95f)
        {
            // 真上/真下の場合は、照準方向にまっすぐ飛ばす
            miningInfo = MiningInfo.Directional(
                digger.transform.position,
                currentAimDirection,
                pickaxeModule.MiningForce
            );
        }
        else
        {
            // それ以外の角度の場合は、円弧状に飛ばす
            bool isFacingRight = playerAnimator != null && playerAnimator.GetBool(pickaxeModule.IsFacingRightBool);
            miningInfo = MiningInfo.ArcSwing(
                digger.transform.position,
                isFacingRight,
                pickaxeModule.MiningForce
            );
        }

        // 掘削モジュールと掘削情報をセット（アニメイベントでExecuteDigFromAnimationが呼ばれる想定）
        digger.SetPendingMining(ToolData.miningModule, miningInfo);

        // アニメーションを再生
        if (playerAnimator != null)
        {
            // 向き（横スク用）: PlayerControllerに依存する場合はUpdateAimで更新
            playerAnimator.SetTrigger(pickaxeModule.MineTriggerName);
        }
    }

    public override void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        base.UpdateAim(direction, moveMode);
        if (direction.sqrMagnitude > 0.001f)
        {
            currentAimDirection = direction.normalized;
        }

        if (playerAnimator == null) return;

        // 横スク用の簡易向きフラグ
        if (moveMode == PlayerController.MoveMode.SideScroller)
        {
            if (ToolData.miningModule is PickaxeMiningModule pickaxeModule)
            {
                if (direction.sqrMagnitude > 0.0001f)
                {
                    bool facingRight = direction.x >= 0f;
                    playerAnimator.SetBool(pickaxeModule.IsFacingRightBool, facingRight);
                }
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

    private void OnDrawGizmos()
    {
        if (ToolData == null || ToolData.miningModule == null || digger == null || digger.DiggingArea == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        // 掘削エリアのBoxColliderから情報を取得してGizmosを描画
        BoxCollider area = digger.DiggingArea;
        Gizmos.matrix = area.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
