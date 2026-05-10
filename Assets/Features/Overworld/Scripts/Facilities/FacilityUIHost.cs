using System;
using UnityEngine;

public class FacilityUIHost : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private OverworldPlayerController playerController;

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
    }

    private void OnDestroy()
    {
        if (currentPanel != null)
        {
            currentPanel.CloseRequested -= HandlePanelCloseRequested;
        }

        RestoreMovementLockState();
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

        panelRoot.gameObject.SetActive(true);
        currentFacility = facility;
        currentPanel = Instantiate(facility.PanelPrefab, panelRoot);
        currentPanel.Initialize(this, facility);
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
        Destroy(currentPanel.gameObject);
        currentPanel = null;
        currentFacility = null;
        panelRoot.gameObject.SetActive(false);
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

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (panelRoot == null)
        {
            Debug.LogError("FacilityUIHost: panelRoot is not configured.", this);
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
