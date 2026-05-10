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

    public void Initialize(FacilityUIHost owner, FacilityDefinition facility)
    {
        if (IsInitialized)
        {
            Debug.LogError($"FacilityPanel '{name}': Initialize was called more than once.", this);
            return;
        }

        if (owner == null)
        {
            Debug.LogError($"FacilityPanel '{name}': owner is not configured.", this);
            return;
        }

        if (facility == null)
        {
            Debug.LogError($"FacilityPanel '{name}': facility is not configured.", this);
            return;
        }

        Owner = owner;
        Facility = facility;
        IsInitialized = true;
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
