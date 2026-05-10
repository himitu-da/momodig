using System;
using UnityEngine;
using UnityEngine.UI;

public class FacilityPanel : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Button closeButton;

    public event Action<FacilityPanel> CloseRequested;

    public FacilityDefinition Facility { get; private set; }
    public FacilityUIHost Owner { get; private set; }
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
        }
    }

    public bool Initialize(FacilityUIHost owner, FacilityDefinition facility)
    {
        if (!enabled)
        {
            Debug.LogError($"FacilityPanel '{name}': cannot initialize because the panel component is disabled.", this);
            return false;
        }

        if (IsInitialized)
        {
            Debug.LogError($"FacilityPanel '{name}': Initialize was called more than once.", this);
            return false;
        }

        if (owner == null)
        {
            Debug.LogError($"FacilityPanel '{name}': owner is not configured.", this);
            return false;
        }

        if (facility == null)
        {
            Debug.LogError($"FacilityPanel '{name}': facility is not configured.", this);
            return false;
        }

        if (!facility.ValidateConfiguration(this))
        {
            return false;
        }

        if (!ValidatePanelConfiguration())
        {
            return false;
        }

        Owner = owner;
        Facility = facility;
        IsInitialized = true;
        OnInitialized();
        return true;
    }

    public void RequestClose()
    {
        HandleCloseClicked();
    }

    public void NotifyClosing()
    {
        OnClosing();
    }

    private void HandleCloseClicked()
    {
        if (!IsInitialized)
        {
            Debug.LogError($"FacilityPanel '{name}': Close button was clicked before Initialize.", this);
            return;
        }

        if (CloseRequested == null)
        {
            Debug.LogError($"FacilityPanel '{name}': no close receiver is registered.", this);
            return;
        }

        CloseRequested.Invoke(this);
    }

    protected virtual bool ValidatePanelConfiguration()
    {
        return true;
    }

    protected virtual void OnInitialized()
    {
    }

    protected virtual void OnClosing()
    {
    }

    private bool ValidateRequiredReferences()
    {
        if (closeButton != null)
        {
            return true;
        }

        Debug.LogError($"FacilityPanel '{name}': closeButton is not configured.", this);
        return false;
    }
}
