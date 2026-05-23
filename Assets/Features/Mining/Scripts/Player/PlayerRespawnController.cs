using System;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

public class PlayerRespawnController : MonoBehaviour
{
    private static readonly ProfilerMarker RespawnMarker =
        new ProfilerMarker("PlayerRespawnController.Respawn");
    private static readonly ProfilerMarker ReleaseInventoryMarker =
        new ProfilerMarker("PlayerRespawnController.ReleaseInventory");

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private DroppedItemManager droppedItemManager;
    [SerializeField] private TerrainManager terrainManager;
    [SerializeField] private TerrainDataManager terrainDataManager;
    [SerializeField] private MiningLogSystem miningLogSystem;

    [Header("Respawn Timing")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float hiddenDuration = 0.1f;
    [SerializeField] private float fadeInDuration = 0.35f;

    [Header("Item Release")]
    [SerializeField] private float releaseRadius = 0.8f;
    [SerializeField] private float releaseHorizontalVelocity = 0.25f;
    [SerializeField] private float releaseUpwardVelocity = 0.2f;
    [SerializeField] private float releaseAngularVelocity = 0.8f;
    [SerializeField] private int maxReleasedItemsPerFrame = 8;

    [Header("Log")]
    [SerializeField] private string respawnLogMessage = "百々世はリスポーンした！";

    private bool isRespawning;

    public bool IsRespawning => isRespawning;

    public void RequestRespawn()
    {
        if (isRespawning)
        {
            return;
        }

        RespawnAsync(destroyCancellationToken).Forget();
    }

    private async UniTask RespawnAsync(System.Threading.CancellationToken cancellationToken)
    {
        using (RespawnMarker.Auto())
        {
            if (!ValidateReferences())
            {
                return;
            }

            isRespawning = true;
            playerController.SetControlLocked(true);
            playerController.SetItemPickupLocked(true);

            try
            {
                if (!await ReleaseInventoryItemsAsync(cancellationToken))
                {
                    return;
                }

                miningLogSystem.ShowLog(respawnLogMessage);
                await FadePlayerAsync(1f, 0f, fadeOutDuration, cancellationToken);

                if (hiddenDuration > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(hiddenDuration), cancellationToken: cancellationToken);
                }

                playerController.TeleportTo(respawnPoint.position);
                if (playerRigidbody != null)
                {
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }

                await FadePlayerAsync(0f, 1f, fadeInDuration, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                SetPlayerAlpha(1f);
                playerController.SetItemPickupLocked(false);
                playerController.SetControlLocked(false);
                isRespawning = false;
            }
        }
    }

    private async UniTask<bool> ReleaseInventoryItemsAsync(System.Threading.CancellationToken cancellationToken)
    {
        using (ReleaseInventoryMarker.Auto())
        {
            if (playerController.Inventory == null)
            {
                Debug.LogError("PlayerRespawnController: PlayerController.Inventory is null.", this);
                return false;
            }

            int releasedCount = 0;
            while (!playerController.Inventory.IsEmpty())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!playerController.Inventory.TryPeekNextItem(out VoxelItemData itemData))
                {
                    Debug.LogError("PlayerRespawnController: failed to read next voxel item from inventory. Respawn aborted.", this);
                    return false;
                }

                Vector3 direction = CalculateReleaseDirection(releasedCount);
                Vector3 position = CalculateReleasePosition(releasedCount, direction);
                Vector3 velocity = direction * releaseHorizontalVelocity + Vector3.up * releaseUpwardVelocity;
                Vector3 angularVelocity = Vector3.forward * releaseAngularVelocity * (releasedCount % 2 == 0 ? 1f : -1f);

                if (!droppedItemManager.TrySpawnDroppedItemFromVoxelItemData(
                        position,
                        itemData,
                        terrainDataManager,
                        velocity,
                        angularVelocity,
                        out GameObject spawnedItem))
                {
                    Debug.LogError("PlayerRespawnController: failed to release an inventory item. Respawn aborted.", this);
                    return false;
                }

                if (!playerController.Inventory.TryRemoveNextItem(out VoxelItemData removedItem))
                {
                    Debug.LogError("PlayerRespawnController: failed to remove released item from inventory. Respawn aborted.", this);
                    droppedItemManager.ReturnItem(spawnedItem);
                    return false;
                }

                if (removedItem.blockDataName != itemData.blockDataName)
                {
                    Debug.LogError(
                        $"PlayerRespawnController: released item mismatch. Expected='{itemData.blockDataName}', Removed='{removedItem.blockDataName}'.",
                        this);
                    return false;
                }

                releasedCount++;
                if (releasedCount % maxReleasedItemsPerFrame == 0)
                {
                    await UniTask.Yield(cancellationToken);
                }
            }

            return true;
        }
    }

    private async UniTask FadePlayerAsync(
        float fromAlpha,
        float toAlpha,
        float duration,
        System.Threading.CancellationToken cancellationToken)
    {
        if (duration <= 0f)
        {
            SetPlayerAlpha(toAlpha);
            return;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetPlayerAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            await UniTask.Yield(cancellationToken);
        }

        SetPlayerAlpha(toAlpha);
    }

    private void SetPlayerAlpha(float alpha)
    {
        Color color = playerSpriteRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        playerSpriteRenderer.color = color;
    }

    private Vector3 CalculateReleaseDirection(int index)
    {
        float angle = index * 137.50776f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.35f, 0f).normalized;
    }

    private Vector3 CalculateReleasePosition(int index, Vector3 direction)
    {
        Vector3 position = playerController.transform.position + direction * releaseRadius;
        TerrainSettings settings = terrainManager.Settings;
        int voxelsPerBlock = Mathf.Max(1, settings.voxelsPerBlock);
        float voxelWorldSize = settings.blockSize / voxelsPerBlock;
        int zLayer = index % voxelsPerBlock;
        float centeredLayer = zLayer - (voxelsPerBlock - 1) * 0.5f;
        position.z = playerController.transform.position.z + centeredLayer * voxelWorldSize;
        return position;
    }

    private bool ValidateReferences()
    {
        bool isValid = true;
        isValid &= ValidateReference(playerController, nameof(playerController));
        isValid &= ValidateReference(playerRigidbody, nameof(playerRigidbody));
        isValid &= ValidateReference(playerSpriteRenderer, nameof(playerSpriteRenderer));
        isValid &= ValidateReference(respawnPoint, nameof(respawnPoint));
        isValid &= ValidateReference(droppedItemManager, nameof(droppedItemManager));
        isValid &= ValidateReference(terrainManager, nameof(terrainManager));
        isValid &= ValidateReference(terrainDataManager, nameof(terrainDataManager));
        isValid &= ValidateReference(miningLogSystem, nameof(miningLogSystem));
        if (terrainManager != null)
        {
            TerrainSettings settings = terrainManager.Settings;
            if (settings == null || settings.voxelsPerBlock <= 0 || settings.blockSize <= 0f)
            {
                Debug.LogError(
                    $"PlayerRespawnController: invalid TerrainManager settings. voxelsPerBlock={settings?.voxelsPerBlock}, blockSize={settings?.blockSize}",
                    this);
                isValid = false;
            }
        }

        return isValid;
    }

    private bool ValidateReference(UnityEngine.Object target, string fieldName)
    {
        if (target != null)
        {
            return true;
        }

        Debug.LogError($"PlayerRespawnController: {fieldName} is not configured.", this);
        return false;
    }

    private void OnValidate()
    {
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        hiddenDuration = Mathf.Max(0f, hiddenDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        releaseRadius = Mathf.Max(0f, releaseRadius);
        releaseHorizontalVelocity = Mathf.Max(0f, releaseHorizontalVelocity);
        releaseUpwardVelocity = Mathf.Max(0f, releaseUpwardVelocity);
        releaseAngularVelocity = Mathf.Max(0f, releaseAngularVelocity);
        maxReleasedItemsPerFrame = Mathf.Max(1, maxReleasedItemsPerFrame);
    }
}
