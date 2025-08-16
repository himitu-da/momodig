using UnityEngine;

/// <summary>
/// MainCameraをPlayerに追従させるシンプルなコントローラー
/// PlayerControllerの移動モードに応じて適切な平面軸でカメラを追従させる
/// FixedUpdate()を使用してプレイヤーの物理演算と同期し、カメラのガタガタを防ぐ
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    [Header("オブジェクト参照")]
    [SerializeField] private Camera mainCamera;       // MainCameraの参照
    [SerializeField] private GameObject playerObject; // PlayerのGameObject

    [Header("追従設定")]
    [SerializeField] private bool enableFollow = true;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private bool useSmoothDamp = false;

    [Header("移動モード別オフセット")]
    [SerializeField] private Vector3 sideScrollerOffset = new Vector3(0, 2, -10);
    [SerializeField] private Vector3 topDownOffset = new Vector3(0, 10, 0);

    [Header("カメラ設定")]
    [SerializeField] private float sideScrollerOrthographicSize = 5f;
    [SerializeField] private float topDownOrthographicSize = 8f;

    private PlayerController playerController;
    private Vector3 velocity = Vector3.zero; // SmoothDamp用
    private PlayerController.MoveMode currentMode;
    private PlayerController.MoveMode lastMode;

    void Start()
    {
        ValidateReferences();
        
        if (playerObject != null)
        {
            playerController = playerObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                currentMode = playerController.currentMoveMode;
                lastMode = currentMode;
                UpdateCameraSettings();
            }
        }

        // 初期警告
        if (mainCamera == null)
            Debug.LogWarning("CameraFollowController: MainCamera参照が設定されていません！");
        if (playerObject == null)
            Debug.LogWarning("CameraFollowController: Player Object参照が設定されていません！");
        if (playerController == null && playerObject != null)
            Debug.LogWarning("CameraFollowController: PlayerオブジェクトにPlayerControllerコンポーネントが見つかりません！");
    }

    /// <summary>
    /// 物理演算のタイミングで実行される更新処理
    /// PlayerControllerのFixedUpdate()と同期してカメラのガタガタを防ぐ
    /// </summary>
    void FixedUpdate()
    {
        if (!enableFollow || mainCamera == null || playerObject == null || playerController == null) 
            return;

        // 移動モード変更の検出
        currentMode = playerController.currentMoveMode;
        if (currentMode != lastMode)
        {
            lastMode = currentMode;
            OnMoveModeChanged();
        }

        UpdateCameraPosition();
    }

    /// <summary>
    /// 参照の妥当性をチェック（nullの場合は警告のみ）
    /// </summary>
    private void ValidateReferences()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("CameraFollowController: MainCamera参照が未設定です。インスペクターで設定してください。");
        }

        if (playerObject == null)
        {
            Debug.LogWarning("CameraFollowController: Player Object参照が未設定です。インスペクターで設定してください。");
        }
    }

    /// <summary>
    /// 移動モードに応じてカメラ位置を更新
    /// </summary>
    private void UpdateCameraPosition()
    {
        Vector3 targetOffset = GetCurrentOffset();
        Vector3 targetPosition = playerObject.transform.position + targetOffset;

        // 移動モードに応じて特定の軸のみを追従
        Vector3 currentPos = mainCamera.transform.position;
        Vector3 newPosition = currentPos;

        switch (currentMode)
        {
            case PlayerController.MoveMode.SideScroller:
                // SideScrollerモード: XY平面で追従、Z軸は固定オフセット
                newPosition.x = targetPosition.x;
                newPosition.y = targetPosition.y;
                newPosition.z = targetPosition.z; // オフセット込みのZ位置
                break;

            case PlayerController.MoveMode.TopDown:
                // TopDownモード: XZ平面で追従、Y軸は固定オフセット
                newPosition.x = targetPosition.x;
                newPosition.y = targetPosition.y; // オフセット込みのY位置（上空から）
                newPosition.z = targetPosition.z;
                break;
        }

        // スムーシングを適用
        if (useSmoothDamp)
        {
            mainCamera.transform.position = Vector3.SmoothDamp(
                mainCamera.transform.position,
                newPosition,
                ref velocity,
                1f / followSpeed
            );
        }
        else
        {
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                newPosition,
                followSpeed * Time.fixedDeltaTime
            );
        }
    }

    /// <summary>
    /// 現在の移動モードに応じたオフセットを取得
    /// </summary>
    private Vector3 GetCurrentOffset()
    {
        return currentMode switch
        {
            PlayerController.MoveMode.SideScroller => sideScrollerOffset,
            PlayerController.MoveMode.TopDown => topDownOffset,
            _ => Vector3.zero
        };
    }

    /// <summary>
    /// 移動モードが変更されたときの処理
    /// </summary>
    private void OnMoveModeChanged()
    {
        Debug.Log($"CameraFollowController: Move mode changed to {currentMode}");
        UpdateCameraSettings();
    }

    /// <summary>
    /// 移動モードに応じてカメラ設定を更新
    /// </summary>
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
    }

    /// <summary>
    /// 追従機能のオン/オフを切り替え
    /// </summary>
    public void SetFollowEnabled(bool enabled)
    {
        enableFollow = enabled;
    }

    /// <summary>
    /// 追従速度を変更
    /// </summary>
    public void SetFollowSpeed(float speed)
    {
        followSpeed = Mathf.Max(0.1f, speed);
    }

    /// <summary>
    /// オフセットを動的に変更
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

#if UNITY_EDITOR
    /// <summary>
    /// エディタ上でGizmosを描画してオフセットを可視化
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (playerObject == null) return;

        Gizmos.color = Color.yellow;
        Vector3 playerPos = playerObject.transform.position;

        // SideScrollerオフセット
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(playerPos + sideScrollerOffset, 0.5f);
        Gizmos.DrawLine(playerPos, playerPos + sideScrollerOffset);

        // TopDownオフセット
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerPos + topDownOffset, 0.5f);
        Gizmos.DrawLine(playerPos, playerPos + topDownOffset);
    }
#endif
}
