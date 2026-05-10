using UnityEngine;

public class PassageController : MonoBehaviour, IGameSceneTransitionHandler, IPassageAreaTriggerReceiver
{
    [Header("Transition")]
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private string destinationSceneName;
    [SerializeField] private string destinationEntryPointId;
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private bool useLinkedDestinationPosition = true;
    [SerializeField] private float destinationPlayerY = -5f;
    [SerializeField] private bool useLinkedTransitionBoundary = true;
    [SerializeField] private float transitionBoundaryY = 5f;
    [SerializeField] private bool continuePassageAfterSceneTransition = true;

    [Header("Passage Areas")]
    [SerializeField] private BoxCollider onAreaCollider;
    [SerializeField] private BoxCollider offAreaCollider;

    [Header("Passage Stencil Mask")]
    [SerializeField] private bool maskPlayerWithPassageRenderer = true;
    [SerializeField] private MeshRenderer passageMaskRenderer;
    [SerializeField] private Material stencilMaskWriterMaterial;
    [SerializeField] private Material maskedPlayerMaterial;

    [Header("References")]
    [SerializeField] private MinecartManager minecartManager;

    private PlayerController playerController;
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private Collider[] playerCollisionColliders;
    private bool[] playerCollisionColliderEnabledStates;
    private bool[] playerCollisionColliderTriggerStates;
    private bool isPlayerInside;
    private bool isPassageActive;
    private bool hasTransferredItems;
    private float transitionStartY;
    private float transitionTargetY;
    private float transitionDirection;
    private float passageMinX;
    private float passageMaxX;
    private bool isSceneTransitioning;
    private bool isContinuingFromSceneTransition;
    private bool isUsingTransitionBoundaryTarget;
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

