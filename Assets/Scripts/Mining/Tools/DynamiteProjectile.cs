using UnityEngine;

public class DynamiteProjectile : MonoBehaviour
{
    private MiningModule module; // 将来使用（爆風サイズなど）

    public void SetModule(MiningModule m)
    {
        module = m;
    }

    // 将来: 衝突/信管/爆発ロジックをここに実装
}
