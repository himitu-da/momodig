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
        
        // Diggerを使用した掘削処理
        PerformMining();
        
        // オブジェクトを破棄
        Destroy(gameObject);
    }
    
    private void PerformMining()
    {
        if (module == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("DynamiteProjectile: No mining module set, using default explosion size");
            }
            // デフォルトサイズでの爆発
            CreateTemporaryDigger(Vector3.zero, new Vector3(3f, 3f, 3f));
            return;
        }
        
        // ModuleからDiggerを作成して掘削実行
        CreateTemporaryDigger(module.DiggingCenter, module.DiggingSize);
    }
    
    private void CreateTemporaryDigger(Vector3 center, Vector3 size)
    {
        // 一時的なDiggerオブジェクトを作成
        GameObject tempDigger = new GameObject("TempDynamiteDigger");
        tempDigger.transform.position = transform.position;
        
        // Diggerコンポーネントを追加
        Digger digger = tempDigger.AddComponent<Digger>();
        
        // BoxColliderを設定（Diggerが自動で追加）
        BoxCollider diggingArea = tempDigger.GetComponent<BoxCollider>();
        if (diggingArea != null)
        {
            diggingArea.center = center;
            diggingArea.size = size;
            diggingArea.isTrigger = true;
        }
        
        // 掘削を実行
        digger.Dig();
        
        // 一定時間後にDiggerオブジェクトを削除（掘削処理完了まで待機）
        Destroy(tempDigger, 2f);
        
        if (enableDebugLogs)
        {
            Debug.Log($"DynamiteProjectile: Mining executed with Digger - Center: {center}, Size: {size}");
        }
    }
    
    private void OnDestroy()
    {
        // 念のためInvokeをキャンセル
        CancelInvoke();
    }
}
