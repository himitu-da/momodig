using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;

/// <summary>
/// プレイヤーのインベントリから地上ストレージへアイテムを輸送するシステム
/// </summary>
public class SurfaceInteractionSystem : MonoBehaviour
{
    [Header("地上インタラクション設定")]
    [SerializeField] private float surfaceDetectionRange = 5f; // 地上への道を検出する範囲
    [SerializeField] private Transform surfaceReturnPoint; // 地上への帰還地点（アイテムの送り先）
    [SerializeField] private float itemTransferSpeed = 50f; // アイテム転送速度（個/秒）

    [Header("アニメーション設定")]
    [SerializeField] private float itemMoveSpeed = 5f; // アイテムの移動速度
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 移動カーブ

    [Header("参照")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private StorageManager storageManager;

    private bool isTransferringItems = false;

    void Awake()
    {
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (storageManager == null) storageManager = StorageManager.Instance;
    }

    void Update()
    {
        if (playerController != null && surfaceReturnPoint != null)
        {
            CheckSurfaceProximity();
        }
    }

    void OnDestroy()
    {
        // アイテム転送中にシーンが切り替わった場合でも、残りのアイテムを確実に転送する
        if (!isTransferringItems)
        {
            TransferAllInventoryToStorage();
        }
    }

    private void CheckSurfaceProximity()
    {
        if (isTransferringItems || playerController.Inventory.IsEmpty()) return;

        float distance = Vector3.Distance(playerController.transform.position, surfaceReturnPoint.position);
        if (distance <= surfaceDetectionRange)
        {
            TransferItemsToStorage().Forget();
        }
    }

    /// <summary>
    /// インベントリ内の全アイテムをストレージに転送する（アニメーションなし）
    /// </summary>
    public void TransferAllInventoryToStorage()
    {
        if (playerController == null || storageManager == null) return;

        var allResources = playerController.Inventory.GetAllResources();
        foreach (var resource in allResources)
        {
            if (resource.Value > 0)
            {
                int amountToRemove = resource.Value;
                playerController.Inventory.RemoveResource(resource.Key, amountToRemove);
                storageManager.AddResource(resource.Key, amountToRemove);
            }
        }
        Debug.Log("All inventory items transferred to storage.");
    }

    private async UniTask TransferItemsToStorage()
    {
        isTransferringItems = true;

        while (!playerController.Inventory.IsEmpty())
        {
            float currentDistance = Vector3.Distance(playerController.transform.position, surfaceReturnPoint.position);
            if (currentDistance > surfaceDetectionRange)
            {
                Debug.Log("Player moved away from the surface point. Stopping transfer.");
                break;
            }

            ResourceType typeToTransfer = GetNextResourceType();
            if (typeToTransfer == ResourceType.Stone && playerController.Inventory.GetResourceCount(ResourceType.Stone) == 0)
            {
                // A small check to see if there's anything left to transfer at all.
                if(playerController.Inventory.IsEmpty()) break;
            }

            int removedAmount = playerController.Inventory.RemoveResource(typeToTransfer, 1);
            if (removedAmount > 0)
            {
                storageManager.AddResource(typeToTransfer, removedAmount);
                AnimateItemTransfer(playerController.transform.position, surfaceReturnPoint.position, typeToTransfer).Forget();
                await UniTask.Delay(TimeSpan.FromSeconds(1f / itemTransferSpeed));
            }
            else
            {
                // This type is empty, try the next one in the next loop iteration.
                continue;
            }
        }

        isTransferringItems = false;
    }

    private ResourceType GetNextResourceType()
    {
        var allResources = playerController.Inventory.GetAllResources();
        foreach (var kvp in allResources)
        {
            if (kvp.Value > 0)
            {
                return kvp.Key;
            }
        }
        return ResourceType.Stone; // Default
    }

    #region Animation System (from MinecartPlayerInteractionSystem)

    private async UniTask AnimateItemTransfer(Vector3 startPos, Vector3 endPos, ResourceType resourceType)
    {
        GameObject animItem = CreateAnimationItem(startPos, resourceType);
        await MoveItemAsync(animItem, startPos, endPos);
        if (animItem != null)
        {
            Destroy(animItem);
        }
    }

    private GameObject CreateAnimationItem(Vector3 position, ResourceType resourceType)
    {
        GameObject animItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        animItem.transform.SetParent(transform);
        animItem.transform.position = position;
        animItem.transform.localScale = Vector3.one * 0.3f;

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

    private async UniTask MoveItemAsync(GameObject item, Vector3 startPos, Vector3 endPos)
    {
        float elapsedTime = 0f;
        float duration = Vector3.Distance(startPos, endPos) / itemMoveSpeed;

        while (elapsedTime < duration && item != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            float curveValue = movementCurve.Evaluate(progress);
            item.transform.position = Vector3.Lerp(startPos, endPos, curveValue);
            item.transform.Rotate(0, 360f * Time.deltaTime, 0);
            await UniTask.Yield();
        }

        if (item != null)
        {
            item.transform.position = endPos;
        }
    }

    #endregion
}
