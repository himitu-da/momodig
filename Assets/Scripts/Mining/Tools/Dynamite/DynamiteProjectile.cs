using UnityEngine;
using System.Collections.Generic;

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
            var hitBlocks = behaviour.PerformExplosionMining(transform.position, Vector3.zero, new Vector3(3f, 3f, 3f), 999, 5f);
            PlayExplosionSound(hitBlocks);
            return;
        }

        float explosionForce = 5f; // デフォルト値
        if (behaviour.ToolData.miningModule is DynamiteMiningModule dynamiteModule)
        {
            explosionForce = dynamiteModule.ExplosionForce;
        }
        
        // Behaviour の Digger を使用して掘削実行
        int explosionDamage = module.DamagePerHit;
        var hitBlocksWithSound = behaviour.PerformExplosionMining(transform.position, module.DiggingCenter, module.DiggingSize, explosionDamage, explosionForce);
        PlayExplosionSound(hitBlocksWithSound);
    }

    private void PlayExplosionSound(HashSet<Block> hitBlocks)
    {
        AudioClip soundToPlay = null;

        // ヒットしたブロックがあれば、その素材に応じた音を取得
        if (hitBlocks.Count > 0)
        {
            // 最初のブロックを代表として音を決定
            var firstBlock = new List<Block>(hitBlocks)[0];
            if (firstBlock != null && firstBlock.BlockData != null)
            {
                var materialType = firstBlock.BlockData.materialType;
                soundToPlay = behaviour.ToolData.GetMiningSound(materialType);
            }
        }
        
        // 再生する音がまだ決まっていない場合（空振りなど）、デフォルトの音を使用
        if (soundToPlay == null)
        {
            soundToPlay = behaviour.ToolData.DefaultMiningSound;
        }

        // 最終的に決まった音を再生
        if (soundToPlay != null)
        {
            AudioManager.Instance.PlayDiggingSE(soundToPlay, hitBlocks.Count, behaviour.ToolData.Volume);
        }
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
