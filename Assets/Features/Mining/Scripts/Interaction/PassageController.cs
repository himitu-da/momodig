using UnityEngine;

public class PassageController : MonoBehaviour, IGameSceneTransitionHandler, IPassageAreaTriggerReceiver
{
    [Header("Transition")]
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private string destinationSceneName;
    [SerializeField] private string destinationEntryPointId;
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 3f, 0f);
    [SerializeField] private bool continuePassageAfterSceneTransition = true;

    [Header("Passage Areas")]
    [SerializeField] private BoxCollider onAreaCollider;
    [SerializeField] private BoxCollider offAreaCollider;
    [SerializeField] private BoxCollider movementAreaCollider;
    [SerializeField] private BoxCollider transitionAreaCollider;
    [SerializeField] private BoxCollider transitionGateCollider;

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
    private float passageMinX;
    private float passageMaxX;
    private bool isSceneTransitioning;
    private bool isIgnoringArrivalGateOverlap;
    private bool hasPreviousPassagePosition;
    private Vector3 previousPassagePosition;
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

            StartPassage();
        }

        UpdatePassage();
    }

    private void StartPassage(bool ignoreCurrentGateOverlap = false)
    {
        if (playerTransform == null || !HasRequiredAreaColliders())
        {
            return;
        }

        playerController.IsInPassage = true;
        isPassageActive = true;
        isIgnoringArrivalGateOverlap = ignoreCurrentGateOverlap && IsInTransitionGateArea();
        StorePreviousPassagePosition();
        CapturePassageBounds();
        DisablePlayerCollision();

        if (maskPlayerWithPassageRenderer)
        {
            passageMaskSession.Begin(passageMaskRenderer, playerTransform, stencilMaskWriterMaterial, maskedPlayerMaterial);
        }
    }

    private void UpdatePassage()
    {
        passageMaskSession.Render();

        if (ShouldCompletePassage())
        {
            CompletePassage();
            return;
        }

        if (!ConstrainPassageMovement())
        {
            return;
        }

        if (ShouldCompletePassage())
        {
            CompletePassage();
            return;
        }

        if (IsInOffArea())
        {
            DeactivatePassage();
            return;
        }

        StorePreviousPassagePosition();
    }

    private void CapturePassageBounds()
    {
        if (movementAreaCollider == null)
        {
            passageMinX = 0f;
            passageMaxX = 0f;
            return;
        }

        Bounds bounds = movementAreaCollider.bounds;

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
        if (playerTransform == null || movementAreaCollider == null)
        {
            return false;
        }

        Bounds bounds = movementAreaCollider.bounds;
        Vector3 position = playerTransform.position;
        bool clampedX = false;
        bool clampedY = false;
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

        if (position.y < bounds.min.y)
        {
            position.y = bounds.min.y;
            clampedY = true;
        }
        else if (position.y > bounds.max.y)
        {
            position.y = bounds.max.y;
            clampedY = true;
        }

        if (clampedX || clampedY)
        {
            playerTransform.position = position;
            StopBlockedVelocity(clampedX, clampedY);
        }

        return true;
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
        if (changeScene != null && !string.IsNullOrEmpty(destinationSceneName))
        {
            if (!TryBeginPassageTransition())
            {
                return;
            }

            TransferAllItemsToStorage();
            hasTransferredItems = true;
            PrepareForSceneTransition();
            changeScene.OnClickToChangeScene(destinationSceneName, destinationEntryPointId);
        }
        else
        {
            Debug.LogWarning("PassageController: ChangeScene or destination scene is not configured.");
        }

        enabled = false;
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

        StartPassageFromSceneTransition(previousSceneName);
    }

    private void StartPassageFromSceneTransition(string previousSceneName)
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

        if (!TryPlacePlayerFromPassageTransition(previousSceneName, player.transform))
        {
            return;
        }

        isPlayerInside = true;
        playerTransform = playerController.transform;
        playerRigidbody = playerController.GetComponent<Rigidbody>();
        isSceneTransitioning = false;

        StartPassage(true);
    }

    private bool TryBeginPassageTransition()
    {
        if (playerTransform == null || transitionAreaCollider == null)
        {
            Debug.LogError("PassageController: Cannot transition because the player or TransitionArea is missing.");
            return false;
        }

        PassageTransitionContext.Begin(gameObject.scene.name, destinationSceneName, transitionAreaCollider, playerTransform.position);
        return true;
    }

    private bool TryPlacePlayerFromPassageTransition(string previousSceneName, Transform player)
    {
        if (player == null || transitionAreaCollider == null)
        {
            return false;
        }

        if (!PassageTransitionContext.TryConsume(previousSceneName, gameObject.scene.name, transitionAreaCollider, out Vector3 targetPosition))
        {
            return false;
        }

        player.position = targetPosition;

        if (player.TryGetComponent(out Rigidbody targetRigidbody))
        {
            targetRigidbody.linearVelocity = Vector3.zero;
            targetRigidbody.angularVelocity = Vector3.zero;
        }

        return true;
    }

    private float GetTransitionDirection()
    {
        if (Mathf.Approximately(travelOffset.y, 0f))
        {
            return 1f;
        }

        return Mathf.Sign(travelOffset.y);
    }

    private bool ShouldCompletePassage()
    {
        if (hasTransferredItems)
        {
            return false;
        }

        if (isIgnoringArrivalGateOverlap)
        {
            if (ShouldCompleteFromArrivalGateOverlap())
            {
                isIgnoringArrivalGateOverlap = false;
                return true;
            }

            if (!IsInTransitionGateArea())
            {
                isIgnoringArrivalGateOverlap = false;
                StorePreviousPassagePosition();
            }

            return false;
        }

        return IsInTransitionGateArea() || DidCrossTransitionGateArea();
    }

    private bool ShouldCompleteFromArrivalGateOverlap()
    {
        if (playerTransform == null || playerController == null)
        {
            return false;
        }

        float requiredDirection = GetTransitionDirection();
        if (playerController.MoveInput.y * requiredDirection >= requiredInputThreshold)
        {
            return true;
        }

        return IsPastTransitionSideOfGate(playerTransform.position);
    }

    private bool IsInTransitionGateArea()
    {
        if (playerTransform == null || transitionGateCollider == null)
        {
            return false;
        }

        return ContainsPoint2D(transitionGateCollider.bounds, playerTransform.position);
    }

    private bool DidCrossTransitionGateArea()
    {
        if (!hasPreviousPassagePosition || playerTransform == null || transitionGateCollider == null)
        {
            return false;
        }

        return SegmentIntersectsBounds2D(transitionGateCollider.bounds, previousPassagePosition, playerTransform.position);
    }

    private bool IsPastTransitionSideOfGate(Vector3 position)
    {
        if (transitionGateCollider == null)
        {
            return false;
        }

        Bounds bounds = transitionGateCollider.bounds;
        float requiredDirection = GetTransitionDirection();
        if (requiredDirection > 0f)
        {
            return position.y > bounds.max.y;
        }

        return position.y < bounds.min.y;
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
            && onAreaCollider != null
            && ContainsPoint2D(onAreaCollider.bounds, playerTransform.position);
    }

    private static bool ContainsPoint2D(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x
            && point.x <= bounds.max.x
            && point.y >= bounds.min.y
            && point.y <= bounds.max.y;
    }

    private void StorePreviousPassagePosition()
    {
        if (playerTransform == null)
        {
            hasPreviousPassagePosition = false;
            previousPassagePosition = Vector3.zero;
            return;
        }

        previousPassagePosition = playerTransform.position;
        hasPreviousPassagePosition = true;
    }

    private static bool SegmentIntersectsBounds2D(Bounds bounds, Vector3 start, Vector3 end)
    {
        float tMin = 0f;
        float tMax = 1f;
        Vector3 delta = end - start;

        return ClipSegmentAxis(-delta.x, start.x - bounds.min.x, ref tMin, ref tMax)
            && ClipSegmentAxis(delta.x, bounds.max.x - start.x, ref tMin, ref tMax)
            && ClipSegmentAxis(-delta.y, start.y - bounds.min.y, ref tMin, ref tMax)
            && ClipSegmentAxis(delta.y, bounds.max.y - start.y, ref tMin, ref tMax);
    }

    private static bool ClipSegmentAxis(float direction, float distance, ref float tMin, ref float tMax)
    {
        if (Mathf.Approximately(direction, 0f))
        {
            return distance >= 0f;
        }

        float t = distance / direction;
        if (direction < 0f)
        {
            if (t > tMax)
            {
                return false;
            }

            if (t > tMin)
            {
                tMin = t;
            }
        }
        else
        {
            if (t < tMin)
            {
                return false;
            }

            if (t < tMax)
            {
                tMax = t;
            }
        }

        return true;
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

        if (movementAreaCollider == null)
        {
            movementAreaCollider = FindChildBoxCollider("PassageMovementArea", "MovementArea", "MoveArea");
        }

        if (transitionAreaCollider == null)
        {
            transitionAreaCollider = FindChildBoxCollider("PassageTransitionArea", "TransitionArea", "TransferArea");
        }

        if (transitionGateCollider == null)
        {
            transitionGateCollider = FindChildBoxCollider("PassageTransitionGate", "TransitionGate", "GateArea");
        }

        WarnIfRequiredAreaIsMissing(onAreaCollider, "PassageOnArea");
        WarnIfRequiredAreaIsMissing(offAreaCollider, "PassageOffArea");
        WarnIfRequiredAreaIsMissing(movementAreaCollider, "PassageMovementArea");
        WarnIfRequiredAreaIsMissing(transitionAreaCollider, "PassageTransitionArea");
        WarnIfRequiredAreaIsMissing(transitionGateCollider, "PassageTransitionGate");
    }

    private void ConfigureAreaTriggers()
    {
        PassageAreaTrigger.Attach(onAreaCollider, this, PassageAreaKind.On);
        PassageAreaTrigger.Attach(offAreaCollider, this, PassageAreaKind.Off);
    }

    private bool HasRequiredAreaColliders()
    {
        return onAreaCollider != null
            && offAreaCollider != null
            && movementAreaCollider != null
            && transitionAreaCollider != null
            && transitionGateCollider != null;
    }

    private void WarnIfRequiredAreaIsMissing(BoxCollider areaCollider, string areaName)
    {
        if (areaCollider == null)
        {
            Debug.LogWarning($"PassageController: Required area collider '{areaName}' was not found.");
        }
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
        isIgnoringArrivalGateOverlap = false;
        hasPreviousPassagePosition = false;
        previousPassagePosition = Vector3.zero;
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
        isIgnoringArrivalGateOverlap = false;
        hasPreviousPassagePosition = false;
        previousPassagePosition = Vector3.zero;
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
