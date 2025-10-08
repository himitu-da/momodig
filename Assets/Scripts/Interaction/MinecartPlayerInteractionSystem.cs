using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;

/// <summary>
/// プレイヤーとトロッコの相互作用を管理するシステム
/// トロッコの検知、アイテム転送、アニメーション処理を担当
/// </summary>
public class MinecartPlayerInteractionSystem : MonoBehaviour
{
    [Header("トロッコ相互作用設定")]
    [SerializeField] private float minecartDetectionRange = 3f; // トロッコ検出範囲
    [SerializeField] private float itemTransferSpeed = 50f; // アイテム転送速度（個/秒）
    
    [Header("アニメーション設定")]
    [SerializeField] private float itemMoveSpeed = 5f; // アイテムの移動速度
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 移動カーブ
    
    [Header("参照")]
    [SerializeField] private MinecartManager minecartManager; // MinecartManagerへの参照
    [SerializeField] private PlayerController playerController; // PlayerControllerへの参照
    
    // プライベート変数
    private bool isTransferringItems = false; // アイテム転送中フラグ
    private float minecartOffset = 2f; // プレイヤーとトロッコの距離
    
    #region Unity ライフサイクル
    
    void Awake()
    {
        ValidateReferences();
    }

    void Update()
    {
        if (minecartManager != null && playerController != null)
        {
            minecartManager.UpdateMinecartPositions(playerController.transform.position, playerController.lastMoveDirection, minecartOffset);
            CheckMinecartProximity();
        }
    }
    
    #endregion
    
    #region 公開メソッド
    
    /// <summary>
    /// 外部システムから呼び出されるトロッコ近接チェック
    /// </summary>
    public void CheckMinecartProximity()
    {
        if (isTransferringItems || playerController == null || playerController.Inventory == null || playerController.Inventory.IsEmpty()) 
            return;
        
        GameObject nearestMinecart = GetNearestMinecart();
        if (nearestMinecart != null)
        {
            float distance = Vector3.Distance(playerController.transform.position, nearestMinecart.transform.position);
            
            if (distance <= minecartDetectionRange)
            {
                LogMinecartDetection(distance);
                TransferItemsToMinecart(nearestMinecart).Forget();
            }
        }
    }
    
    /// <summary>
    /// トロッコ検出範囲を取得
    /// </summary>
    public float GetDetectionRange() => minecartDetectionRange;
    
    /// <summary>
    /// トロッコ検出範囲を設定
    /// </summary>
    public void SetDetectionRange(float range) => minecartDetectionRange = range;
    
    /// <summary>
    /// 転送中かどうかを取得
    /// </summary>
    public bool IsTransferringItems() => isTransferringItems;
    
    #endregion
    
    #region プライベートメソッド
    
    /// <summary>
    /// 参照の妥当性をチェック
    /// </summary>
    private void ValidateReferences()
    {
        if (minecartManager != null)
        {
            Debug.Log("MinecartInteractionSystem: MinecartManagerが参照されています");
        }
        else
        {
            Debug.LogWarning("MinecartInteractionSystem: MinecartManagerがアタッチされていません");
        }
        
        if (playerController == null)
        {
            Debug.LogError("MinecartInteractionSystem: PlayerControllerが参照されていません");
        }
    }
    
