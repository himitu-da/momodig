using UnityEngine;

public class DynamiteProjectile : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionDelay = 3f; // 3秒で爆発
    [SerializeField] private bool enableDebugLogs = true;
    
    private DynamiteToolBehaviour behaviour; // behaviour からデータを取得
    private bool hasExploded = false;
    
    private void Start()
    {
        // 3秒後に爆発
        Invoke(nameof(Explode), explosionDelay);
        
        if (enableDebugLogs)
        {
            Debug.Log($"DynamiteProjectile: Started timer explosion in {explosionDelay} seconds at {transform.position}");
        }
    }
    
    public void SetBehaviour(DynamiteToolBehaviour b)
    {
        behaviour = b;
        if (enableDebugLogs && b != null)
        {
            Debug.Log($"DynamiteProjectile: Behaviour set - ToolData: {b.ToolData}");
        }
    }
    
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"DynamiteProjectile: Exploding at {transform.position}");
        }
        
        // Diggerを使用した掘削処理
        PerformMining();
        
        // オブジェクトを破棄
        Destroy(gameObject);
    }
    
    private void PerformMining()
    {
        if (behaviour == null || behaviour.ToolData == null)
        {
            if (enableDebugLogs) Debug.LogWarning("DynamiteProjectile: Behaviour or ToolData is null.");
            return;
        }

        MiningModule module = behaviour.ToolData.miningModule;
        if (module == null)
        {
            if (enableDebugLogs) Debug.LogWarning("DynamiteProjectile: No mining module from behaviour, using default explosion size");
            // デフォルト値での爆発
            behaviour.PerformExplosionMining(transform.position, Vector3.zero, new Vector3(3f, 3f, 3f), 999, 5f);
            return;
        }

        float explosionForce = 5f; // デフォルト値
        if (behaviour.ToolData.miningModule is DynamiteMiningModule dynamiteModule)
        {
            explosionForce = dynamiteModule.ExplosionForce;
        }
        
        // Behaviour の Digger を使用して掘削実行
        int explosionDamage = module.DamagePerHit;
        behaviour.PerformExplosionMining(transform.position, module.DiggingCenter, module.DiggingSize, explosionDamage, explosionForce);
    }
    
    private void OnDestroy()
    {
        // 念のためInvokeをキャンセル
        CancelInvoke();
    }

    private void OnDrawGizmos()
    {
        if (behaviour == null || behaviour.ToolData == null || behaviour.ToolData.miningModule == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        MiningModule module = behaviour.ToolData.miningModule;
        Vector3 center = transform.position + module.DiggingCenter;
        Vector3 size = module.DiggingSize;
        Gizmos.DrawWireCube(center, size);
    }
}
