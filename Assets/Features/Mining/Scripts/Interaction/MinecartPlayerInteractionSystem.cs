using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

public class MinecartPlayerInteractionSystem : MonoBehaviour
{
    [Header("Minecart Interaction")]
    [SerializeField] private float minecartDetectionRange = 3f;
    [SerializeField] private float itemTransferSpeed = 50f;

    [Header("Animation")]
    [SerializeField] private float itemMoveSpeed = 5f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("References")]
    [SerializeField] private MinecartManager minecartManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TerrainDataManager terrainDataManager;

    private bool isTransferringItems;
    private float minecartOffset = 2f;

    void Awake()
    {
        if (terrainDataManager == null)
        {
            terrainDataManager = VoxelItemVisualUtility.ResolveTerrainDataManager();
        }

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

    public void CheckMinecartProximity()
    {
        if (isTransferringItems || playerController == null || playerController.Inventory == null || playerController.Inventory.IsEmpty())
        {
            return;
        }

        GameObject nearestMinecart = GetNearestMinecart();
        if (nearestMinecart == null)
        {
            return;
        }

        float distance = Vector3.Distance(playerController.transform.position, nearestMinecart.transform.position);
        if (distance <= minecartDetectionRange)
        {
            TransferItemsToMinecart(nearestMinecart).Forget();
        }
    }

    public float GetDetectionRange() => minecartDetectionRange;
    public void SetDetectionRange(float range) => minecartDetectionRange = range;
    public bool IsTransferringItems() => isTransferringItems;

    private void ValidateReferences()
    {
        if (minecartManager == null)
        {
            Debug.LogWarning("MinecartInteractionSystem: MinecartManager is not assigned.");
        }

        if (playerController == null)
        {
            Debug.LogError("MinecartInteractionSystem: PlayerController is not assigned.");
        }
    }

    private GameObject GetNearestMinecart()
    {
        GameObject nearest = null;
        float nearestDistance = float.MaxValue;

        if (minecartManager == null)
        {
            return null;
        }

        foreach (Minecart cart in minecartManager.minecarts)
        {
            if (cart == null || cart.gameObject == null)
            {
                continue;
            }

            float distance = Vector3.Distance(playerController.transform.position, cart.gameObject.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = cart.gameObject;
            }
        }

        return nearest;
    }

    private async UniTask TransferItemsToMinecart(GameObject targetMinecart)
    {
        isTransferringItems = true;

        while (!playerController.Inventory.IsEmpty())
        {
            if (!IsMinecartManagerAvailable())
            {
                break;
            }

            Minecart frontCart = minecartManager.minecarts[0];
            GameObject currentTargetMinecart = frontCart.gameObject;
            if (currentTargetMinecart == null)
            {
                Debug.LogError("MinecartInteractionSystem: front minecart has no GameObject.");
                break;
            }

            float currentDistance = Vector3.Distance(playerController.transform.position, currentTargetMinecart.transform.position);
            if (currentDistance > minecartDetectionRange)
            {
                Debug.Log($"MinecartInteractionSystem: transfer stopped because minecart moved away (distance: {currentDistance:F2}).");
                break;
            }

            int capacity = minecartManager.CartCapacity.IntValue;
            if (!frontCart.HasCapacity(capacity))
            {
                Debug.Log("MinecartInteractionSystem: front minecart is full.");
                break;
            }

            if (!playerController.Inventory.TryPeekNextItem(out VoxelItemData itemData))
            {
                Debug.LogError("MinecartInteractionSystem: failed to read next voxel item from inventory.");
                break;
            }

            GameObject animItem;
            if (!VoxelItemVisualUtility.TryCreateAnimationItem(
                    playerController.transform.position,
                    transform,
                    itemData,
                    terrainDataManager,
                    "MinecartInteractionSystem",
                    out animItem))
            {
                break;
            }

            if (!playerController.Inventory.TryRemoveNextItem(out VoxelItemData removedItem))
            {
                Debug.LogError("MinecartInteractionSystem: failed to remove voxel item after animation object was created.");
                Destroy(animItem);
                break;
            }

            if (!minecartManager.AddItemToFrontCart(removedItem))
            {
                Debug.LogError("MinecartInteractionSystem: failed to add voxel item to minecart after capacity check.");
                playerController.Inventory.AddItem(removedItem);
                Destroy(animItem);
                break;
            }

            AnimateItemTransfer(animItem, playerController.transform.position, currentTargetMinecart.transform.position).Forget();
            await UniTask.Delay(TimeSpan.FromSeconds(1f / itemTransferSpeed));
        }

        isTransferringItems = false;
    }

    private bool IsMinecartManagerAvailable()
    {
        if (minecartManager == null)
        {
            Debug.LogError("MinecartInteractionSystem: MinecartManager is null.");
            return false;
        }

        if (!minecartManager.digable)
        {
            return false;
        }

        if (minecartManager.minecarts.Count == 0)
        {
            Debug.LogError("MinecartInteractionSystem: no minecarts are available.");
            return false;
        }

        return true;
    }

    private async UniTask AnimateItemTransfer(GameObject animItem, Vector3 startPos, Vector3 endPos)
    {
        Vector3 targetPos = CalculateTargetPosition(endPos);
        await MoveItemAsync(animItem, startPos, targetPos);
        if (animItem != null)
        {
            Destroy(animItem);
        }
    }

    private Vector3 CalculateTargetPosition(Vector3 basePosition)
    {
        Vector3 randomOffset = new Vector3(
            UnityEngine.Random.Range(-0.5f, 0.5f),
            UnityEngine.Random.Range(-0.1f, 0.3f),
            UnityEngine.Random.Range(-0.5f, 0.5f));

        return basePosition + randomOffset;
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
