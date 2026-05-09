using UnityEngine;

public class OverworldPassageController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private float transitionSpeed = 0.5f;
    [SerializeField] private float requiredInputThreshold = 0.5f;
    [SerializeField] private string destinationSceneName = "MiningScene";
    [SerializeField] private ChangeScene changeScene;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, -3f, 0f);
    [SerializeField] private Vector3 facingDirectionDuringTransition = Vector3.up;

    private OverworldPlayerController playerController;
    private PlayerVisualsController playerVisualsController;
    private Transform playerTransform;
    private Collider playerCollider;
    private Vector3 initialPlayerPosition;
    private Vector3 initialPlayerScale;
    private float entryProgress;
    private bool isPlayerInside;

    private void Awake()
    {
        if (changeScene == null)
        {
            changeScene = FindFirstObjectByType<ChangeScene>();
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

        if (!playerController.IsMovementLocked)
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

        if (!playerController.IsMovementLocked)
        {
            if (verticalInput >= -requiredInputThreshold)
            {
                return;
            }

            StartPassage();
        }

        entryProgress += -verticalInput * transitionSpeed * Time.deltaTime;
        entryProgress = Mathf.Clamp01(entryProgress);

        UpdateVisuals();

        if (verticalInput > 0f && Mathf.Approximately(entryProgress, 0f))
        {
            ResetState();
            return;
        }

        if (entryProgress >= 1f)
        {
            CompletePassage();
        }
    }

    private void StartPassage()
    {
        playerController.IsMovementLocked = true;

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

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
    }

    private void ResetState()
    {
        if (playerController != null)
        {
            playerController.IsMovementLocked = false;
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
    }

    private void ClearState()
    {
        playerController = null;
        playerVisualsController = null;
        playerTransform = null;
        playerCollider = null;
        entryProgress = 0f;
        isPlayerInside = false;
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
}
