using UnityEngine;

public class DynamiteProjectile : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionDelay = 3f; // 3秒で爆発
    [SerializeField] private bool enableDebugLogs = true;
    
    private MiningModule module; // 爆風サイズなど
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
    
    public void SetModule(MiningModule m)
    {
        module = m;
        if (enableDebugLogs && m != null)
        {
            Debug.Log($"DynamiteProjectile: Module set - Center: {m.DiggingCenter}, Size: {m.DiggingSize}");
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
        
        // Diggerを使用した掘削処理（Blockを直接叩かない）
        PerformMining();
        
        // オブジェクトを破棄
        Destroy(gameObject);
    }
    
    private void PerformMining()
    {
        Vector3 center = Vector3.zero;
        Vector3 size = new Vector3(3f, 3f, 3f);
        int explosionDamage = 999;
        
        if (module != null)
        {
            center = module.DiggingCenter;
            size = module.DiggingSize;
            explosionDamage = module.DamagePerHit;
        }
        else
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("DynamiteProjectile: No mining module set, using default explosion size");
            }
        }
        
        CreateAndUseTemporaryDigger(center, size, explosionDamage);
    }
    
    private void CreateAndUseTemporaryDigger(Vector3 localCenter, Vector3 size, int damage)
    {
        // 一時的なDiggerオブジェクトを作成し、Digger経由で掘削する
        GameObject tempDigger = new GameObject("TempDynamiteDigger");
        
        // 配置: 爆心と同じワールド位置に置き、回転も合わせる
        tempDigger.transform.position = transform.position;
        tempDigger.transform.rotation = transform.rotation;
        
        // Diggerコンポーネントを追加（AwakeでBoxColliderが作られる）
        Digger digger = tempDigger.AddComponent<Digger>();
        
        // Awakeが即時実行されるためBoxColliderは存在するはず。取得してパラメータを設定する。
        BoxCollider diggingArea = tempDigger.GetComponent<BoxCollider>();
        if (diggingArea == null)
        {
            // 念のため追加（通常はDigger.Awakeで追加される）
            diggingArea = tempDigger.AddComponent<BoxCollider>();
            diggingArea.isTrigger = true;
        }

        // BoxColliderはローカル中心とサイズで設定する（Digger.Digは transform を考慮してOverlapBoxを行う）
        diggingArea.center = localCenter;
        diggingArea.size = size;
        diggingArea.isTrigger = true;

        // ここでDiggerに直接掘削を行わせる（Digger.Dig は diggingArea のワールド変換を使う）
        digger.Dig(damage);

        // 掘削処理がコルーチンで回る可能性があるため少し遅らせて削除
        Object.Destroy(tempDigger, 2f);

        if (enableDebugLogs)
        {
            Debug.Log($"DynamiteProjectile: Spawned TempDigger at {tempDigger.transform.position} with center {localCenter}, size {size}, damage {damage}");
        }
    }
    
    private void OnDestroy()
    {
        // 念のためInvokeをキャンセル
        CancelInvoke();
    }
}