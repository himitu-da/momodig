using UnityEngine;

public class DynamiteToolBehaviour : MiningToolBehaviour
{
    [SerializeField] private float subUseDisplayDuration = 0.3f;

    private GameObject owner;
    private PlayerController playerController;
    private MiningLightManager miningLightManager;

    public override void SetMiningLightManager(MiningLightManager miningLightManager)
    {
        this.miningLightManager = miningLightManager;
    }

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        owner = user;
        
        // userはMiningToolsなので、親オブジェクト！Elayer�E�からPlayerControllerを取征E
        playerController = user != null ? user.GetComponentInParent<PlayerController>() : null;
    }

    public override void Use(Vector3 direction, PlayerController playerController)
    {
        if (!IsEquipped) return;

        if (owner == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: owner is null.");
            return;
        }

        // Sub役割の場合、投擲モーションのあいだだけ手元のダイナマイトを表示する
        BeginUseDisplay();
        CancelInvoke(nameof(EndSubUseDisplay));
        Invoke(nameof(EndSubUseDisplay), subUseDisplayDuration);

        // ToolData と Module が忁E��E
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
        float force = dynamiteModule.ThrowForce.Value;
        float maxDistance = dynamiteModule.MaxThrowDistance.Value;
        float gravityValue = dynamiteModule.Gravity;

        if (projectilePrefab == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: projectile prefab is not assigned.");
            return;
        }

        Vector3 dir = ComputeBallisticDirection(owner, playerController);
        
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = new Vector3(1f, 0f, 0f);
        }

        // プレイヤーの中忁E��置から発封E��EpawnDistanceを使わなぁE��E
        Vector3 spawnPos = owner.transform.position;
        var go = Object.Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        SetProjectileCollisionLayer(go);

        var proj = go.GetComponent<DynamiteProjectile>();
        if (proj == null) proj = go.AddComponent<DynamiteProjectile>();
        proj.SetBehaviour(this);
        proj.ConfigureProjectileLight(miningLightManager, dynamiteModule.ProjectileLightProfile);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;

        Vector3 startPos = owner.transform.position;
        Vector3 targetPos = playerController != null ? playerController.GetMouseWorldPosition(10f) : startPos + dir.normalized * maxDistance;
        targetPos.z = startPos.z;

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

        Vector3 velocity = CalculateBallisticVelocity(horizontalDistance, verticalDistance, gravityValue);

        // プロジェクト�E Rigidbody 拡張: linearVelocity を使用
        rb.linearVelocity = velocity;
    }

    private static void SetProjectileCollisionLayer(GameObject projectile)
    {
        int toolLayer = LayerMask.NameToLayer("Tool");
        if (projectile == null || toolLayer < 0)
        {
            return;
        }

        projectile.layer = toolLayer;
        for (int i = 0; i < projectile.transform.childCount; i++)
        {
            projectile.transform.GetChild(i).gameObject.layer = toolLayer;
        }
    }

    protected override void RenderForDirection(Vector3 direction)
    {
        // Dynamite は手元の見た目を方向に合わせる必要がない（投擲時に projectile が独立する）。
        // Main 役割で使われた場合に備え、必要になったらここで向きを反映する。
    }

    private void EndSubUseDisplay()
    {
        EndUseDisplay();
    }

    /// <summary>
    /// プレイ面でマウス位置を目標とした放物運動の方向を計箁E
    /// </summary>
    private Vector3 ComputeBallisticDirection(GameObject user, PlayerController pc)
    {
        if (pc == null) return Vector3.right;
        
        // Moduleからパラメータを取征E
        if (!(ToolData.miningModule is DynamiteMiningModule dynamiteModule))
        {
            return Vector3.right;
        }
        
        float maxDistance = dynamiteModule.MaxThrowDistance.Value;
        
        // プレイヤーの中忁E��置とマウスのワールド座標を取征E
        Vector3 startPos = user.transform.position; // プレイヤーの中忁E��置
        Vector3 targetPos = pc.GetMouseWorldPosition(10f); // Z=0平面でのワールド座樁E
        
        // プレイ面なのでZ座標を0に固宁E
        targetPos.z = startPos.z;
        
        // 水平距離と高度差を計箁E
        float horizontalDistance = targetPos.x - startPos.x;
        float verticalDistance = targetPos.y - startPos.y;
        
        // 距離制限をチェチE���E�忁E��に応じて�E�E
        float totalDistance = Mathf.Sqrt(horizontalDistance * horizontalDistance + verticalDistance * verticalDistance);
        if (totalDistance > maxDistance)
        {
            // 最大距離を趁E��る場合�E方向を維持して距離を制陁E
            Vector3 throwDirection = (targetPos - startPos).normalized;
            targetPos = startPos + throwDirection * maxDistance;
            horizontalDistance = targetPos.x - startPos.x;
            verticalDistance = targetPos.y - startPos.y;
        }
        
        // 放物運動の初速度を計算（角度制限なし！E
        Vector3 velocity = CalculateBallisticVelocity(horizontalDistance, verticalDistance, dynamiteModule.Gravity);
        
        return velocity.normalized;
    }
    
    /// <summary>
    /// 放物運動で目標点に到達するため�E初速度ベクトルを計箁E
    /// </summary>
    private Vector3 CalculateBallisticVelocity(float horizontalDistance, float verticalDistance, float gravityValue)
    {
        // 水平距離ぁEに近い場合�E垂直投封E
        if (Mathf.Abs(horizontalDistance) < 0.1f)
        {
            float vY = verticalDistance > 0 ? Mathf.Sqrt(2 * gravityValue * Mathf.Abs(verticalDistance)) : -Mathf.Sqrt(2 * gravityValue * Mathf.Abs(verticalDistance));
            return new Vector3(0f, vY, 0f);
        }
        
        // 固定時間法を使用�E�より確実で直感的�E�E
        float x = horizontalDistance;
        float y = verticalDistance;
        float g = gravityValue;
        
        // 適刁E��飛行時間を推定（距離に基づく！E
        float distance = Mathf.Sqrt(x * x + y * y);
        float timeOfFlight = Mathf.Sqrt(2 * distance / g); // 基本皁E��推宁E
        
        // 目標が遠ぁE��合や高い場合�E時間を調整
        if (distance > 10f || y > 5f)
        {
            timeOfFlight *= 1.5f;
        }
        else if (y < -2f)
        {
            timeOfFlight *= 0.7f; // 下向き�E場合�E短ぁE
        }
        
        // 初速度を計箁E
        float vx = x / timeOfFlight;
        float vy = (y + 0.5f * g * timeOfFlight * timeOfFlight) / timeOfFlight;
        
        return new Vector3(vx, vy, 0f);
    }
    

    /// <summary>
    /// 爁E��による掘削を実行！Erojectile から呼び出し！E
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<(System.Collections.Generic.HashSet<Block> hitBlocks, int destroyedVoxelCount)> PerformExplosionMining(Vector3 explosionPosition, Vector3 center, Vector3 size, int damage, float force)
    {
        if (digger == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: Digger is not set.");
            return (new System.Collections.Generic.HashSet<Block>(), 0);
        }

        // Digger の位置を�E発位置に一時的に設宁E
        Vector3 originalPosition = digger.transform.position;
        digger.transform.position = explosionPosition;

        // BoxCollider を設宁E
        BoxCollider diggingArea = digger.GetComponent<BoxCollider>();
        if (diggingArea != null)
        {
            diggingArea.center = center;
            diggingArea.size = size;
            diggingArea.isTrigger = true;
        }

        // 掘削惁E��を作�E
        var miningInfo = MiningInfo.Explosive(explosionPosition, force);

        // 掘削実衁E
        var (hitBlocks, destroyedVoxelCount) = await digger.Dig(damage, miningInfo);

        TerrainManager terrainManager = Object.FindFirstObjectByType<TerrainManager>();
        terrainManager?.FluidManager?.QueueExplosion(explosionPosition, size, force);

        // 位置を�Eに戻ぁE
        digger.transform.position = originalPosition;

        return (hitBlocks, destroyedVoxelCount);
    }
}



