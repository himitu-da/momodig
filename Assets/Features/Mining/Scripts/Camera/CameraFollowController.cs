using UnityEngine;

/// <summary>
/// Miningシーン用のカメラ追従・構図制御クラス。
/// SideScrollerモードでは移動方向に応じた回転と位置計算を行う。
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    [Header("オブジェクト参照")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject playerObject;

    [Header("追従設定")]
    [SerializeField] private bool enableFollow = true;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private bool useSmoothDamp = false;

    [Header("モード別オフセット（従来追従/TopDown）")]
    [SerializeField] private Vector3 sideScrollerOffset = new Vector3(0f, 2f, -10f);
    [SerializeField] private Vector3 topDownOffset = new Vector3(0f, 10f, 0f);

    [Header("カメラサイズ設定")]
    [SerializeField] private float sideScrollerOrthographicSize = 5f;
    [SerializeField] private float topDownOrthographicSize = 8f;

    [Header("SideScroller方向連動カメラ")]
    [Tooltip("SideScrollerで方向連動カメラを有効にする")]
    [SerializeField] private bool useDirectionalCameraInSideScroller = true;

    [Tooltip("回転後のプレイヤー基準位置（速度0のとき）")]
    [SerializeField] private Vector2 playerBaseViewportOffset = Vector2.zero;

    [Tooltip("移動方向の反対側へ最大でどれだけずらすか（Viewport座標）")]
    [SerializeField, Range(0f, 0.45f)] private float maxPlayerViewportOffset = 0.18f;

    [Tooltip("この速度で速度係数が1.0になる")]
    [SerializeField] private float speedForMaxCameraResponse = 5f;

    [Tooltip("速度係数の対数カーブ強度（大きいほど低速から反応）")]
    [SerializeField] private float speedLogResponseScale = 1.25f;

    [Tooltip("この速度未満では速度係数を0として扱う")]
    [SerializeField] private float minimumSpeedForCameraResponse = 0.05f;

    [Tooltip("速度が落ちた後も移動ベクトルを維持する秒数")]
    [SerializeField, Min(0f)] private float moveVectorHoldDurationSeconds = 0.2f;

    [Tooltip("プレイヤーからカメラまでの距離")]
    [SerializeField] private float cameraDistanceFromPlayer = 10f;

    [Header("SideScroller回転設定")]
    [Tooltip("基準ピッチ角（通常時）")]
    [SerializeField] private float basePitchAngle = 10f;

    [Tooltip("基準ヨー角（通常時）")]
    [SerializeField] private float baseYawAngle = 0f;

    [Tooltip("上下移動で追加する最大ピッチ角")]
    [SerializeField] private float maxPitchAngleOffset = 8f;

    [Tooltip("左右移動で追加する最大ヨー角")]
    [SerializeField] private float maxYawAngleOffset = 12f;

    [Tooltip("回転追従の補間速度")]
    [SerializeField] private float rotationSmoothingSpeed = 8f;

    private PlayerController playerController;
    private Rigidbody playerRigidbody;
    private Vector3 followVelocity = Vector3.zero;
    private Quaternion initialCameraRotation = Quaternion.identity;
    private PlayerController.MoveMode currentMode;
    private PlayerController.MoveMode lastMode;
    private Vector2 heldPlanarMoveVector = Vector2.zero;
    private float remainingMoveVectorHoldTime = 0f;

    private void Start()
    {
        ValidateReferences();
        if (mainCamera != null)
        {
            initialCameraRotation = mainCamera.transform.rotation;
        }

        if (playerObject != null)
        {
            playerController = playerObject.GetComponent<PlayerController>();
            playerRigidbody = playerObject.GetComponent<Rigidbody>();
            if (playerController != null)
            {
                currentMode = playerController.currentMoveMode;
                lastMode = currentMode;
                UpdateCameraSettings();
            }
        }

        if (mainCamera == null)
            Debug.LogWarning("CameraFollowController: MainCamera reference is missing.");
        if (playerObject == null)
            Debug.LogWarning("CameraFollowController: Player Object reference is missing.");
        if (playerController == null && playerObject != null)
            Debug.LogWarning("CameraFollowController: PlayerController component was not found on Player Object.");
        if (playerRigidbody == null && playerObject != null)
            Debug.LogWarning("CameraFollowController: Rigidbody component was not found on Player Object.");
    }

    /// <summary>
    /// プレイヤーの物理更新と同期するため、物理タイミングで実行する。
    /// </summary>
    private void FixedUpdate()
    {
        if (!enableFollow || mainCamera == null || playerObject == null || playerController == null)
            return;

        currentMode = playerController.currentMoveMode;
        if (currentMode != lastMode)
        {
            lastMode = currentMode;
            OnMoveModeChanged();
        }

        if (currentMode == PlayerController.MoveMode.SideScroller && useDirectionalCameraInSideScroller)
        {
            UpdateSideScrollerDirectionalCamera();
            return;
        }

        UpdateCameraPositionLegacy();
    }

    /// <summary>
    /// 参照の妥当性を確認し、未設定時に警告を出す。
    /// </summary>
    private void ValidateReferences()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("CameraFollowController: MainCamera reference is not set. Please assign it in the Inspector.");
        }

        if (playerObject == null)
        {
            Debug.LogWarning("CameraFollowController: Player Object reference is not set. Please assign it in the Inspector.");
        }
    }

    /// <summary>
    /// TopDown時、または方向連動カメラが無効なときの従来追従処理。
    /// </summary>
    private void UpdateCameraPositionLegacy()
    {
        Vector3 targetOffset = GetCurrentOffset();
        Vector3 targetPosition = playerObject.transform.position + targetOffset;
        Vector3 currentPos = mainCamera.transform.position;
        Vector3 newPosition = currentPos;

        switch (currentMode)
        {
            case PlayerController.MoveMode.SideScroller:
                newPosition.x = targetPosition.x;
                newPosition.y = targetPosition.y;
                newPosition.z = targetPosition.z;
                break;

            case PlayerController.MoveMode.TopDown:
                newPosition.x = targetPosition.x;
                newPosition.y = targetPosition.y;
                newPosition.z = targetPosition.z;
                break;
        }

        ApplyPosition(newPosition);
    }

    /// <summary>
    /// SideScroller向けカメラ制御。
    /// 1) 移動方向から回転を決定（移動方向の逆向き）
    /// 2) 指定Viewport座標にプレイヤーが来るようカメラ位置を逆算
    /// </summary>
    private void UpdateSideScrollerDirectionalCamera()
    {
        Vector3 playerPosition = playerObject.transform.position;
        Vector2 rawPlanarVelocity = GetSideScrollerPlanarVelocity();
        Vector2 planarVelocity = GetHeldSideScrollerPlanarVelocity(rawPlanarVelocity);
        float speed = planarVelocity.magnitude;

        Vector2 moveDirection = speed > minimumSpeedForCameraResponse ? planarVelocity / speed : Vector2.zero;
        float speedInfluence = CalculateLogSpeedInfluence(speed);

        // 進行方向を見やすくするため、画面上のプレイヤー位置を移動方向の反対側へ寄せる。
        Vector2 targetPlayerViewportOffset = CalculateTargetPlayerViewportOffset(moveDirection, speedInfluence);

        // カメラは移動方向の逆向きへ回転させる。
        float targetPitch = basePitchAngle - (moveDirection.y * maxPitchAngleOffset * speedInfluence);
        float targetYaw = baseYawAngle + (moveDirection.x * maxYawAngleOffset * speedInfluence);
        Quaternion targetRotation = BuildHorizonLeveledRotation(targetPitch, targetYaw);

        float distanceFromPlayer = ResolveCameraDistance();
        Vector3 localPlayerOffset = BuildLocalPlayerOffset(targetPlayerViewportOffset, distanceFromPlayer);
        Vector3 targetPosition = playerPosition - (targetRotation * localPlayerOffset);

        ApplyPosition(targetPosition);
        ApplyRotation(targetRotation);
    }

    private Vector2 GetSideScrollerPlanarVelocity()
    {
        if (playerRigidbody == null)
        {
            return Vector2.zero;
        }

        Vector3 velocity = playerRigidbody.linearVelocity;
        return new Vector2(velocity.x, velocity.y);
    }

    private Vector2 GetHeldSideScrollerPlanarVelocity(Vector2 rawPlanarVelocity)
    {
        float rawSpeed = rawPlanarVelocity.magnitude;
        if (rawSpeed > minimumSpeedForCameraResponse)
        {
            heldPlanarMoveVector = rawPlanarVelocity;
            remainingMoveVectorHoldTime = moveVectorHoldDurationSeconds;
            return rawPlanarVelocity;
        }

        if (remainingMoveVectorHoldTime > 0f && heldPlanarMoveVector.sqrMagnitude > Mathf.Epsilon)
        {
            remainingMoveVectorHoldTime = Mathf.Max(0f, remainingMoveVectorHoldTime - Time.fixedDeltaTime);
            return heldPlanarMoveVector;
        }

        heldPlanarMoveVector = Vector2.zero;
        return Vector2.zero;
    }

    private float CalculateLogSpeedInfluence(float speed)
    {
        if (speed <= minimumSpeedForCameraResponse)
        {
            return 0f;
        }

        float safeCurveScale = Mathf.Max(0.0001f, speedLogResponseScale);
        float safeReferenceSpeed = Mathf.Max(minimumSpeedForCameraResponse, speedForMaxCameraResponse);
        float denominator = Mathf.Log(1f + safeReferenceSpeed * safeCurveScale);
        if (denominator <= Mathf.Epsilon)
        {
            return 0f;
        }

        float numerator = Mathf.Log(1f + speed * safeCurveScale);
        return Mathf.Clamp01(numerator / denominator);
    }

    private Vector2 CalculateTargetPlayerViewportOffset(Vector2 moveDirection, float speedInfluence)
    {
        // 目標プレイヤー位置（回転後）:
        // base - moveDir * (maxOffset * speedInfluence)
        return playerBaseViewportOffset - moveDirection * (maxPlayerViewportOffset * speedInfluence);
    }

    private Quaternion BuildHorizonLeveledRotation(float pitch, float yaw)
    {
        Quaternion rawRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = rawRotation * Vector3.forward;
        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return rawRotation;
        }

        // World Upを基準に回転を再構築し、地平線のロール傾きを補正する。
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private float ResolveCameraDistance()
    {
        if (cameraDistanceFromPlayer > 0.01f)
        {
            return cameraDistanceFromPlayer;
        }

        float fromOffset = Mathf.Abs(sideScrollerOffset.z);
        if (fromOffset > 0.01f)
        {
            return fromOffset;
        }

        return 10f;
    }

    private Vector3 BuildLocalPlayerOffset(Vector2 viewportOffset, float distance)
    {
        float worldHeight;
        if (mainCamera.orthographic)
        {
            worldHeight = mainCamera.orthographicSize * 2f;
        }
        else
        {
            float halfFovRad = Mathf.Deg2Rad * mainCamera.fieldOfView * 0.5f;
            worldHeight = 2f * Mathf.Tan(halfFovRad) * distance;
        }

        float worldWidth = worldHeight * mainCamera.aspect;
        float localX = viewportOffset.x * worldWidth;
        float localY = viewportOffset.y * worldHeight;
        return new Vector3(localX, localY, distance);
    }

    private void ApplyPosition(Vector3 targetPosition)
    {
        if (useSmoothDamp)
        {
            mainCamera.transform.position = Vector3.SmoothDamp(
                mainCamera.transform.position,
                targetPosition,
                ref followVelocity,
                1f / Mathf.Max(0.1f, followSpeed)
            );
            return;
        }

        float t = Mathf.Clamp01(followSpeed * Time.fixedDeltaTime);
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, t);
    }

    private void ApplyRotation(Quaternion targetRotation)
    {
        float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, rotationSmoothingSpeed) * Time.fixedDeltaTime);
        mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetRotation, t);
    }

    private Vector3 GetCurrentOffset()
    {
        return currentMode switch
        {
            PlayerController.MoveMode.SideScroller => sideScrollerOffset,
            PlayerController.MoveMode.TopDown => topDownOffset,
            _ => Vector3.zero
        };
    }

    private void OnMoveModeChanged()
    {
        Debug.Log($"CameraFollowController: Move mode changed to {currentMode}");
        followVelocity = Vector3.zero;
        heldPlanarMoveVector = Vector2.zero;
        remainingMoveVectorHoldTime = 0f;
        UpdateCameraSettings();
    }

    private void UpdateCameraSettings()
    {
        if (mainCamera == null) return;

        float targetSize = currentMode switch
        {
            PlayerController.MoveMode.SideScroller => sideScrollerOrthographicSize,
            PlayerController.MoveMode.TopDown => topDownOrthographicSize,
            _ => 5f
        };

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = targetSize;
        }

        if (currentMode == PlayerController.MoveMode.SideScroller && useDirectionalCameraInSideScroller)
        {
            mainCamera.transform.rotation = BuildHorizonLeveledRotation(basePitchAngle, baseYawAngle);
            return;
        }

        mainCamera.transform.rotation = initialCameraRotation;
    }

    /// <summary>
    /// 追従機能の有効/無効を切り替える。
    /// </summary>
    public void SetFollowEnabled(bool enabled)
    {
        enableFollow = enabled;
    }

    /// <summary>
    /// カメラ追従速度を設定する。
    /// </summary>
    public void SetFollowSpeed(float speed)
    {
        followSpeed = Mathf.Max(0.1f, speed);
    }

    /// <summary>
    /// 従来追従で使用するモード別オフセットを設定する。
    /// </summary>
    public void SetOffset(PlayerController.MoveMode mode, Vector3 offset)
    {
        switch (mode)
        {
            case PlayerController.MoveMode.SideScroller:
                sideScrollerOffset = offset;
                break;
            case PlayerController.MoveMode.TopDown:
                topDownOffset = offset;
                break;
        }
    }
}
