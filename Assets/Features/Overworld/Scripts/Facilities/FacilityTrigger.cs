using System.Collections.Generic;
using UnityEngine;

public class FacilityTrigger : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private FacilityDefinition facility;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private FacilityPromptView promptView;
    [SerializeField] private FacilityUIHost facilityUIHost;

    private readonly HashSet<Collider> playerContacts = new HashSet<Collider>();

    public FacilityDefinition Facility => facility;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        if (!promptView.Bind(facility, HandlePromptOpenRequested))
        {
            enabled = false;
            return;
        }

        promptView.Hide();
        facilityUIHost.PanelOpened += HandlePanelOpened;
        facilityUIHost.PanelClosed += HandlePanelClosed;
    }

    private void OnDisable()
    {
        playerContacts.Clear();

        if (promptView != null)
        {
            promptView.Hide();
        }
    }

    private void OnDestroy()
    {
        if (facilityUIHost != null)
        {
            facilityUIHost.PanelOpened -= HandlePanelOpened;
            facilityUIHost.PanelClosed -= HandlePanelClosed;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !IsPlayerCollider(other))
        {
            return;
        }

        bool wasOutside = playerContacts.Count == 0;
        playerContacts.Add(other);

        if (wasOutside)
        {
            ShowPromptIfAvailable();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !IsPlayerCollider(other))
        {
            return;
        }

        if (!playerContacts.Remove(other))
        {
            Debug.LogError($"FacilityTrigger '{name}': exit received for unregistered player collider '{other.name}'.", this);
            return;
        }

        if (playerContacts.Count == 0)
        {
            promptView.Hide();
        }
    }

    private void HandlePromptOpenRequested()
    {
        if (!enabled)
        {
            return;
        }

        facilityUIHost.Open(facility);
    }

    private void HandlePanelOpened(FacilityDefinition openedFacility)
    {
        promptView.Hide();
    }

    private void HandlePanelClosed()
    {
        if (playerContacts.Count > 0)
        {
            ShowPromptIfAvailable();
        }
    }

    private void ShowPromptIfAvailable()
    {
        if (facilityUIHost.IsOpen)
        {
            promptView.Hide();
            return;
        }

        promptView.Show();
    }

    private bool IsPlayerCollider(Collider candidate)
    {
        return candidate != null
            && playerRoot != null
            && (candidate.transform == playerRoot || candidate.transform.IsChildOf(playerRoot));
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (facility == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': facility is not configured.", this);
            isValid = false;
        }
        else
        {
            isValid &= facility.ValidateConfiguration(this);
        }

        if (triggerCollider == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': triggerCollider is not configured.", this);
            isValid = false;
        }
        else
        {
            if (triggerCollider.gameObject != gameObject)
            {
                Debug.LogError($"FacilityTrigger '{name}': triggerCollider must be on the same GameObject as FacilityTrigger.", this);
                isValid = false;
            }

            if (!triggerCollider.isTrigger)
            {
                Debug.LogError($"FacilityTrigger '{name}': triggerCollider must have Is Trigger enabled.", triggerCollider);
                isValid = false;
            }
        }

        if (playerRoot == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': playerRoot is not configured.", this);
            isValid = false;
        }

        if (promptView == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': promptView is not configured.", this);
            isValid = false;
        }

        if (facilityUIHost == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': facilityUIHost is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
