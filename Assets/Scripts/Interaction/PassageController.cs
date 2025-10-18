using UnityEngine;
using UnityEngine.InputSystem;

public class PassageController : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private float transitionSpeed = 0.5f; // 演出の速度
    [SerializeField] private float requiredInputThreshold = 0.5f; // 入力として認識する最小値
    [SerializeField] private string destinationSceneName; // 遷移先のシーン名
    [SerializeField] private ChangeScene changeScene; // シーン遷移を担当するコンポーネント

    [Header("参照")]
    [SerializeField] private MinecartManager minecartManager; // トロッコマネージャーへの参照

    private PlayerController playerController;
    private Transform playerTransform; // Player全体のTransformを保持
    private Collider playerCollider; // PlayerColliderのColliderを保持
    private Vector3 initialPlayerScale;
    private float entryProgress = 0f; // 0.0 (通常) to 1.0 (完全に入った)
    private bool isPlayerInside = false;
    private bool hasTransferredItems = false; // アイテム転送済みフラグ

    private void Awake()
    {
        // MinecartManagerが設定されていない場合は自動的に検索
        if (minecartManager == null)
        {
            minecartManager = FindFirstObjectByType<MinecartManager>();
            if (minecartManager == null)
            {
                Debug.LogWarning("PassageController: MinecartManagerが見つかりません。トロッコのアイテム保存は行われません。");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && playerController == null)
        {
            playerController = other.GetComponentInParent<PlayerController>();
            if (playerController != null)
            {
                isPlayerInside = true;
                Debug.Log("Player is inside passage trigger. Waiting for input.");

                // 参照を取得するだけ
                playerTransform = playerController.transform;
                initialPlayerScale = playerTransform.localScale;
                Transform playerColliderTransform = playerController.transform.Find("PlayerCollider");
                if (playerColliderTransform != null)
                {
                    playerCollider = playerColliderTransform.GetComponent<Collider>();
                    if (playerCollider == null)
                    {
                        Debug.LogWarning("PassageController: 'PlayerCollider' オブジェクトにColliderコンポーネントが見つかりません。");
                    }
                }
                else
                {
                    Debug.LogWarning("PassageController: 'PlayerCollider' という名前の子オブジェクトが見つかりません。");
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && playerController != null)
        {
            // プレイヤーが移動モードに入っていなければ、単純にトリガーから出ただけ
            if (!playerController.IsInPassage)
            {
                Debug.Log("Player exited trigger without starting passage mode.");
                isPlayerInside = false;
                playerController = null; // 参照をクリア
            }
            // 移動モード中の場合は、意図しないリセットを防ぐため何もしない
            // キャンセルは下入力でのみ行われる
        }
    }

    private void Update()
    {
        if (!isPlayerInside || playerController == null)
        {
            return;
        }

        // PlayerControllerから入力を取得
        float verticalInput = playerController.MoveInput.y;

        // まだ移動モードでない場合、上入力で開始する
        if (!playerController.IsInPassage)
        {
            // requiredInputThreshold より大きい入力があった場合のみ
            if (verticalInput > requiredInputThreshold)
            {
                Debug.Log("Passage mode started by input.");
                playerController.IsInPassage = true;
                if (playerCollider != null)
                {
                    playerCollider.enabled = false;
                }
            }
            else
            {
                return; // 移動モードに入っていない、かつ上入力もないので何もしない
            }
        }

        // --- ここからは移動モード中の処理 ---

        // 進行度を更新
        entryProgress += verticalInput * transitionSpeed * Time.deltaTime;
        entryProgress = Mathf.Clamp01(entryProgress);

        // 演出を適用
        UpdateVisuals();

        // キャンセルチェック（下入力で進行度が0になった場合）
        if (verticalInput < 0 && Mathf.Approximately(entryProgress, 0f))
        {
            Debug.Log("Passage entry cancelled.");
            ResetState();
            return; // リセットしたので以降の処理は不要
        }

        // 完了チェック（進行度が1.0に達したら）
        if (entryProgress >= 1.0f && !hasTransferredItems)
        {
            // アイテム転送を実行
            TransferAllItemsToStorage();
            hasTransferredItems = true;

            // シーン遷移を実行
            Debug.Log($"シーン「{destinationSceneName}」への移動を開始します！");
            if (changeScene != null && !string.IsNullOrEmpty(destinationSceneName))
            {
                changeScene.OnClickToChangeScene(destinationSceneName);
            }
            else
            {
                Debug.LogWarning("ChangeSceneコンポーネントまたは遷移先のシーン名が設定されていません。");
            }
            // 完了後はこのコンポーネントの役割は終わるため、無効化
            enabled = false;
        }
    }

    private void UpdateVisuals()
    {
        if (playerTransform != null)
        {
            // 進行度に応じてスケールを線形に変化させる
            playerTransform.localScale = Vector3.Lerp(initialPlayerScale, Vector3.zero, entryProgress);
        }
    }

    private void ResetState()
    {
        if (playerController != null)
        {
            playerController.IsInPassage = false;
        }
        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }
        if (playerTransform != null)
        {
            playerTransform.localScale = initialPlayerScale;
        }

        isPlayerInside = false;
        playerController = null;
        playerTransform = null;
        playerCollider = null;
        entryProgress = 0f;
        hasTransferredItems = false; // 転送フラグもリセット
    }

    /// <summary>
    /// プレイヤーインベントリとトロッコの全アイテムをGameDataPersistenceManagerに転送
    /// </summary>
    private void TransferAllItemsToStorage()
    {
        if (playerController == null)
        {
            Debug.LogWarning("PassageController: PlayerControllerがnullのため、アイテム転送をスキップします。");
            return;
        }

        var persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager == null)
        {
            Debug.LogError("PassageController: GameDataPersistenceManagerが見つかりません。");
            return;
        }

        Debug.Log("PassageController: プレイヤーとトロッコのアイテムをGameDataPersistenceManagerに転送開始");

        // プレイヤーインベントリの全アイテムを転送
        var playerResources = playerController.Inventory.GetAllResources();
        foreach (var resource in playerResources)
        {
            if (resource.Value > 0)
            {
                // GameDataPersistenceManagerに追加
                if (persistenceManager.storedResources.ContainsKey(resource.Key))
                {
                    persistenceManager.storedResources[resource.Key] += resource.Value;
                }
                else
                {
                    persistenceManager.storedResources[resource.Key] = resource.Value;
                }
                Debug.Log($"PassageController: プレイヤーから{resource.Key}を{resource.Value}個転送");
            }
        }

        // プレイヤーインベントリをクリア（各リソースタイプを削除）
        foreach (var resource in playerResources)
        {
            if (resource.Value > 0)
            {
                playerController.Inventory.RemoveResource(resource.Key, resource.Value);
            }
        }

        // トロッコの全アイテムを転送
        if (minecartManager != null && minecartManager.minecarts != null)
        {
            foreach (var minecart in minecartManager.minecarts)
            {
                if (minecart != null && minecart.resources != null)
                {
                    foreach (var resource in minecart.resources)
                    {
                        if (resource.Value > 0)
                        {
                            // GameDataPersistenceManagerに追加
                            if (persistenceManager.storedResources.ContainsKey(resource.Key))
                            {
                                persistenceManager.storedResources[resource.Key] += resource.Value;
                            }
                            else
                            {
                                persistenceManager.storedResources[resource.Key] = resource.Value;
                            }
                            Debug.Log($"PassageController: トロッコから{resource.Key}を{resource.Value}個転送");
                        }
                    }
                    // トロッコをクリア
                    minecart.ClearResources();
                }
            }
        }
        else
        {
            Debug.LogWarning("PassageController: MinecartManagerまたはトロッコリストが見つかりません。");
        }

        Debug.Log("PassageController: アイテム転送完了");
    }
}
