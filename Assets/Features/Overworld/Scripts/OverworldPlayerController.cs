using UnityEngine;
using UnityEngine.InputSystem;

public class OverworldPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 0.05f;
    [SerializeField] private float deceleration = 0.1f;
    [SerializeField] private float fallSpeedMultiplier = 0.5f;
    [SerializeField] private float fallAcceleration = 1f;

    private Rigidbody rb;
    private InputSystem_Actions controls;
    private Vector2 moveInput;
    private Vector3 currentVelocity;
    private float currentFallSpeed;
    private PlayerVisualsController playerVisualsController;

    public Vector2 MoveInput => moveInput;
    public Vector3 LastMoveDirection { get; private set; } = Vector3.right;
    public bool IsFacingRight { get; private set; } = true;
    public bool IsMovementLocked { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerVisualsController = GetComponentInChildren<PlayerVisualsController>();

        if (rb != null)
        {
            rb.useGravity = false;
            UpdateConstraints();
        }

        controls = new InputSystem_Actions();
        controls.Player.Move.performed += OnMovePerformed;
        controls.Player.Move.canceled += OnMoveCanceled;
    }

    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new InputSystem_Actions();
            controls.Player.Move.performed += OnMovePerformed;
            controls.Player.Move.canceled += OnMoveCanceled;
        }

        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls?.Player.Disable();
    }

    private void OnDestroy()
    {
        if (controls == null)
        {
            return;
        }

        controls.Player.Move.performed -= OnMovePerformed;
        controls.Player.Move.canceled -= OnMoveCanceled;
        controls.Dispose();
        controls = null;
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 moveDirection = new Vector3(moveInput.x, moveInput.y, 0f);
        Vector3 targetVelocity = CalculateTargetVelocity(moveDirection);

        if (IsMovementLocked)
        {
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            float smoothTime = moveDirection.sqrMagnitude > 0f ? acceleration : deceleration;
            rb.linearVelocity = Vector3.SmoothDamp(rb.linearVelocity, targetVelocity, ref currentVelocity, smoothTime);
        }

        UpdateFacing(moveDirection);
        UpdateVisuals(moveDirection);
    }

    private Vector3 CalculateTargetVelocity(Vector3 moveDirection)
    {
        if (moveInput == Vector2.zero)
        {
            currentFallSpeed += fallAcceleration * Time.fixedDeltaTime;
            float maxFallSpeed = moveSpeed * fallSpeedMultiplier;
            currentFallSpeed = Mathf.Min(currentFallSpeed, maxFallSpeed);
            return new Vector3(0f, -currentFallSpeed, 0f);
        }

        currentFallSpeed = 0f;

        if (moveInput.x != 0f && moveInput.y == 0f)
        {
            return new Vector3(moveInput.x, 0f, 0f).normalized * moveSpeed;
        }

        return moveDirection.normalized * moveSpeed;
    }

    private void UpdateFacing(Vector3 moveDirection)
    {
        if (moveInput.x != 0f)
        {
            IsFacingRight = moveInput.x > 0f;
        }

        if (moveDirection.sqrMagnitude > 0.1f)
        {
            LastMoveDirection = moveDirection.normalized;
            return;
        }

        LastMoveDirection = new Vector3(IsFacingRight ? 1f : -1f, 0f, 0f);
    }

    private void UpdateVisuals(Vector3 moveDirection)
    {
        if (playerVisualsController != null)
        {
            playerVisualsController.UpdateMovementAnimation(moveDirection);
        }
    }

    private void UpdateConstraints()
    {
        if (rb == null)
        {
            return;
        }

        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }
}
