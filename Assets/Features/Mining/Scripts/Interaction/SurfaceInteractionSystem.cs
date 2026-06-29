using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

public class SurfaceInteractionSystem : MonoBehaviour
{
    [Header("Surface Interaction")]
    [SerializeField] private float surfaceDetectionRange = 5f;
    [SerializeField] private Transform surfaceReturnPoint;
    [SerializeField] private float itemTransferSpeed = 50f;

    [Header("Animation")]
    [SerializeField] private float itemMoveSpeed = 5f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private StorageManager storageManager;
    [SerializeField] private TerrainDataManager terrainDataManager;
    [SerializeField] private ConveyorExportSystem conveyorExportSystem;

    private bool isTransferringItems;

    void Awake()
    {
        if (playerController == null)
        {
            Debug.LogError("SurfaceInteractionSystem: PlayerController is not assigned.", this);
        }

        if (storageManager == null)
        {
            Debug.LogError("SurfaceInteractionSystem: StorageManager is not assigned.", this);
        }

        if (terrainDataManager == null)
        {
            Debug.LogError("SurfaceInteractionSystem: TerrainDataManager is not assigned.", this);
        }
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
        if (!isTransferringItems)
        {
            TransferAllInventoryToStorage();
        }
    }

    private void CheckSurfaceProximity()
    {
        if (isTransferringItems || playerController.Inventory.IsEmpty())
        {
            return;
        }

        if (conveyorExportSystem != null && conveyorExportSystem.IsUnlocked)
        {
            return;
        }

        float distance = Vector3.Distance(playerController.transform.position, surfaceReturnPoint.position);
        if (distance <= surfaceDetectionRange)
        {
            TransferItemsToStorage().Forget();
        }
    }

    public void TransferAllInventoryToStorage()
    {
        if (playerController == null || storageManager == null)
        {
            return;
        }

        if (!playerController.Inventory.TryDrainAllItems(out List<VoxelItemData> itemData))
        {
            Debug.LogError("SurfaceInteractionSystem: inventory contains invalid voxel item data. Transfer aborted.");
            return;
        }

        if (itemData.Count == 0)
        {
            return;
        }

        if (!VoxelItemData.TryAggregateResourceCounts(itemData, out Dictionary<ResourceType, int> resourceCounts, "SurfaceInteractionSystem.TransferAllInventoryToStorage"))
        {
            return;
        }

        storageManager.AddResources(resourceCounts);
        Debug.Log("All inventory voxel items transferred to storage.");
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

            if (!playerController.Inventory.TryPeekNextItem(out VoxelItemData itemData))
            {
                Debug.LogError("SurfaceInteractionSystem: failed to read next voxel item from inventory.");
                break;
            }

            GameObject animItem;
            if (!VoxelItemVisualUtility.TryCreateAnimationItem(
                    playerController.transform.position,
                    transform,
                    itemData,
                    terrainDataManager,
                    "SurfaceInteractionSystem",
                    out animItem))
            {
                break;
            }

            if (!playerController.Inventory.TryRemoveNextItem(out VoxelItemData removedItem))
            {
                Debug.LogError("SurfaceInteractionSystem: failed to remove voxel item after animation object was created.");
                Destroy(animItem);
                break;
            }

            storageManager.AddResource(removedItem.resourceType, 1);
            AnimateItemTransfer(animItem, playerController.transform.position, surfaceReturnPoint.position).Forget();
            await UniTask.Delay(TimeSpan.FromSeconds(1f / itemTransferSpeed));
        }

        isTransferringItems = false;
    }

    private async UniTask AnimateItemTransfer(GameObject animItem, Vector3 startPos, Vector3 endPos)
    {
        await MoveItemAsync(animItem, startPos, endPos);
        if (animItem != null)
        {
            Destroy(animItem);
        }
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
}
