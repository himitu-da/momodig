using UnityEngine;
using System.Collections.Generic;

public class DynamiteProjectile : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionDelay = 3f; // 3秒で爆発
    [SerializeField] private bool enableDebugLogs = true;
    
    private DynamiteToolBehaviour behaviour; // behaviour からデータを取得
    private MiningLightSource lightSource;
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

    public void ConfigureProjectileLight(MiningLightManager lightManager, MiningLightProfile lightProfile)
    {
        if (lightProfile == null)
        {
            return;
        }

        if (lightManager == null)
        {
            Debug.LogError("DynamiteProjectile: MiningLightManager is not configured for projectile light.", this);
            return;
        }

        lightSource = GetComponent<MiningLightSource>();
        if (lightSource == null)
        {
            lightSource = gameObject.AddComponent<MiningLightSource>();
        }

        lightSource.Configure(lightManager, lightProfile, transform);
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
    
    private async void PerformMining()
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
            var (hitBlocks, destroyedVoxelCount) = await behaviour.PerformExplosionMining(transform.position, Vector3.zero, new Vector3(3f, 3f, 3f), 999, 5f);
            PlayExplosionSound(hitBlocks, destroyedVoxelCount);
            return;
        }

        float explosionForce = 5f; // デフォルト値
        if (behaviour.ToolData.miningModule is DynamiteMiningModule dynamiteModule)
        {
            explosionForce = dynamiteModule.ExplosionForce.Value;
        }
        
        // Behaviour の Digger を使用して掘削実行
        int explosionDamage = module.DamagePerHit.IntValue;
        var (hitBlocksWithSound, destroyedVoxelCountWithSound) = await behaviour.PerformExplosionMining(transform.position, module.DiggingCenter, module.DiggingSize.Value, explosionDamage, explosionForce);
        PlayExplosionSound(hitBlocksWithSound, destroyedVoxelCountWithSound);
    }

    private void PlayExplosionSound(HashSet<Block> hitBlocks, int destroyedVoxelCount)
    {
        // 掘削音を再生
        AudioClip diggingSound = behaviour.ToolData.DefaultMiningSound; // ダイナマイトは素材別の音は不要と想定
        if (diggingSound != null)
        {
            AudioManager.Instance.PlayDiggingSE(diggingSound, hitBlocks.Count, behaviour.ToolData.Volume);
        }

        // 破壊音は再生しない（BlockDiggingSystemが担当するため）
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
        Vector3 size = module.DiggingSize.Value;
        Gizmos.DrawWireCube(center, size);
    }
}
