using System;
using UnityEngine;

public class FacilityUIHost : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup baseUiGroup;
    [SerializeField] private OverworldPlayerController playerController;

    [Header("Runtime Services")]
    [SerializeField] private FacilityUpgradeService facilityUpgradeService;
    [SerializeField] private PlayerInventoryLoadout miningEntranceLoadout;

    [Header("Behavior")]
    [SerializeField] private bool lockPlayerMovementWhileOpen = true;

    private FacilityPanel currentPanel;
    private FacilityDefinition currentFacility;
    private bool hasStoredMovementLockState;
    private bool previousMovementLockState;

    public event Action<FacilityDefinition> PanelOpened;
    public event Action PanelClosed;

    public bool IsOpen => currentPanel != null;
    public FacilityDefinition CurrentFacility => currentFacility;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        panelRoot.gameObject.SetActive(false);
        SetBaseUiVisible(true);
    }

    private void OnDestroy()
    {
        if (currentPanel != null)
        {
            currentPanel.CloseRequested -= HandlePanelCloseRequested;
            currentPanel.NotifyClosing();
        }

        RestoreMovementLockState();

        if (baseUiGroup != null)
        {
            SetBaseUiVisible(true);
        }
    }

    public bool Open(FacilityDefinition facility)
    {
        if (!enabled)
        {
            Debug.LogError("FacilityUIHost: cannot open a facility because the host is disabled.", this);
            return false;
        }

        if (facility == null)
        {
            Debug.LogError("FacilityUIHost: facility is not configured.", this);
            return false;
        }

        if (!facility.ValidateConfiguration(this))
        {
            return false;
        }

        if (IsOpen)
        {
            Debug.LogError($"FacilityUIHost: cannot open '{facility.DisplayName}' because '{currentFacility.DisplayName}' is already open.", this);
            return false;
        }

        StoreMovementLockState();

        SetBaseUiVisible(false);
        panelRoot.gameObject.SetActive(true);
        FacilityPanel createdPanel = Instantiate(facility.PanelPrefab, panelRoot);
        if (!createdPanel.gameObject.activeSelf)
        {
            Debug.LogError($"FacilityUIHost: panel prefab for '{facility.DisplayName}' must have an active root GameObject.", createdPanel);
            Destroy(createdPanel.gameObject);
            panelRoot.gameObject.SetActive(false);
            SetBaseUiVisible(true);
            RestoreMovementLockState();
            return false;
        }

        if (!BindRuntimeServices(createdPanel, facility))
        {
            Destroy(createdPanel.gameObject);
            panelRoot.gameObject.SetActive(false);
            SetBaseUiVisible(true);
            RestoreMovementLockState();
            return false;
        }

        if (!createdPanel.Initialize(this, facility))
        {
            Destroy(createdPanel.gameObject);
            panelRoot.gameObject.SetActive(false);
            SetBaseUiVisible(true);
            RestoreMovementLockState();
            return false;
        }

        currentFacility = facility;
        currentPanel = createdPanel;
        currentPanel.CloseRequested += HandlePanelCloseRequested;

        if (lockPlayerMovementWhileOpen)
        {
            playerController.IsMovementLocked = true;
        }

        PanelOpened?.Invoke(facility);
        return true;
    }

    public void CloseCurrentPanel()
    {
        if (!IsOpen)
        {
            Debug.LogError("FacilityUIHost: CloseCurrentPanel was called, but no panel is open.", this);
            return;
        }

        currentPanel.CloseRequested -= HandlePanelCloseRequested;
        currentPanel.NotifyClosing();
        Destroy(currentPanel.gameObject);
        currentPanel = null;
        currentFacility = null;
        panelRoot.gameObject.SetActive(false);
        SetBaseUiVisible(true);
        RestoreMovementLockState();

        PanelClosed?.Invoke();
    }

    private void HandlePanelCloseRequested(FacilityPanel panel)
    {
        if (panel != currentPanel)
        {
            Debug.LogError("FacilityUIHost: received a close request from a panel that is not currently open.", this);
            return;
        }

        CloseCurrentPanel();
    }

    private bool BindRuntimeServices(FacilityPanel panel, FacilityDefinition facility)
    {
        if (panel is IFacilityPanelRuntimeBinding runtimeBinding)
        {
            if (facilityUpgradeService == null)
            {
                Debug.LogError($"FacilityUIHost: facilityUpgradeService is not configured for '{facility.DisplayName}'.", this);
                return false;
            }

            return runtimeBinding.BindRuntime(facilityUpgradeService);
        }

        if (panel is IMiningEntrancePanelRuntimeBinding miningEntranceBinding)
        {
            if (miningEntranceLoadout == null)
            {
                Debug.LogError($"FacilityUIHost: miningEntranceLoadout is not configured for '{facility.DisplayName}'.", this);
                return false;
            }

            return miningEntranceBinding.BindRuntime(miningEntranceLoadout);
        }

        return true;
    }

    private void StoreMovementLockState()
    {
        if (!lockPlayerMovementWhileOpen)
        {
            return;
        }

        previousMovementLockState = playerController.IsMovementLocked;
        hasStoredMovementLockState = true;
    }

    private void RestoreMovementLockState()
    {
        if (!lockPlayerMovementWhileOpen || !hasStoredMovementLockState || playerController == null)
        {
            return;
        }

        playerController.IsMovementLocked = previousMovementLockState;
        hasStoredMovementLockState = false;
    }

    private void SetBaseUiVisible(bool visible)
    {
        baseUiGroup.alpha = visible ? 1f : 0f;
        baseUiGroup.interactable = visible;
        baseUiGroup.blocksRaycasts = visible;
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (panelRoot == null)
        {
            Debug.LogError("FacilityUIHost: panelRoot is not configured.", this);
            isValid = false;
        }

        if (baseUiGroup == null)
        {
            Debug.LogError("FacilityUIHost: baseUiGroup is not configured.", this);
            isValid = false;
        }

        if (playerController == null)
        {
            Debug.LogError("FacilityUIHost: playerController is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
