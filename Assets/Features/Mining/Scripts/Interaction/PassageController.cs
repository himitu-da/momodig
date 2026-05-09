using UnityEngine;

public class PassageController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private float cancelDistance = 0.2f;
    [SerializeField] private string destinationSceneName;
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 3f, 0f);

    [Header("Passage Stencil Mask")]
    [SerializeField] private bool maskPlayerWithPassageRenderer = true;
    [SerializeField] private MeshRenderer passageMaskRenderer;
    [SerializeField] private Material stencilMaskWriterMaterial;
    [SerializeField] private Material maskedPlayerMaterial;

    [Header("References")]
    [SerializeField] private MinecartManager minecartManager;

    private PlayerController playerController;
    private Transform playerTransform;
    private bool isPlayerInside;
    private bool isPassageActive;
    private bool hasTransferredItems;
    private float transitionStartY;
    private float transitionTargetY;
    private float transitionDirection;
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
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || playerController == null)
        {
            return;
        }

        if (!isPassageActive)
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

        if (!isPassageActive)
        {
            float requiredDirection = GetTransitionDirection();
            if (verticalInput * requiredDirection < requiredInputThreshold)
            {
                return;
            }

            StartPassage(requiredDirection);
        }

        UpdatePassage();
    }

    private void StartPassage(float requiredDirection)
    {
        if (playerTransform == null)
        {
            return;
        }

        playerController.IsInPassage = true;
        isPassageActive = true;
        transitionDirection = requiredDirection;
        transitionStartY = playerTransform.position.y;
        transitionTargetY = transform.position.y + travelOffset.y;

        if ((transitionTargetY - transitionStartY) * transitionDirection <= 0f)
        {
            transitionTargetY = transitionStartY + travelOffset.y;
        }

        if (maskPlayerWithPassageRenderer)
        {
            passageMaskSession.Begin(passageMaskRenderer, playerTransform, stencilMaskWriterMaterial, maskedPlayerMaterial);
        }
    }

    private void UpdatePassage()
    {
        passageMaskSession.Render();

        if (HasReachedTransitionTarget() && !hasTransferredItems)
        {
            CompletePassage();
            return;
        }

        if (HasReturnedPastStart())
        {
            ResetState();
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

    private float GetTransitionDirection()
    {
        if (Mathf.Approximately(travelOffset.y, 0f))
        {
            return 1f;
        }

        return Mathf.Sign(travelOffset.y);
    }

    private bool HasReachedTransitionTarget()
    {
        return playerTransform != null
            && (playerTransform.position.y - transitionTargetY) * transitionDirection >= 0f;
    }

    private bool HasReturnedPastStart()
    {
        return playerTransform != null
            && (playerTransform.position.y - transitionStartY) * transitionDirection < -cancelDistance;
    }

    private void ResetState()
    {
        if (playerController != null)
        {
            playerController.IsInPassage = false;
        }

        playerController = null;
        playerTransform = null;
        isPlayerInside = false;
        isPassageActive = false;
        hasTransferredItems = false;
        transitionStartY = 0f;
        transitionTargetY = 0f;
        transitionDirection = 0f;
        passageMaskSession.End();
    }

    private void ClearState()
    {
        playerController = null;
        playerTransform = null;
        isPlayerInside = false;
        isPassageActive = false;
        hasTransferredItems = false;
        transitionStartY = 0f;
        transitionTargetY = 0f;
        transitionDirection = 0f;
        passageMaskSession.End();
    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.IsInPassage = false;
        }

        passageMaskSession.End();
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