        ResolveAreaColliders();
        ConfigureAreaTriggers();
    }

    public void OnPassageAreaTrigger(PassageAreaKind areaKind, Collider other, bool entered)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (areaKind == PassageAreaKind.On)
        {
            if (entered)
            {
                EnterOnArea(other);
            }
            else
            {
                ExitOnArea(other);
            }

            return;
        }

        if (areaKind == PassageAreaKind.Off && entered)
        {
            EnterOffArea(other);
        }
    }

    private void EnterOnArea(Collider other)
    {
        PlayerController enteringPlayerController = other.GetComponentInParent<PlayerController>();
        if (enteringPlayerController == null || (playerController != null && playerController != enteringPlayerController))
        {
            return;
        }

        playerController = enteringPlayerController;
        isPlayerInside = true;
        playerTransform = playerController.transform;
        playerRigidbody = playerController.GetComponent<Rigidbody>();
        CapturePassageBounds();
    }

    private void ExitOnArea(Collider other)
    {
        if (!IsCurrentPlayer(other) || isPassageActive)
        {
            return;
        }

        ClearState();
    }

    private void EnterOffArea(Collider other)
    {
        if (!IsCurrentPlayer(other))
        {
            return;
        }

        DeactivatePassage();
    }

    private bool IsCurrentPlayer(Collider other)
    {
        return playerController != null
            && other.GetComponentInParent<PlayerController>() == playerController;
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
            CapturePassageBounds();

            if (IsInOffArea())
            {
                return;
            }

            if (!IsInOnArea())
            {
                return;
            }

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
        StartPassage(requiredDirection, true);
    }

    private void StartPassage(float requiredDirection, bool useTransitionBoundary)
    {
        if (playerTransform == null)
        {
            return;
        }

        playerController.IsInPassage = true;
        isPassageActive = true;
        transitionDirection = requiredDirection;
        transitionStartY = playerTransform.position.y;
        float travelDistance = Mathf.Abs(travelOffset.y);
        bool useBoundaryTarget = useTransitionBoundary && useLinkedTransitionBoundary;
        isUsingTransitionBoundaryTarget = useBoundaryTarget;
        transitionTargetY = ResolveTransitionTargetY(requiredDirection, travelDistance, useTransitionBoundary);
        CapturePassageBounds();
        DisablePlayerCollision();

        if (!useBoundaryTarget && (transitionTargetY - transitionStartY) * transitionDirection <= 0f)
        {
            transitionTargetY = transitionStartY + travelDistance * transitionDirection;
        }

        if (maskPlayerWithPassageRenderer)
        {
            passageMaskSession.Begin(passageMaskRenderer, playerTransform, stencilMaskWriterMaterial, maskedPlayerMaterial);
        }
    }

    private void UpdatePassage()
    {
        passageMaskSession.Render();

        if (!ConstrainPassageMovement())
        {
            return;
        }

        if (HasReachedTransitionTarget() && !hasTransferredItems)
        {
            SnapPlayerToTransitionTarget();

            if (isContinuingFromSceneTransition)
            {
                DeactivatePassage();
                return;
            }

            CompletePassage();
            return;
        }

        if (IsInOffArea())
        {
            DeactivatePassage();
            return;
        }

    }

    private void CapturePassageBounds()
    {
        Bounds bounds = GetOnAreaBounds();

        passageMinX = bounds.min.x;
        passageMaxX = bounds.max.x;
        if (passageMinX > passageMaxX)
        {
            float swap = passageMinX;
            passageMinX = passageMaxX;
            passageMaxX = swap;
        }
    }

    private void DisablePlayerCollision()
    {
        if (playerTransform == null)
        {
            return;
        }

        playerCollisionColliders = playerTransform.GetComponentsInChildren<Collider>(true);
        playerCollisionColliderEnabledStates = new bool[playerCollisionColliders.Length];
        playerCollisionColliderTriggerStates = new bool[playerCollisionColliders.Length];

        for (int i = 0; i < playerCollisionColliders.Length; i++)
        {
            Collider playerCollider = playerCollisionColliders[i];
            if (playerCollider == null)
            {
                continue;
            }

            playerCollisionColliderEnabledStates[i] = playerCollider.enabled;
            playerCollisionColliderTriggerStates[i] = playerCollider.isTrigger;
            if (playerCollider.enabled && !playerCollider.isTrigger)
            {
                playerCollider.isTrigger = true;
            }
        }
    }

    private void RestorePlayerCollision()
    {
        if (playerCollisionColliders != null && playerCollisionColliderEnabledStates != null && playerCollisionColliderTriggerStates != null)
        {
            int count = Mathf.Min(playerCollisionColliders.Length, playerCollisionColliderEnabledStates.Length, playerCollisionColliderTriggerStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (playerCollisionColliders[i] != null)
                {
                    playerCollisionColliders[i].isTrigger = playerCollisionColliderTriggerStates[i];
                    playerCollisionColliders[i].enabled = playerCollisionColliderEnabledStates[i];
                }
            }
        }

        playerCollisionColliders = null;
        playerCollisionColliderEnabledStates = null;
        playerCollisionColliderTriggerStates = null;
    }

    private bool ConstrainPassageMovement()
    {
        if (playerTransform == null)
        {
            return false;
        }

        Vector3 position = playerTransform.position;
        bool clampedX = false;
        if (position.x < passageMinX)
        {
            position.x = passageMinX;
            clampedX = true;
        }
        else if (position.x > passageMaxX)
        {
            position.x = passageMaxX;
            clampedX = true;
        }

        if (clampedX)
        {
            playerTransform.position = position;
            StopBlockedVelocity(true, false);
        }

        return true;
    }

    private float ResolveTransitionTargetY(float requiredDirection, float travelDistance, bool useTransitionBoundary)
    {
        if (useTransitionBoundary && useLinkedTransitionBoundary)
        {
            return transitionBoundaryY;
        }

        if (!useTransitionBoundary)
        {
            return transitionStartY + travelDistance * requiredDirection;
        }

        return transform.position.y + travelDistance * requiredDirection;
    }

    private void SnapPlayerToTransitionTarget()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector3 position = playerTransform.position;
        position.y = transitionTargetY;
        playerTransform.position = position;
        StopBlockedVelocity(false, true);
    }

    private void StopBlockedVelocity(bool stopX, bool stopY)
    {
        if (playerRigidbody == null)
        {
            return;
        }

        Vector3 velocity = playerRigidbody.linearVelocity;
        if (stopX)
        {
            velocity.x = 0f;
        }

        if (stopY)
        {
            velocity.y = 0f;
        }

        playerRigidbody.linearVelocity = velocity;
    }

    private void CompletePassage()
    {
        TransferAllItemsToStorage();
        hasTransferredItems = true;

        if (changeScene != null && !string.IsNullOrEmpty(destinationSceneName))
        {
            PrepareForSceneTransition();
            if (useLinkedDestinationPosition)
            {
                changeScene.OnClickToChangeScene(destinationSceneName, destinationEntryPointId, GetDestinationPlayerPosition());
            }
            else
            {
                changeScene.OnClickToChangeScene(destinationSceneName, destinationEntryPointId);
            }
        }
        else
        {
            Debug.LogWarning("PassageController: ChangeScene or destination scene is not configured.");
        }

        enabled = false;
    }

    private Vector3 GetDestinationPlayerPosition()
    {
        Vector3 sourcePosition = playerTransform != null ? playerTransform.position : transform.position;
        return new Vector3(sourcePosition.x, destinationPlayerY, sourcePosition.z);
    }

    public void OnBeforeContentSceneUnload(string nextSceneName)
    {
    }

    public void OnAfterContentSceneLoad(string previousSceneName)
    {
        if (!continuePassageAfterSceneTransition || previousSceneName != destinationSceneName)
        {
            return;
        }

        StartPassageFromSceneTransition();
    }

    private void StartPassageFromSceneTransition()
    {
        GameObject player = SceneEntryPoint.FindTaggedObjectInScene(gameObject.scene, "Player");
        if (player == null)
        {
            Debug.LogWarning($"PassageController: Player tagged 'Player' was not found in scene '{gameObject.scene.name}'.");
            return;
        }

        playerController = player.GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("PassageController: PlayerController was not found on the linked player.");
            return;
        }

        isPlayerInside = true;
        playerTransform = playerController.transform;
        playerRigidbody = playerController.GetComponent<Rigidbody>();
        isContinuingFromSceneTransition = true;
        isSceneTransitioning = false;

        StartPassage(-GetTransitionDirection(), false);
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
        if (playerTransform == null)
        {
            return false;
        }

        if (isUsingTransitionBoundaryTarget)
        {
            return playerTransform.position.y >= transitionBoundaryY;
        }

        return (playerTransform.position.y - transitionTargetY) * transitionDirection >= 0f;
    }

    private bool IsInOffArea()
    {
        if (playerTransform == null || offAreaCollider == null)
        {
            return false;
        }

        return ContainsPoint2D(offAreaCollider.bounds, playerTransform.position);
    }

    private bool IsInOnArea()
    {
        return playerTransform != null
            && ContainsPoint2D(GetOnAreaBounds(), playerTransform.position);
    }

    private Bounds GetOnAreaBounds()
    {
        if (onAreaCollider != null)
        {
            return onAreaCollider.bounds;
        }

        Collider passageCollider = GetComponent<Collider>();
        if (passageCollider != null)
        {
            return passageCollider.bounds;
        }

        if (passageMaskRenderer != null)
        {
            return passageMaskRenderer.bounds;
        }

        return new Bounds(transform.position, new Vector3(1f, 1f, 1f));
    }

    private static bool ContainsPoint2D(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x
            && point.x <= bounds.max.x
            && point.y >= bounds.min.y
            && point.y <= bounds.max.y;
    }

    private void ResolveAreaColliders()
    {
        if (onAreaCollider == null)
        {
            onAreaCollider = FindChildBoxCollider("PassageOnArea", "OnArea", "ONArea");
        }

        if (offAreaCollider == null)
        {
            offAreaCollider = FindChildBoxCollider("PassageOffArea", "OffArea", "OFFArea");
        }
    }

    private void ConfigureAreaTriggers()
    {
        PassageAreaTrigger.Attach(onAreaCollider, this, PassageAreaKind.On);
        PassageAreaTrigger.Attach(offAreaCollider, this, PassageAreaKind.Off);
    }

    private BoxCollider FindChildBoxCollider(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = transform.Find(names[i]);
            if (child != null && child.TryGetComponent(out BoxCollider boxCollider))
            {
                return boxCollider;
            }
        }

        return null;
    }

    private void DeactivatePassage()
    {
        if (isSceneTransitioning)
        {
            return;
        }

        if (playerController != null)
        {
            playerController.IsInPassage = false;
        }

        isPassageActive = false;
        hasTransferredItems = false;
        isContinuingFromSceneTransition = false;
        isUsingTransitionBoundaryTarget = false;
        transitionStartY = 0f;
        transitionTargetY = 0f;
        transitionDirection = 0f;
        RestorePlayerCollision();
        passageMaskSession.End();
    }

    private void ClearState()
    {
        DeactivatePassage();
        playerController = null;
        playerTransform = null;
        playerRigidbody = null;
        isPlayerInside = false;
        hasTransferredItems = false;
        isContinuingFromSceneTransition = false;
        isUsingTransitionBoundaryTarget = false;
        passageMinX = 0f;
        passageMaxX = 0f;
    }

    private void OnDisable()
    {
        DeactivatePassage();
    }

    private void PrepareForSceneTransition()
    {
        isSceneTransitioning = true;
        RestorePlayerCollision();

        if (playerTransform == null)
        {
            return;
        }

        Renderer[] renderers = playerTransform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }
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
