using UnityEngine;

public class OverworldPassageController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private string destinationSceneName = "MiningScene";
    [SerializeField] private string destinationEntryPointId;
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, -3f, 0f);

    [Header("Passage Areas")]
    [SerializeField] private BoxCollider onAreaCollider;
    [SerializeField] private BoxCollider offAreaCollider;

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
    private bool isPlayerInside;
    private bool isPassageActive;
    private float transitionStartY;
    private float transitionTargetY;
    private float transitionDirection;
    private float passageMinX;
    private float passageMaxX;
    private bool isSceneTransitioning;
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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || playerController != null)
        {
            return;
        }

        playerController = other.GetComponentInParent<OverworldPlayerController>();
        if (playerController == null)
        {
            return;
        }

        isPlayerInside = true;
        playerTransform = playerController.transform;
        playerRigidbody = playerController.GetComponent<Rigidbody>();
        CapturePassageBounds();
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
        if (playerTransform == null)
        {
            return;
        }

        isPassageActive = true;
        transitionDirection = requiredDirection;
        transitionStartY = playerTransform.position.y;
        transitionTargetY = transform.position.y + travelOffset.y;
        CapturePassageBounds();
        DisablePlayerCollision();

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

        if (!ConstrainPassageMovement())
        {
            return;
        }

        if (IsInOffArea())
        {
            DeactivatePassage();
            return;
        }

        if (HasReachedTransitionTarget())
        {
            CompletePassage();
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

        for (int i = 0; i < playerCollisionColliders.Length; i++)
        {
            Collider playerCollider = playerCollisionColliders[i];
            if (playerCollider == null)
            {
                continue;
            }

            playerCollisionColliderEnabledStates[i] = playerCollider.enabled;
            if (!playerCollider.isTrigger)
            {
                playerCollider.enabled = false;
            }
        }
    }

    private void RestorePlayerCollision()
    {
        if (playerCollisionColliders != null && playerCollisionColliderEnabledStates != null)
        {
            int count = Mathf.Min(playerCollisionColliders.Length, playerCollisionColliderEnabledStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (playerCollisionColliders[i] != null)
                {
                    playerCollisionColliders[i].enabled = playerCollisionColliderEnabledStates[i];
                }
            }
        }

        playerCollisionColliders = null;
        playerCollisionColliderEnabledStates = null;
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
            changeScene.OnClickToChangeScene(destinationSceneName, destinationEntryPointId);
        }
        else
        {
            Debug.LogWarning("OverworldPassageController: ChangeScene or destination scene is not configured.");
        }

        enabled = false;
    }

    private float GetTransitionDirection()
    {
        if (Mathf.Approximately(travelOffset.y, 0f))
        {
            return -1f;
        }

        return Mathf.Sign(travelOffset.y);
    }

    private bool HasReachedTransitionTarget()
    {
        return playerTransform != null
            && (playerTransform.position.y - transitionTargetY) * transitionDirection >= 0f;
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
