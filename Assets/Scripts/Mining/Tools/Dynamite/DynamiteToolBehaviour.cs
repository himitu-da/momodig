using UnityEngine;

public class DynamiteToolBehaviour : MiningToolBehaviour
{
    private GameObject owner;
    private PlayerController playerController;

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        owner = user;
        
        // userはMiningToolsなので、親オブジェクト（Player）からPlayerControllerを取得
        playerController = user != null ? user.GetComponentInParent<PlayerController>() : null;
    }

    public override void Use(Vector3 direction, PlayerController playerController)
    {
        if (!IsEquipped) return;

        // Playerのアニメーションを開始するよう通知 (投げるモーションなど)
        playerController.TriggerMineAnimation();

        if (owner == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: owner is null.");
            return;
        }

        // ToolData と Module が必須
        if (ToolData.miningModule == null)
        {
            Debug.LogWarning("MiningModule is not set on tool data.");
            return;
        }
        if (!(ToolData.miningModule is DynamiteMiningModule dynamiteModule))
        {
            Debug.LogError("MiningModule on ToolData is not a DynamiteMiningModule.");
            return;
        }

        GameObject projectilePrefab = dynamiteModule.ProjectilePrefab;
        float force = dynamiteModule.ThrowForce;
        float maxDistance = dynamiteModule.MaxThrowDistance;
        float gravityValue = dynamiteModule.Gravity;

        if (projectilePrefab == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: projectile prefab is not assigned.");
            return;
        }

        bool isTopDown = playerController != null && playerController.currentMoveMode == PlayerController.MoveMode.TopDown;
        Vector3 dir;
        
        if (isTopDown)
        {
            // TopDown: マウス方向への直接投射（従来と同様）
            dir = ComputeQuantizedDirection(owner, playerController, isTopDown);
        }
        else
        {
            // SideScroller: マウス位置を目標とした放物運動
            dir = ComputeBallisticDirection(owner, playerController);
        }
        
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = isTopDown ? new Vector3(1f, 0f, 0f) : new Vector3(1f, 0f, 0f);
        }

        // プレイヤーの中心位置から発射（spawnDistanceを使わない）
        Vector3 spawnPos = owner.transform.position;
        var go = Object.Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        var proj = go.GetComponent<DynamiteProjectile>();
        if (proj == null) proj = go.AddComponent<DynamiteProjectile>();
        proj.SetBehaviour(this);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = !isTopDown;

        Vector3 velocity;
        if (isTopDown)
        {
            // TopDown: 従来通りの直接速度設定
            velocity = dir.normalized * force;
        }
        else
        {
            // SideScroller: 放物運動用の初速度を再計算
            Vector3 startPos = owner.transform.position;
            Vector3 targetPos = playerController.GetMouseWorldPosition(10f);
            targetPos.z = startPos.z; // Z座標を固定
            
            float horizontalDistance = targetPos.x - startPos.x;
            float verticalDistance = targetPos.y - startPos.y;
            
            // 距離制限
            float totalDistance = Mathf.Sqrt(horizontalDistance * horizontalDistance + verticalDistance * verticalDistance);
            if (totalDistance > maxDistance)
            {
                Vector3 throwDirection = (targetPos - startPos).normalized;
                targetPos = startPos + throwDirection * maxDistance;
                horizontalDistance = targetPos.x - startPos.x;
                verticalDistance = targetPos.y - startPos.y;
            }
            
            velocity = CalculateBallisticVelocity(horizontalDistance, verticalDistance, gravityValue);
        }

        // プロジェクトの Rigidbody 拡張: linearVelocity を使用
        rb.linearVelocity = velocity;
    }

    public override void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        base.UpdateAim(direction, moveMode);
        // Dynamite は特に Aim 反映不要（必要ならここでUI等更新）
    }

    private Vector3 ComputeQuantizedDirection(GameObject user, PlayerController pc, bool isTopDown)
    {
        if (pc != null)
        {
            if (isTopDown)
            {
                // TopDown: XZ 平面
                Vector2 base2 = new Vector2(pc.lastMoveDirection.x, pc.lastMoveDirection.z);
                Vector2 q = Quantize8(base2);
                if (q.sqrMagnitude < 0.5f)
                {
                    Vector3 fwd = user.transform.forward;
                    q = Quantize8(new Vector2(fwd.x, fwd.z));
                }
                return new Vector3(q.x, 0f, q.y);
            }
            else
            {
                // SideScroller: XY 平面
                Vector2 base2 = new Vector2(pc.lastMoveDirection.x, pc.lastMoveDirection.y);
                Vector2 q = Quantize8(base2);
                if (q.sqrMagnitude < 0.5f)
                {
                    float sign = pc.IsFacingRight ? 1f : -1f;
                    q = Quantize8(new Vector2(sign, 0f));
                }
                return new Vector3(q.x, q.y, 0f);
            }
        }

        // フォールバック
        Vector3 fw = user != null ? user.transform.forward : Vector3.right;
        if (isTopDown)
        {
            Vector2 q = Quantize8(new Vector2(fw.x, fw.z));
            return new Vector3(q.x, 0f, q.y);
        }
        else
        {
            float sx = Mathf.Approximately(fw.x, 0f) ? 1f : Mathf.Sign(fw.x);
            Vector2 q = Quantize8(new Vector2(sx, 0f));
            return new Vector3(q.x, q.y, 0f);
        }
    }
    
    /// <summary>
    /// SideScrollerモードでマウス位置を目標とした放物運動の方向を計算
    /// </summary>
    private Vector3 ComputeBallisticDirection(GameObject user, PlayerController pc)
    {
        if (pc == null) return Vector3.right;
        
        // Moduleからパラメータを取得
        if (!(ToolData.miningModule is DynamiteMiningModule dynamiteModule))
        {
            return Vector3.right;
        }
        
        float maxDistance = dynamiteModule.MaxThrowDistance;
        
        // プレイヤーの中心位置とマウスのワールド座標を取得
        Vector3 startPos = user.transform.position; // プレイヤーの中心位置
        Vector3 targetPos = pc.GetMouseWorldPosition(10f); // Z=0平面でのワールド座標
        
        // SideScrollerなのでZ座標を0に固定
        targetPos.z = startPos.z;
        
        // 水平距離と高度差を計算
        float horizontalDistance = targetPos.x - startPos.x;
        float verticalDistance = targetPos.y - startPos.y;
        
        // 距離制限をチェック（必要に応じて）
        float totalDistance = Mathf.Sqrt(horizontalDistance * horizontalDistance + verticalDistance * verticalDistance);
        if (totalDistance > maxDistance)
        {
            // 最大距離を超える場合は方向を維持して距離を制限
            Vector3 throwDirection = (targetPos - startPos).normalized;
            targetPos = startPos + throwDirection * maxDistance;
            horizontalDistance = targetPos.x - startPos.x;
            verticalDistance = targetPos.y - startPos.y;
        }
        
        // 放物運動の初速度を計算（角度制限なし）
        Vector3 velocity = CalculateBallisticVelocity(horizontalDistance, verticalDistance, dynamiteModule.Gravity);
        
        return velocity.normalized;
    }
    
    /// <summary>
    /// 放物運動で目標点に到達するための初速度ベクトルを計算
    /// </summary>
    private Vector3 CalculateBallisticVelocity(float horizontalDistance, float verticalDistance, float gravityValue)
    {
        // 水平距離が0に近い場合は垂直投射
        if (Mathf.Abs(horizontalDistance) < 0.1f)
        {
            float vY = verticalDistance > 0 ? Mathf.Sqrt(2 * gravityValue * Mathf.Abs(verticalDistance)) : -Mathf.Sqrt(2 * gravityValue * Mathf.Abs(verticalDistance));
            return new Vector3(0f, vY, 0f);
        }
        
        // 固定時間法を使用（より確実で直感的）
        float x = horizontalDistance;
        float y = verticalDistance;
        float g = gravityValue;
        
        // 適切な飛行時間を推定（距離に基づく）
        float distance = Mathf.Sqrt(x * x + y * y);
        float timeOfFlight = Mathf.Sqrt(2 * distance / g); // 基本的な推定
        
        // 目標が遠い場合や高い場合は時間を調整
        if (distance > 10f || y > 5f)
        {
            timeOfFlight *= 1.5f;
        }
        else if (y < -2f)
        {
            timeOfFlight *= 0.7f; // 下向きの場合は短く
        }
        
        // 初速度を計算
        float vx = x / timeOfFlight;
        float vy = (y + 0.5f * g * timeOfFlight * timeOfFlight) / timeOfFlight;
        
        return new Vector3(vx, vy, 0f);
    }
    
    // 8方向に量子化（E, NE, N, NW, W, SW, S, SE）
    private Vector2 Quantize8(Vector2 v)
    {
        if (v.sqrMagnitude < 1e-6f) return Vector2.zero;

        float angle = Mathf.Atan2(v.y, v.x); // [-pi, pi]
        float step = Mathf.PI / 4f;          // 45°
        int idx = Mathf.RoundToInt(angle / step);
        idx = (idx % 8 + 8) % 8;             // 0..7 に正規化

        switch (idx)
        {
            case 0:  return new Vector2(1f, 0f);                          // E
            case 1:  return new Vector2(0.70710678f, 0.70710678f);        // NE
            case 2:  return new Vector2(0f, 1f);                          // N
            case 3:  return new Vector2(-0.70710678f, 0.70710678f);       // NW
            case 4:  return new Vector2(-1f, 0f);                         // W
            case 5:  return new Vector2(-0.70710678f, -0.70710678f);      // SW
            case 6:  return new Vector2(0f, -1f);                         // S
            case 7:  return new Vector2(0.70710678f, -0.70710678f);       // SE
            default: return new Vector2(1f, 0f);
        }
    }

    /// <summary>
    /// 爆発による掘削を実行（Projectile から呼び出し）
    /// </summary>
    public void PerformExplosionMining(Vector3 explosionPosition, Vector3 center, Vector3 size, int damage, float force)
    {
        if (digger == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: Digger is not set.");
            return;
        }

        // Digger の位置を爆発位置に一時的に設定
        Vector3 originalPosition = digger.transform.position;
        digger.transform.position = explosionPosition;

        // BoxCollider を設定
        BoxCollider diggingArea = digger.GetComponent<BoxCollider>();
        if (diggingArea != null)
        {
            diggingArea.center = center;
            diggingArea.size = size;
            diggingArea.isTrigger = true;
        }

        // 掘削情報を作成
        var miningInfo = MiningInfo.Explosive(explosionPosition, force);

        // 掘削実行
        digger.Dig(damage, miningInfo);

        // 位置を元に戻す
        digger.transform.position = originalPosition;
    }
}
