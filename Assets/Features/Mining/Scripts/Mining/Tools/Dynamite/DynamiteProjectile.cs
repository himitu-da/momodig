using UnityEngine;
using System.Collections.Generic;

public class DynamiteProjectile : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionDelay = 3f; // 3秒で爆発
    [SerializeField] private bool enableDebugLogs = true;
    
    private DynamiteToolBehaviour behaviour; // behaviour からデータを取得
    private MiningLightSource lightSource;
    private MiningLightManager miningLightManager;
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
        if (lightManager == null)
        {
            Debug.LogError("DynamiteProjectile: MiningLightManager is not configured for projectile light.", this);
            return;
        }

        miningLightManager = lightManager;
        if (lightProfile == null)
        {
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
        Vector3 explosionPosition = transform.position;
        
        if (enableDebugLogs)
        {
            Debug.Log($"DynamiteProjectile: Exploding at {explosionPosition}");
        }

        // Diggerを使用した掘削処理
        PerformMining(explosionPosition);
        
        // オブジェクトを破棄
        Destroy(gameObject);
    }
    
    private async void PerformMining(Vector3 explosionPosition)
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
            var (hitBlocks, destroyedVoxelCount) = await behaviour.PerformExplosionMining(explosionPosition, Vector3.zero, new Vector3(3f, 3f, 3f), 999, 5f);
            PlayExplosionSound(hitBlocks, destroyedVoxelCount);
            return;
        }

        float explosionForce = 5f; // デフォルト値
        DynamiteMiningModule dynamiteModule = behaviour.ToolData.miningModule as DynamiteMiningModule;
        if (dynamiteModule != null)
        {
            explosionForce = dynamiteModule.ExplosionForce.Value;
        }
        
        // Behaviour の Digger を使用して掘削実行
        int explosionDamage = module.DamagePerHit.IntValue;
        var (hitBlocksWithSound, destroyedVoxelCountWithSound) = await behaviour.PerformExplosionMining(explosionPosition, module.DiggingCenter, module.DiggingSize.Value, explosionDamage, explosionForce);
        PlayExplosionSound(hitBlocksWithSound, destroyedVoxelCountWithSound);
        SpawnExplosionAfterglowLight(explosionPosition, dynamiteModule);
    }

    private void SpawnExplosionAfterglowLight(Vector3 explosionPosition, DynamiteMiningModule dynamiteModule)
    {
        if (dynamiteModule == null || !dynamiteModule.EnableExplosionAfterglowLight)
        {
            return;
        }

        SpawnExplosionBurstLight(
            explosionPosition,
            dynamiteModule.ExplosionAfterglowLightProfile,
            dynamiteModule.ExplosionAfterglowLifetimeSeconds,
            dynamiteModule.ExplosionAfterglowBrightnessCurve,
            dynamiteModule.ExplosionAfterglowUpdateIntervalSeconds,
            "afterglow");
    }

    private void SpawnExplosionBurstLight(
        Vector3 explosionPosition,
        MiningLightProfile profile,
        float lifetimeSeconds,
        AnimationCurve brightnessCurve,
        float updateIntervalSeconds,
        string lightKind)
    {
        if (miningLightManager == null)
        {
            Debug.LogError($"DynamiteProjectile: cannot spawn explosion {lightKind} light because MiningLightManager is not configured.", this);
            return;
        }

        if (!miningLightManager.SpawnTemporaryBurstLight(
                explosionPosition,
                profile,
                lifetimeSeconds,
                brightnessCurve,
                updateIntervalSeconds))
        {
            Debug.LogWarning($"DynamiteProjectile: explosion {lightKind} light was not spawned.", this);
        }
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
