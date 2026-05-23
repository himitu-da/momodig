using UnityEngine;

public class TorchToolBehaviour : MiningToolBehaviour
{
    [SerializeField, Min(0f)] private float subUseDisplayDuration = 0.15f;
    [SerializeField] private Material cursorMaterial;
    [SerializeField, Min(0.001f)] private float cursorLineWidth = 0.03f;
    [SerializeField] private float cursorZOffset = -0.01f;

    private TorchPlacementManager torchPlacementManager;
    private PlayerController equippedPlayerController;
    private LineRenderer cursorRenderer;
    private GameObject cursorObject;
    private bool cursorConfigurationErrorLogged;

    public override void SetTorchPlacementManager(TorchPlacementManager torchPlacementManager)
    {
        this.torchPlacementManager = torchPlacementManager;
    }

    public override void OnEquip(GameObject user)
    {
        base.OnEquip(user);
        equippedPlayerController = user != null ? user.GetComponentInParent<PlayerController>() : null;
        if (equippedPlayerController == null)
        {
            Debug.LogError("TorchToolBehaviour: PlayerController is not configured for cursor preview.", this);
        }
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        equippedPlayerController = null;
        SetCursorVisible(false);
    }

    public override void Use(Vector3 direction, PlayerController playerController)
    {
        if (!IsEquipped)
        {
            return;
        }

        if (torchPlacementManager == null)
        {
            Debug.LogError("TorchToolBehaviour: TorchPlacementManager is not configured.", this);
            return;
        }

        if (playerController == null)
        {
            Debug.LogError("TorchToolBehaviour: PlayerController is not configured.", this);
            return;
        }

        BeginUseDisplay();
        CancelInvoke(nameof(EndSubUseDisplay));
        Invoke(nameof(EndSubUseDisplay), subUseDisplayDuration);

        Vector3 targetPosition = playerController.GetMouseWorldPosition(10f);
        if (!torchPlacementManager.ToggleTorchAtWorldPosition(targetPosition))
        {
            Debug.LogWarning($"TorchToolBehaviour: failed to toggle torch at worldPosition={targetPosition}.", this);
        }
    }

    protected override void RenderForDirection(Vector3 direction)
    {
    }

    private void Update()
    {
        if (!IsEquipped)
        {
            SetCursorVisible(false);
            return;
        }

        if (!EnsureCursorRenderer())
        {
            SetCursorVisible(false);
            return;
        }

        if (equippedPlayerController == null)
        {
            SetCursorVisible(false);
            return;
        }

        if (torchPlacementManager == null)
        {
            if (!cursorConfigurationErrorLogged)
            {
                Debug.LogError("TorchToolBehaviour: TorchPlacementManager is not configured for cursor preview.", this);
                cursorConfigurationErrorLogged = true;
            }

            SetCursorVisible(false);
            return;
        }

        Vector3 mouseWorldPosition = equippedPlayerController.GetMouseWorldPosition(10f);
        if (!torchPlacementManager.TryGetPlaceableCursorSquareAtWorldPosition(
                mouseWorldPosition,
                out Vector3 cursorCenter,
                out float cursorSize))
        {
            SetCursorVisible(false);
            return;
        }

        cursorCenter.z += cursorZOffset;
        UpdateCursorShape(cursorCenter, cursorSize);
        SetCursorVisible(true);
    }

    private void EndSubUseDisplay()
    {
        EndUseDisplay();
    }

    private void OnDisable()
    {
        SetCursorVisible(false);
    }

    private bool EnsureCursorRenderer()
    {
        if (cursorRenderer != null)
        {
            return true;
        }

        if (cursorMaterial == null)
        {
            if (!cursorConfigurationErrorLogged)
            {
                Debug.LogError("TorchToolBehaviour: cursorMaterial is not configured.", this);
                cursorConfigurationErrorLogged = true;
            }

            return false;
        }

        cursorObject = new GameObject("TorchPlacementCursor");
        cursorRenderer = cursorObject.AddComponent<LineRenderer>();
        cursorRenderer.sharedMaterial = cursorMaterial;
        cursorRenderer.useWorldSpace = true;
        cursorRenderer.loop = true;
        cursorRenderer.positionCount = 4;
        cursorRenderer.widthMultiplier = cursorLineWidth;
        cursorRenderer.numCornerVertices = 0;
        cursorRenderer.numCapVertices = 0;
        cursorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cursorRenderer.receiveShadows = false;
        cursorRenderer.enabled = false;
        return true;
    }

    private void UpdateCursorShape(Vector3 center, float size)
    {
        float half = size * 0.5f;
        cursorRenderer.SetPosition(0, center + new Vector3(-half, -half, 0f));
        cursorRenderer.SetPosition(1, center + new Vector3(half, -half, 0f));
        cursorRenderer.SetPosition(2, center + new Vector3(half, half, 0f));
        cursorRenderer.SetPosition(3, center + new Vector3(-half, half, 0f));
    }

    private void SetCursorVisible(bool visible)
    {
        if (cursorRenderer != null)
        {
            cursorRenderer.enabled = visible;
        }
    }

    private void OnDestroy()
    {
        if (cursorObject != null)
        {
            Destroy(cursorObject);
        }
    }
}
