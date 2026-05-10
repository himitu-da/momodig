using UnityEngine;

public class OverworldPassageController : MonoBehaviour, IGameSceneTransitionHandler, IPassageAreaTriggerReceiver
{
    [Header("Transition")]
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private string destinationSceneName = "MiningScene";
    [SerializeField] private string destinationEntryPointId;
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, -3f, 0f);
    [SerializeField] private bool useLinkedDestinationPosition = true;
    [SerializeField] private float destinationPlayerY = 5f;
    [SerializeField] private bool continuePassageAfterSceneTransition = true;

    [Header("Passage Areas")]
    [SerializeField] private BoxCollider onAreaCollider;
    [SerializeField] private BoxCollider offAreaCollider;
    [SerializeField] private BoxCollider movementAreaCollider;
    [SerializeField] private BoxCollider transitionAreaCollider;

    [Header("Passage Stencil Mask")]
    [SerializeField] private bool maskPlayerWithPassageRenderer = true;
    [SerializeField] private MeshRenderer passageMaskRenderer;
    [SerializeField] private Material stencilMaskWriterMaterial;
    [SerializeField] private Material maskedPlayerMaterial;

    private OverworldPlayerController playerController;
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private Collider[] playerCollisionColliders;
    private bool[] playerCollisionColliderEnabledStates;
    private bool[] playerCollisionColliderTriggerStates;
    private bool isPlayerInside;
    private bool isPassageActive;
    private float passageMinX;
    private float passageMaxX;
    private bool isSceneTransitioning;
    private bool isContinuingFromSceneTransition;
    private readonly PassageStencilMaskSession passageMaskSession = new PassageStencilMaskSession();

    private void Awake()
    {
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
        OverworldPlayerController enteringPlayerController = other.GetComponentInParent<OverworldPlayerController>();
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
            && other.GetComponentInParent<OverworldPlayerController>() == playerController;
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

    private void StartPassage()
    {
        if (playerTransform == null || !HasRequiredAreaColliders())
        {
            return;
        }

        isPassageActive = true;
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
            Debug.LogWarning("OverworldPassageController: ChangeScene or destination scene is not configured.");
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
            Debug.LogWarning($"OverworldPassageController: Player tagged 'Player' was not found in scene '{gameObject.scene.name}'.");
            return;
        }

        playerController = player.GetComponentInParent<OverworldPlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("OverworldPassageController: OverworldPlayerController was not found on the linked player.");
            return;
        }

        isPlayerInside = true;
        playerTransform = playerController.transform;
        playerRigidbody = playerController.GetComponent<Rigidbody>();
        isContinuingFromSceneTransition = true;
        isSceneTransitioning = false;

        StartPassage();
    }

    private float GetTransitionDirection()
    {
        if (Mathf.Approximately(travelOffset.y, 0f))
        {
            return -1f;
        }

        return Mathf.Sign(travelOffset.y);
    }

    private bool ShouldCompletePassage()
    {
        return !isContinuingFromSceneTransition
            && IsInTransitionArea();
    }

    private bool IsInTransitionArea()
    {
        if (playerTransform == null || transitionAreaCollider == null)
        {
            return false;
        }

        return ContainsPoint2D(transitionAreaCollider.bounds, playerTransform.position);
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

        WarnIfRequiredAreaIsMissing(onAreaCollider, "PassageOnArea");
        WarnIfRequiredAreaIsMissing(offAreaCollider, "PassageOffArea");
        WarnIfRequiredAreaIsMissing(movementAreaCollider, "PassageMovementArea");
        WarnIfRequiredAreaIsMissing(transitionAreaCollider, "PassageTransitionArea");
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
            && transitionAreaCollider != null;
    }

    private void WarnIfRequiredAreaIsMissing(BoxCollider areaCollider, string areaName)
    {
        if (areaCollider == null)
        {
            Debug.LogWarning($"OverworldPassageController: Required area collider '{areaName}' was not found.");
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
            playerController.IsMovementLocked = false;
        }

        isPassageActive = false;
        isContinuingFromSceneTransition = false;
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
        isContinuingFromSceneTransition = false;
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
}
