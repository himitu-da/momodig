using UnityEngine;

public class PickaxeToolBehaviour : MiningToolBehaviour
{
    private Transform toolMount; // Diggerの親 = MiningToolsController
    private PlayerController.MoveMode currentMoveMode; // 現在の移動モード
    private Vector3 currentAimDirection = Vector3.right; // 現在の照準方向
    private bool useBuffered = false; // 先行入力用のバッファフラグ
    private bool canBufferUse = false; // 先行入力の受付期間フラグ

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        if (digger != null)
        {
            toolMount = digger.transform.parent;
        }
    }

    public override void Use(Vector3 direction)
    {
        // direction パラメータはインターフェース互換性のために残すが、ここでは使用しない。
        // currentAimDirection は UpdateAim によって常に最新に保たれている。

        Debug.Log($"[PickaxeToolBehaviour] Use called. IsMining: {IsMining}, canBufferUse: {canBufferUse}, useBuffered: {useBuffered}");

        // 既に採掘中の場合は、受付期間中であれば入力をバッファリングする
        if (IsMining)
        {
            if (canBufferUse)
            {
                useBuffered = true;
            }
            return;
        }

        // 新しい採掘を開始する前に、バッファ受付フラグをリセット
        canBufferUse = false;

        // 採掘の向きを物理的に反映させる
        UpdateToolRotation(currentAimDirection, currentMoveMode);

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

        // 掘削情報とアニメーションの向きを決定
        MiningInfo miningInfo;
        var dir = currentAimDirection.normalized;
        float verticalDot = Vector3.Dot(dir, Vector3.up);
        int directionState;

        // 照準がほぼ真上または真下を向いているか判定 (cos(18 deg) ~= 0.95)
        if (Mathf.Abs(verticalDot) > 0.95f)
        {
            // 上下方向の掘削
            bool isUp = verticalDot > 0;
            var centerOffset = pickaxeModule.VerticalDiggingCenter;
            // 下向きの場合はオフセットのYを反転
            if (!isUp)
            {
                centerOffset.y *= -1;
            }
            
            // 掘削範囲をDiggerに直接設定
            digger.SetDiggingAreaParameters(centerOffset, pickaxeModule.VerticalDiggingSize);

            // 爆心地を掘削範囲の中心に設定
            Vector3 explosionCenter = digger.transform.TransformPoint(centerOffset);

            // 爆発タイプのMiningInfoを作成
            miningInfo = MiningInfo.Explosive(
                explosionCenter,
                pickaxeModule.VerticalMiningForce
            );
            directionState = isUp ? 2 : 3; // 2: 上, 3: 下
        }
        else
        {
            // 左右方向の掘削
            // Diggerはアニメーションイベントでモジュールのデフォルト値(Horizontal)を使用するため、
            // ここで掘削範囲を明示的に設定する必要はない。
            bool isFacingRight = dir.x >= 0;
            miningInfo = MiningInfo.ArcSwing(
                digger.transform.position,
                isFacingRight,
                pickaxeModule.MiningForce
            );
            directionState = isFacingRight ? 0 : 1; // 0: 右, 1: 左
        }

        // 掘削モジュールと掘削情報をセット（アニメイベントでExecuteDigFromAnimationが呼ばれる想定）
        digger.SetPendingMining(pickaxeModule, miningInfo);

        // アニメーションを再生
        if (playerAnimator != null)
        {
            IsMining = true;
            playerAnimator.SetInteger(pickaxeModule.DirectionStateName, directionState);
            playerAnimator.SetTrigger(pickaxeModule.MineTriggerName);
        }
    }

    public override void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        base.UpdateAim(direction, moveMode);
        currentMoveMode = moveMode; // 移動モードをキャッシュ
        if (direction.sqrMagnitude > 0.001f)
        {
            currentAimDirection = direction.normalized;
        }

        // 採掘中でないときは、常にツールの向きを照準に追従させる
        if (!IsMining)
        {
            UpdateToolRotation(currentAimDirection, currentMoveMode);
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

    /// <summary>
    /// アニメーションの終了時に呼び出されるメソッド。
    /// </summary>
    public void OnMineAnimationEnd()
    {
        IsMining = false;

        // バッファされた入力があれば、即座に次のUseを実行
        if (useBuffered)
        {
            useBuffered = false;
            // バッファされた入力でUseを呼び出す際は、最後に更新された向き(currentAimDirection)をそのまま使う
            Use(currentAimDirection);
        }
        else
        {
            // バッファされた入力がない場合は、受付フラグもリセット
            canBufferUse = false;
        }
    }

    /// <summary>
    /// アニメーションイベントから呼び出し、先行入力の受付を開始するメソッド。
    /// </summary>
    public void OpenBufferWindow()
    {
        canBufferUse = true;
    }

    /// <summary>
    /// ツールホルダー自体の向きを更新する
    /// </summary>
    private void UpdateToolRotation(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        if (toolMount == null) return;

        Quaternion targetRotation;
        if (moveMode == PlayerController.MoveMode.TopDown)
        {
            // TopDownモードの回転計算
            float angle = Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
            targetRotation = Quaternion.AngleAxis(angle, Vector3.up);
        }
        else // SideScroller
        {
            // SideScrollerモードの回転計算
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        // このオブジェクト（MiningTools）の向きを更新
        toolMount.rotation = targetRotation;
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
