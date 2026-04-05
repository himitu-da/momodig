using UnityEngine;

public class DynamiteToolBehaviour : MiningToolBehaviour
{
    private GameObject owner;
    private PlayerController playerController;

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        owner = user;
        
        // user縺ｯMiningTools縺ｪ縺ｮ縺ｧ縲∬ｦｪ繧ｪ繝悶ず繧ｧ繧ｯ繝茨ｼ・layer・峨°繧臼layerController繧貞叙蠕・
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

        // ToolData 縺ｨ Module 縺悟ｿ・・
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

        bool isTopDown = playerController != null && playerController.currentMoveMode == PlayerController.MoveMode.TopDown;
        Vector3 dir;
        
        if (isTopDown)
        {
            // TopDown: 繝槭え繧ｹ譁ｹ蜷代∈縺ｮ逶ｴ謗･謚募ｰ・ｼ亥ｾ捺擂縺ｨ蜷梧ｧ假ｼ・
            dir = ComputeQuantizedDirection(owner, playerController, isTopDown);
        }
        else
        {
            // SideScroller: 繝槭え繧ｹ菴咲ｽｮ繧堤岼讓吶→縺励◆謾ｾ迚ｩ驕句虚
            dir = ComputeBallisticDirection(owner, playerController);
        }
        
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = isTopDown ? new Vector3(1f, 0f, 0f) : new Vector3(1f, 0f, 0f);
        }

        // 繝励Ξ繧､繝､繝ｼ縺ｮ荳ｭ蠢・ｽ咲ｽｮ縺九ｉ逋ｺ蟆・ｼ・pawnDistance繧剃ｽｿ繧上↑縺・ｼ・
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
            // TopDown: 蠕捺擂騾壹ｊ縺ｮ逶ｴ謗･騾溷ｺｦ險ｭ螳・
            velocity = dir.normalized * force;
        }
        else
        {
            // SideScroller: 謾ｾ迚ｩ驕句虚逕ｨ縺ｮ蛻晞溷ｺｦ繧貞・險育ｮ・
            Vector3 startPos = owner.transform.position;
            Vector3 targetPos = playerController.GetMouseWorldPosition(10f);
            targetPos.z = startPos.z; // Z蠎ｧ讓吶ｒ蝗ｺ螳・
            
            float horizontalDistance = targetPos.x - startPos.x;
            float verticalDistance = targetPos.y - startPos.y;
            
            // 霍晞屬蛻ｶ髯・
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

        // 繝励Ο繧ｸ繧ｧ繧ｯ繝医・ Rigidbody 諡｡蠑ｵ: linearVelocity 繧剃ｽｿ逕ｨ
        rb.linearVelocity = velocity;
    }

    public override void UpdateAim(Vector3 direction, PlayerController.MoveMode moveMode)
    {
        base.UpdateAim(direction, moveMode);
        // Dynamite 縺ｯ迚ｹ縺ｫ Aim 蜿肴丐荳崎ｦ・ｼ亥ｿ・ｦ√↑繧峨％縺薙〒UI遲画峩譁ｰ・・
    }

    private Vector3 ComputeQuantizedDirection(GameObject user, PlayerController pc, bool isTopDown)
    {
        if (pc != null)
        {
            if (isTopDown)
            {
                // TopDown: XZ 蟷ｳ髱｢
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
                // SideScroller: XY 蟷ｳ髱｢
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

        // 繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ
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
    /// SideScroller繝｢繝ｼ繝峨〒繝槭え繧ｹ菴咲ｽｮ繧堤岼讓吶→縺励◆謾ｾ迚ｩ驕句虚縺ｮ譁ｹ蜷代ｒ險育ｮ・
    /// </summary>
    private Vector3 ComputeBallisticDirection(GameObject user, PlayerController pc)
    {
        if (pc == null) return Vector3.right;
        
        // Module縺九ｉ繝代Λ繝｡繝ｼ繧ｿ繧貞叙蠕・
        if (!(ToolData.miningModule is DynamiteMiningModule dynamiteModule))
        {
            return Vector3.right;
        }
        
        float maxDistance = dynamiteModule.MaxThrowDistance.Value;
        
        // 繝励Ξ繧､繝､繝ｼ縺ｮ荳ｭ蠢・ｽ咲ｽｮ縺ｨ繝槭え繧ｹ縺ｮ繝ｯ繝ｼ繝ｫ繝牙ｺｧ讓吶ｒ蜿門ｾ・
        Vector3 startPos = user.transform.position; // 繝励Ξ繧､繝､繝ｼ縺ｮ荳ｭ蠢・ｽ咲ｽｮ
        Vector3 targetPos = pc.GetMouseWorldPosition(10f); // Z=0蟷ｳ髱｢縺ｧ縺ｮ繝ｯ繝ｼ繝ｫ繝牙ｺｧ讓・
        
        // SideScroller縺ｪ縺ｮ縺ｧZ蠎ｧ讓吶ｒ0縺ｫ蝗ｺ螳・
        targetPos.z = startPos.z;
        
        // 豌ｴ蟷ｳ霍晞屬縺ｨ鬮伜ｺｦ蟾ｮ繧定ｨ育ｮ・
        float horizontalDistance = targetPos.x - startPos.x;
        float verticalDistance = targetPos.y - startPos.y;
        
        // 霍晞屬蛻ｶ髯舌ｒ繝√ぉ繝・け・亥ｿ・ｦ√↓蠢懊§縺ｦ・・
        float totalDistance = Mathf.Sqrt(horizontalDistance * horizontalDistance + verticalDistance * verticalDistance);
        if (totalDistance > maxDistance)
        {
            // 譛螟ｧ霍晞屬繧定ｶ・∴繧句ｴ蜷医・譁ｹ蜷代ｒ邯ｭ謖√＠縺ｦ霍晞屬繧貞宛髯・
            Vector3 throwDirection = (targetPos - startPos).normalized;
            targetPos = startPos + throwDirection * maxDistance;
            horizontalDistance = targetPos.x - startPos.x;
            verticalDistance = targetPos.y - startPos.y;
        }
        
        // 謾ｾ迚ｩ驕句虚縺ｮ蛻晞溷ｺｦ繧定ｨ育ｮ暦ｼ郁ｧ貞ｺｦ蛻ｶ髯舌↑縺暦ｼ・
        Vector3 velocity = CalculateBallisticVelocity(horizontalDistance, verticalDistance, dynamiteModule.Gravity);
        
        return velocity.normalized;
    }
    
    /// <summary>
    /// 謾ｾ迚ｩ驕句虚縺ｧ逶ｮ讓咏せ縺ｫ蛻ｰ驕斐☆繧九◆繧√・蛻晞溷ｺｦ繝吶け繝医Ν繧定ｨ育ｮ・
    /// </summary>
    private Vector3 CalculateBallisticVelocity(float horizontalDistance, float verticalDistance, float gravityValue)
    {
        // 豌ｴ蟷ｳ霍晞屬縺・縺ｫ霑代＞蝣ｴ蜷医・蝙ら峩謚募ｰ・
        if (Mathf.Abs(horizontalDistance) < 0.1f)
        {
            float vY = verticalDistance > 0 ? Mathf.Sqrt(2 * gravityValue * Mathf.Abs(verticalDistance)) : -Mathf.Sqrt(2 * gravityValue * Mathf.Abs(verticalDistance));
            return new Vector3(0f, vY, 0f);
        }
        
        // 蝗ｺ螳壽凾髢捺ｳ輔ｒ菴ｿ逕ｨ・医ｈ繧顔｢ｺ螳溘〒逶ｴ諢溽噪・・
        float x = horizontalDistance;
        float y = verticalDistance;
        float g = gravityValue;
        
        // 驕ｩ蛻・↑鬟幄｡梧凾髢薙ｒ謗ｨ螳夲ｼ郁ｷ晞屬縺ｫ蝓ｺ縺･縺擾ｼ・
        float distance = Mathf.Sqrt(x * x + y * y);
        float timeOfFlight = Mathf.Sqrt(2 * distance / g); // 蝓ｺ譛ｬ逧・↑謗ｨ螳・
        
        // 逶ｮ讓吶′驕縺・ｴ蜷医ｄ鬮倥＞蝣ｴ蜷医・譎る俣繧定ｪｿ謨ｴ
        if (distance > 10f || y > 5f)
        {
            timeOfFlight *= 1.5f;
        }
        else if (y < -2f)
        {
            timeOfFlight *= 0.7f; // 荳句髄縺阪・蝣ｴ蜷医・遏ｭ縺・
        }
        
        // 蛻晞溷ｺｦ繧定ｨ育ｮ・
        float vx = x / timeOfFlight;
        float vy = (y + 0.5f * g * timeOfFlight * timeOfFlight) / timeOfFlight;
        
        return new Vector3(vx, vy, 0f);
    }
    
    // 8譁ｹ蜷代↓驥丞ｭ仙喧・・, NE, N, NW, W, SW, S, SE・・
    private Vector2 Quantize8(Vector2 v)
    {
        if (v.sqrMagnitude < 1e-6f) return Vector2.zero;

        float angle = Mathf.Atan2(v.y, v.x); // [-pi, pi]
        float step = Mathf.PI / 4f;          // 45ﾂｰ
        int idx = Mathf.RoundToInt(angle / step);
        idx = (idx % 8 + 8) % 8;             // 0..7 縺ｫ豁｣隕丞喧

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
    /// 辷・匱縺ｫ繧医ｋ謗伜炎繧貞ｮ溯｡鯉ｼ・rojectile 縺九ｉ蜻ｼ縺ｳ蜃ｺ縺暦ｼ・
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<(System.Collections.Generic.HashSet<Block> hitBlocks, int destroyedVoxelCount)> PerformExplosionMining(Vector3 explosionPosition, Vector3 center, Vector3 size, int damage, float force)
    {
        if (digger == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: Digger is not set.");
            return (new System.Collections.Generic.HashSet<Block>(), 0);
        }

        // Digger 縺ｮ菴咲ｽｮ繧堤・逋ｺ菴咲ｽｮ縺ｫ荳譎ら噪縺ｫ險ｭ螳・
        Vector3 originalPosition = digger.transform.position;
        digger.transform.position = explosionPosition;

        // BoxCollider 繧定ｨｭ螳・
        BoxCollider diggingArea = digger.GetComponent<BoxCollider>();
        if (diggingArea != null)
        {
            diggingArea.center = center;
            diggingArea.size = size;
            diggingArea.isTrigger = true;
        }

        // 謗伜炎諠・ｱ繧剃ｽ懈・
        var miningInfo = MiningInfo.Explosive(explosionPosition, force);

        // 謗伜炎螳溯｡・
        var (hitBlocks, destroyedVoxelCount) = await digger.Dig(damage, miningInfo);

        TerrainManager terrainManager = Object.FindFirstObjectByType<TerrainManager>();
        terrainManager?.FluidSimulation?.QueueExplosion(explosionPosition, size, force);

        // 菴咲ｽｮ繧貞・縺ｫ謌ｻ縺・
        digger.transform.position = originalPosition;

        return (hitBlocks, destroyedVoxelCount);
    }
}


