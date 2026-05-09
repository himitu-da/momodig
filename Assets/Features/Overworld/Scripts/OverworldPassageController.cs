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
    private bool isPlayerInside;
    private bool isPassageActive;
    private float transitionStartY;
    private float transitionTargetY;
    private float transitionDirection;
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
        isPlayerInside = false;
        isPassageActive = false;
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
        transitionStartY = 0f;
        transitionTargetY = 0f;
        transitionDirection = 0f;
        passageMaskSession.End();
    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.IsMovementLocked = false;
        }

        passageMaskSession.End();
    }
}
