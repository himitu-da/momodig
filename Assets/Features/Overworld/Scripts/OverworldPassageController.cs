using UnityEngine;

public class OverworldPassageController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private float cancelDistance = 0.2f;
    [SerializeField] private string destinationSceneName = "MiningScene";
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, -3f, 0f);

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

        if (HasReachedTransitionTarget())
        {
            CompletePassage();
            return;
        }

        if (HasReturnedPastStart())
        {
            ResetState();
        }
    }

    private void CapturePassageBounds()
    {
        Bounds bounds;
        Collider passageCollider = GetComponent<Collider>();
        if (passageCollider != null)
        {
            bounds = passageCollider.bounds;
        }
        else if (passageMaskRenderer != null)
        {
            bounds = passageMaskRenderer.bounds;
        }
        else
        {
            bounds = new Bounds(transform.position, new Vector3(1f, 1f, 1f));
        }

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
        if (position.x < passageMinX - cancelDistance || position.x > passageMaxX + cancelDistance)
        {
            ResetState();
            return false;
        }

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

        float returnDistance = (position.y - transitionStartY) * transitionDirection;
        if (returnDistance < -cancelDistance)
        {
            ResetState();
            return false;
        }

        bool clampedY = false;
        if (returnDistance < 0f)
        {
            position.y = transitionStartY;
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
            changeScene.OnClickToChangeScene(destinationSceneName);
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

    private bool HasReturnedPastStart()
    {
        return playerTransform != null
            && (playerTransform.position.y - transitionStartY) * transitionDirection < -cancelDistance;
    }

    private void ResetState()
    {
        if (playerController != null)
        {
            playerController.IsMovementLocked = false;
        }

        playerController = null;
        playerTransform = null;
        playerRigidbody = null;
        isPlayerInside = false;
        isPassageActive = false;
        transitionStartY = 0f;
        transitionTargetY = 0f;
        transitionDirection = 0f;
        passageMinX = 0f;
        passageMaxX = 0f;
        RestorePlayerCollision();
        passageMaskSession.End();
    }

    private void ClearState()
    {
        playerController = null;
        playerTransform = null;
        playerRigidbody = null;
        isPlayerInside = false;
        isPassageActive = false;
        transitionStartY = 0f;
        transitionTargetY = 0f;
        transitionDirection = 0f;
        passageMinX = 0f;
        passageMaxX = 0f;
        RestorePlayerCollision();
        passageMaskSession.End();
    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.IsMovementLocked = false;
        }

        RestorePlayerCollision();
        passageMaskSession.End();
    }
}
