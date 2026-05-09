using UnityEngine;

public class PassageController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float transitionSpeed = 0.5f;
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private string destinationSceneName;
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private Vector3 facingDirectionDuringTransition = Vector3.up;

    [Header("Passage Stencil Mask")]
    [SerializeField] private bool maskPlayerWithPassageRenderer = true;
    [SerializeField] private MeshRenderer passageMaskRenderer;
    [SerializeField] private Material stencilMaskWriterMaterial;
    [SerializeField] private Material maskedPlayerMaterial;

    [Header("References")]
    [SerializeField] private MinecartManager minecartManager;

    private PlayerController playerController;
    private PlayerVisualsController playerVisualsController;
    private Transform playerTransform;
    private Collider playerCollider;
    private Vector3 initialPlayerPosition;
    private Vector3 initialPlayerScale;
    private float entryProgress;
    private bool isPlayerInside;
    private bool hasTransferredItems;
    private readonly PassageStencilMaskSession passageMaskSession = new PassageStencilMaskSession();

    private void Awake()
    {
        if (minecartManager == null)
        {
            minecartManager = FindFirstObjectByType<MinecartManager>();
            if (minecartManager == null)
            {
                Debug.LogWarning("PassageController: MinecartManager was not found. Minecart resources will not be stored.");
            }
        }

        if (changeScene == null)
        {
            changeScene = FindFirstObjectByType<ChangeScene>();
        }

        if (passageMaskRenderer == null)
        {
            passageMaskRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || playerController != null)
        {
            return;
        }

        playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        isPlayerInside = true;
        playerTransform = playerController.transform;
        playerVisualsController = playerController.GetComponentInChildren<PlayerVisualsController>();
        initialPlayerPosition = playerTransform.position;
        initialPlayerScale = playerTransform.localScale;
        playerCollider = ResolvePlayerCollider(playerController.transform, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || playerController == null)
        {
            return;
        }

        if (!playerController.IsInPassage)
        {
            ClearState();
        }
    }

    private void Update()
    {
        if (!isPlayerInside || playerController == null)
        {
            return;
        }

        float verticalInput = playerController.MoveInput.y;

        if (!playerController.IsInPassage)
        {
            if (verticalInput <= requiredInputThreshold)
            {
                return;
            }

            StartPassage();
        }

        entryProgress += verticalInput * transitionSpeed * Time.deltaTime;
        entryProgress = Mathf.Clamp01(entryProgress);

        UpdateVisuals();

        if (verticalInput < 0f && Mathf.Approximately(entryProgress, 0f))
        {
            ResetState();
            return;
        }

        if (entryProgress >= 1f && !hasTransferredItems)
        {
            CompletePassage();
        }
    }

    private void StartPassage()
    {
        playerController.IsInPassage = true;

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        if (maskPlayerWithPassageRenderer)
        {
            passageMaskSession.Begin(passageMaskRenderer, playerTransform, stencilMaskWriterMaterial, maskedPlayerMaterial);
        }
    }

    private void CompletePassage()
    {
        TransferAllItemsToStorage();
        hasTransferredItems = true;

        if (changeScene != null && !string.IsNullOrEmpty(destinationSceneName))
        {
            changeScene.OnClickToChangeScene(destinationSceneName);
        }
        else
        {
            Debug.LogWarning("PassageController: ChangeScene or destination scene is not configured.");
        }

        enabled = false;
    }

    private void UpdateVisuals()
    {
        if (playerTransform != null)
        {
            playerTransform.position = Vector3.Lerp(initialPlayerPosition, initialPlayerPosition + travelOffset, entryProgress);
            playerTransform.localScale = initialPlayerScale;
        }

        if (playerVisualsController != null)
        {
            playerVisualsController.UpdateMovementAnimation(facingDirectionDuringTransition);
        }

        passageMaskSession.Render();
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
            playerTransform.position = initialPlayerPosition;
            playerTransform.localScale = initialPlayerScale;
        }

        playerController = null;
        playerVisualsController = null;
        playerTransform = null;
        playerCollider = null;
        entryProgress = 0f;
        isPlayerInside = false;
        hasTransferredItems = false;
        passageMaskSession.End();
    }

    private void ClearState()
    {
        playerController = null;
        playerVisualsController = null;
        playerTransform = null;
        playerCollider = null;
        entryProgress = 0f;
        isPlayerInside = false;
        hasTransferredItems = false;
    }

    private void OnDisable()
    {
        passageMaskSession.End();
    }

    private Collider ResolvePlayerCollider(Transform playerRoot, Collider triggerCollider)
    {
        Transform colliderTransform = playerRoot.Find("PlayerCollision");
        if (colliderTransform == null)
        {
            colliderTransform = playerRoot.Find("PlayerCollider");
        }

        if (colliderTransform != null && colliderTransform.TryGetComponent(out Collider resolvedCollider))
        {
            return resolvedCollider;
        }

        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            return triggerCollider;
        }

        Collider[] colliders = playerRoot.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
            {
                return colliders[i];
            }
        }

        return null;
    }

    private void TransferAllItemsToStorage()
    {
        if (playerController == null)
        {
            Debug.LogWarning("PassageController: PlayerController is null. Item transfer was skipped.");
            return;
        }

        StorageManager storageManager = StorageManager.Instance;
        if (storageManager == null)
        {
            Debug.LogError("PassageController: StorageManager was not found.");
            return;
        }

        var playerResources = playerController.Inventory.GetAllResources();
        foreach (var resource in playerResources)
        {
            if (resource.Value > 0)
            {
                storageManager.AddResource(resource.Key, resource.Value);
            }
        }

        foreach (var resource in playerResources)
        {
            if (resource.Value > 0)
            {
                playerController.Inventory.RemoveResource(resource.Key, resource.Value);
            }
        }

        if (minecartManager != null && minecartManager.minecarts != null)
        {
            foreach (var minecart in minecartManager.minecarts)
            {
                if (minecart == null || minecart.resources == null)
                {
                    continue;
                }

                foreach (var resource in minecart.resources)
                {
                    if (resource.Value > 0)
                    {
                        storageManager.AddResource(resource.Key, resource.Value);
                    }
                }

                minecart.ClearResources();
            }
        }
    }
}
