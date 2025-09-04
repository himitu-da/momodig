using UnityEngine;

public class DynamiteToolBehaviour : MiningToolBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject dynamiteProjectilePrefab; // フォールバック用
    [SerializeField] private float throwForce = 8f;               // フォールバック用
    [SerializeField] private float spawnDistance = 0.8f;

    private GameObject owner;
    private PlayerController playerController;

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        owner = user;
        
        // userはMiningToolsなので、親オブジェクト（Player）からPlayerControllerを取得
        playerController = user != null ? user.GetComponentInParent<PlayerController>() : null;
    }

    public override void Use()
    {
        if (!IsEquipped) return;
        if (owner == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: owner is null.");
            return;
        }

        // ToolData(Dynamite) があれば優先的に使う
        GameObject projectilePrefab = dynamiteProjectilePrefab;
        float force = throwForce;
        if (ToolData is Dynamite dyn)
        {
            if (dyn.ProjectilePrefab != null) projectilePrefab = dyn.ProjectilePrefab;
            if (dyn.ThrowForce > 0f) force = dyn.ThrowForce;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: projectile prefab is not assigned.");
            return;
        }

        bool isTopDown = playerController != null && playerController.currentMoveMode == PlayerController.MoveMode.TopDown;
        Vector3 dir = ComputeQuantizedDirection(owner, playerController, isTopDown);
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = isTopDown ? new Vector3(1f, 0f, 0f) : new Vector3(1f, 0f, 0f);
        }

        Vector3 spawnPos = owner.transform.position + dir.normalized * spawnDistance;
        var go = Object.Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        var proj = go.GetComponent<DynamiteProjectile>();
        if (proj == null) proj = go.AddComponent<DynamiteProjectile>();
        if (ToolData != null && ToolData.miningModule != null)
        {
            proj.SetModule(ToolData.miningModule);
        }

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = !isTopDown;

    Vector3 v = dir.normalized * force;
    // プロジェクトの Rigidbody 拡張: linearVelocity を使用
    rb.linearVelocity = v;
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
}