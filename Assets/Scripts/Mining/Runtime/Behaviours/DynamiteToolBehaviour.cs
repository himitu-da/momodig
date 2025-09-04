using UnityEngine;

public class DynamiteToolBehaviour : MiningToolBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject dynamiteProjectilePrefab;
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float spawnDistance = 0.8f;

    private GameObject owner;
    private PlayerController playerController;

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        owner = user;
        playerController = user != null ? user.GetComponent<PlayerController>() : null;
    }

    public override void Use()
    {
        if (!IsEquipped) return;
        if (owner == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: owner is null.");
            return;
        }
        if (dynamiteProjectilePrefab == null)
        {
            Debug.LogWarning("DynamiteToolBehaviour: projectile prefab is not assigned.");
            return;
        }

        bool isTopDown = playerController != null && playerController.currentMoveMode == PlayerController.MoveMode.TopDown;
        Vector3 dir = ComputeQuantizedDirection(owner, playerController, isTopDown);
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = isTopDown ? new Vector3(1, 0, 0) : new Vector3(1, 0, 0);
        }

        Vector3 spawnPos = owner.transform.position + dir.normalized * spawnDistance;
        var go = Object.Instantiate(dynamiteProjectilePrefab, spawnPos, Quaternion.identity);

        var proj = go.GetComponent<DynamiteProjectile>();
        if (proj == null) proj = go.AddComponent<DynamiteProjectile>();
        if (ToolData != null && ToolData.miningModule != null)
        {
            proj.SetModule(ToolData.miningModule);
        }

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = !isTopDown;
        // 速度設定（プロジェクトに合わせたプロパティ名に合わせる）
        try
        {
            // いくつかの環境では linearVelocity が使用されている想定
            rb.GetType().GetProperty("linearVelocity")?.SetValue(rb, dir.normalized * throwForce);
        }
        catch { }
        rb.linearVelocity = dir.normalized * throwForce;
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
        Vector3 fw = user.transform.forward;
        if (isTopDown)
        {
            Vector2 q = Quantize8(new Vector2(fw.x, fw.z));
            return new Vector3(q.x, 0f, q.y);
        }
        else
        {
            Vector2 q = Quantize8(new Vector2(Mathf.Approximately(Mathf.Sign(fw.x), 0f) ? 1f : Mathf.Sign(fw.x), 0f));
            return new Vector3(q.x, q.y, 0f);
        }
    }

    // 8方向に量子化（E, NE, N, NW, W, SW, S, SE）
    private Vector2 Quantize8(Vector2 v)
    {
        if (v.sqrMagnitude < 0.0001f) return Vector2.zero;
        v.Normalize();
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // -180..180
        if (angle < 0f) angle += 360f; // 0..360
        int sector = Mathf.RoundToInt(angle / 45f) % 8; // 0..7
        switch (sector)
        {
            case 0: return new Vector2(1f, 0f);           // E
            case 1: return new Vector2(0.7071f, 0.7071f); // NE
            case 2: return new Vector2(0f, 1f);           // N
            case 3: return new Vector2(-0.7071f, 0.7071f);// NW
            case 4: return new Vector2(-1f, 0f);          // W
            case 5: return new Vector2(-0.7071f, -0.7071f);// SW
            case 6: return new Vector2(0f, -1f);          // S
            case 7: return new Vector2(0.7071f, -0.7071f);// SE
            default: return new Vector2(1f, 0f);
        }
    }
}