    /// <summary>
    /// 最も近いトロッコを取得
    /// </summary>
    private GameObject GetNearestMinecart()
    {
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;

        if (minecartManager == null) return null;

        foreach (Minecart cart in minecartManager.minecarts)
        {
            if (cart.gameObject != null)
            {
                float distance = Vector3.Distance(playerController.transform.position, cart.gameObject.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = cart.gameObject;
                }
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// トロッコ検出時のログ出力
    /// </summary>
    private void LogMinecartDetection(float distance)
    {
        // Debug.Log($"MinecartInteractionSystem: 最寄りトロッコとの距離: {distance:F2}m (検出範囲: {minecartDetectionRange}m)");
        // Debug.Log("MinecartInteractionSystem: トロッコが検出範囲内に入りました！");
        
        if (minecartManager != null)
        {
            // Debug.Log($"MinecartInteractionSystem: MinecartManager状態 - digable: {minecartManager.digable}, トロッコ数: {minecartManager.minecarts.Count}");
            if (minecartManager.minecarts.Count > 0)
            {
                var cart = minecartManager.minecarts[0];
                // Debug.Log($"MinecartInteractionSystem: トロッコ0の資源状況 - Stone:{cart.resources[ResourceType.Stone]}, Iron:{cart.resources[ResourceType.Iron]}, Gold:{cart.resources[ResourceType.Gold]}, Diamond:{cart.resources[ResourceType.Diamond]}");
            }
        }
        else
        {
            Debug.LogError("MinecartInteractionSystem: MinecartManagerがnullです！");
        }
        
        // Debug.Log("MinecartInteractionSystem: アイテム転送を開始しようとしています...");
    }
    
    #endregion
    
    #region アイテム転送システム
    
    /// <summary>
    /// プレイヤーからトロッコにアイテムを転送
    /// </summary>
    private async UniTask TransferItemsToMinecart(GameObject targetMinecart)
    {
        isTransferringItems = true;
        // Debug.Log("MinecartInteractionSystem: アイテム転送開始");
        
        // Debug.Log($"MinecartInteractionSystem: プレイヤーインベントリ総数: {playerController.Inventory.GetTotalItemCount()}");
        
        while (!playerController.Inventory.IsEmpty())
        {
            // トロッコが離れた場合は中断
            float currentDistance = Vector3.Distance(playerController.transform.position, targetMinecart.transform.position);
            if (currentDistance > minecartDetectionRange)
            {
                Debug.Log($"MinecartInteractionSystem: トロッコが離れたため転送中断 (距離: {currentDistance:F2}m)");
                break;
            }
            
            // MinecartManagerの利用可能性チェック
            if (!IsMinecartManagerAvailable())
            {
                break;
            }
            
            // 転送するリソースタイプを選択
            ResourceType transferType = SelectTransferResourceType();
            if (transferType == ResourceType.Stone && playerController.Inventory.GetResourceCount(ResourceType.Stone) == 0)
            {
                Debug.Log("MinecartInteractionSystem: 転送可能なリソースが見つかりません");
                break;
            }
            
            // トロッコの容量チェックと転送処理
            if (ProcessResourceTransfer(transferType, targetMinecart))
            {
                // 転送速度に応じて待機
                await UniTask.Delay(TimeSpan.FromSeconds(1f / itemTransferSpeed));
            }
            else
            {
                // 全てのリソースタイプが満載の場合は終了
                break;
            }
        }
        
        isTransferringItems = false;
        // Debug.Log("MinecartInteractionSystem: アイテム転送終了");
    }
    
    /// <summary>
    /// MinecartManagerの利用可能性をチェック
    /// </summary>
    private bool IsMinecartManagerAvailable()
    {
        if (minecartManager == null)
        {
            Debug.LogError("MinecartInteractionSystem: MinecartManagerがnullです");
            return false;
        }
        
        if (!minecartManager.digable)
        {
            Debug.Log($"MinecartInteractionSystem: トロッコが利用できません (digable: {minecartManager.digable})");
            return false;
        }
        
        if (minecartManager.minecarts.Count == 0)
        {
            Debug.LogError("MinecartInteractionSystem: 利用可能なトロッコがありません");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 転送するリソースタイプを選択
    /// </summary>
    private ResourceType SelectTransferResourceType()
    {
        var allResources = playerController.Inventory.GetAllResources();
        foreach (var kvp in allResources)
        {
            if (kvp.Value > 0)
            {
                // Debug.Log($"MinecartInteractionSystem: 転送予定リソース: {kvp.Key} (持ち数: {kvp.Value})");
                return kvp.Key;
            }
        }
        return ResourceType.Stone; // デフォルト値
    }
    
    /// <summary>
    /// リソース転送を処理
    /// </summary>
    private bool ProcessResourceTransfer(ResourceType transferType, GameObject targetMinecart)
    {
        var targetCart = minecartManager.minecarts[0];
        int currentAmount = targetCart.resources[transferType];
        int capacity = minecartManager.CartCapacity;
        
        // Debug.Log($"MinecartInteractionSystem: トロッコ容量チェック - {transferType}: {currentAmount}/{capacity}");
        
        // 容量が満杯の場合、他のリソースタイプをチェック
        if (currentAmount >= capacity)
        {
            transferType = FindAlternativeResourceType(transferType, targetCart, capacity);
            if (transferType == ResourceType.Stone && playerController.Inventory.GetResourceCount(ResourceType.Stone) == 0)
            {
                Debug.Log("MinecartInteractionSystem: 全てのリソースタイプで満載のため転送終了");
                return false;
            }
        }
        
        // リソース転送実行
        return ExecuteResourceTransfer(transferType, targetMinecart);
    }
    
    /// <summary>
    /// 代替のリソースタイプを見つける
    /// </summary>
    private ResourceType FindAlternativeResourceType(ResourceType currentType, Minecart targetCart, int capacity)
    {
        Debug.Log($"MinecartInteractionSystem: トロッコの{currentType}が満載です ({targetCart.resources[currentType]}/{capacity})");
        
        foreach (ResourceType otherType in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (otherType != currentType && 
                playerController.Inventory.GetResourceCount(otherType) > 0 && 
                targetCart.resources[otherType] < capacity)
            {
                Debug.Log($"MinecartInteractionSystem: 別のリソースタイプに切り替え: {otherType}");
                return otherType;
            }
        }
        
        return ResourceType.Stone; // 見つからない場合のデフォルト
    }
    
    /// <summary>
    /// リソース転送を実行
    /// </summary>
    private bool ExecuteResourceTransfer(ResourceType transferType, GameObject targetMinecart)
    {
        int removedAmount = playerController.Inventory.RemoveResource(transferType, 1);
        if (removedAmount > 0)
        {
            // Debug.Log($"MinecartInteractionSystem: プレイヤーから{transferType}を{removedAmount}個削除");
            
            // アニメーション付きでトロッコに移動
            AnimateItemTransfer(playerController.transform.position, targetMinecart.transform.position, transferType).Forget();
            
            // トロッコに追加
            minecartManager.updatevalue(0, transferType, removedAmount);
            // Debug.Log($"MinecartInteractionSystem: {transferType}をトロッコに{removedAmount}個転送完了");
            
            // 満載チェックはMinecartManagerのupdatevalue内で行われるため、ここでの追加処理は不要
            
            return true;
        }
        else
        {
            Debug.LogWarning($"MinecartInteractionSystem: プレイヤーから{transferType}の削除に失敗");
            return false;
        }
    }
    
    #endregion
    
    #region アニメーションシステム
    
    /// <summary>
    /// アイテム転送のアニメーション
    /// </summary>
    private async UniTask AnimateItemTransfer(Vector3 startPos, Vector3 endPos, ResourceType resourceType)
    {
        GameObject animItem = CreateAnimationItem(startPos, resourceType);
        
        Vector3 targetPos = CalculateTargetPosition(endPos);
        await MoveItemAsync(animItem, startPos, targetPos);
        
        // 完了後削除
        if (animItem != null)
        {
            Destroy(animItem);
        }
    }
    
    /// <summary>
    /// アニメーション用のアイテムを作成
    /// </summary>
    private GameObject CreateAnimationItem(Vector3 position, ResourceType resourceType)
    {
        GameObject animItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        animItem.transform.SetParent(transform); // 親オブジェクトを設定
        animItem.transform.position = position;
        animItem.transform.localScale = Vector3.one * 0.3f;
        
        // 当たり判定無効化
        var collider = animItem.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        var renderer = animItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Custom Unlitマテリアルを作成
            Material mat = new Material(Shader.Find("Custom/Default"));
            mat.renderQueue = RenderQueue.Geometry;
            mat.color = ResourceTypeUtility.GetResourceColor(resourceType);
            renderer.material = mat;
        }
        return animItem;
    }
    
    /// <summary>
    /// ターゲット位置を計算（ランダムオフセット付き）
    /// </summary>
    private Vector3 CalculateTargetPosition(Vector3 basePosition)
    {
        Vector3 randomOffset = new Vector3(
            UnityEngine.Random.Range(-0.5f, 0.5f), // X軸方向のバラツキ
            UnityEngine.Random.Range(-0.1f, 0.3f),  // Y軸方向のバラツキ（トロッコの中に入るように）
            UnityEngine.Random.Range(-0.5f, 0.5f)  // Z軸方向のバラツキ
        );
        return basePosition + randomOffset;
    }
    
    /// <summary>
    /// アイテムを指定位置に移動させる
    /// </summary>
    private async UniTask MoveItemAsync(GameObject item, Vector3 startPos, Vector3 endPos)
    {
        float elapsedTime = 0f;
        float duration = Vector3.Distance(startPos, endPos) / itemMoveSpeed;

        while (elapsedTime < duration && item != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            // AnimationCurveを使用してスムーズな移動
            float curveValue = movementCurve.Evaluate(progress);
            Vector3 currentPosition = Vector3.Lerp(startPos, endPos, curveValue);
            
            item.transform.position = currentPosition;
            // 回転アニメーション
            item.transform.Rotate(0, 360f * Time.deltaTime, 0);

            await UniTask.Yield();
        }

        // 最終位置に設定
        if (item != null)
        {
            item.transform.position = endPos;
        }
    }
    
    #endregion
    
    #region デバッグ・ユーティリティ
    
    /// <summary>
    /// デバッグ情報を取得
    /// </summary>
    public string GetDebugInfo()
    {
        return $"MinecartInteractionSystem - " +
               $"Range: {minecartDetectionRange:F1}m, " +
               $"Transferring: {isTransferringItems}, " +
               $"Minecarts: {(minecartManager != null ? minecartManager.minecarts.Count : 0)}, " +
               $"TransferSpeed: {itemTransferSpeed}/s";
    }
    
    #endregion
}
