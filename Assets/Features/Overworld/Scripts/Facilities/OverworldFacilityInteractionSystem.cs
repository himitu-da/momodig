using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class OverworldFacilityInteractionSystem : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private FacilityPromptView promptView;
    [SerializeField] private FacilityUIHost facilityUIHost;

    private readonly Dictionary<FacilityTrigger, HashSet<Collider>> activeContacts = new Dictionary<FacilityTrigger, HashSet<Collider>>();
    private FacilityTrigger currentTrigger;
    private bool hasReportedAmbiguousContact;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        promptView.Hide();
        facilityUIHost.PanelOpened += HandlePanelOpened;
        facilityUIHost.PanelClosed += HandlePanelClosed;
    }

    private void OnDestroy()
    {
        if (facilityUIHost != null)
        {
            facilityUIHost.PanelOpened -= HandlePanelOpened;
            facilityUIHost.PanelClosed -= HandlePanelClosed;
        }
    }

    public bool IsPlayerCollider(Collider candidate)
    {
        return candidate != null
            && playerRoot != null
            && (candidate.transform == playerRoot || candidate.transform.IsChildOf(playerRoot));
    }

    public void EnterFacility(FacilityTrigger trigger, Collider playerCollider)
    {
        if (!ValidateContactArguments(trigger, playerCollider))
        {
            return;
        }

        if (!activeContacts.TryGetValue(trigger, out HashSet<Collider> colliders))
        {
            colliders = new HashSet<Collider>();
            activeContacts.Add(trigger, colliders);
        }

        colliders.Add(playerCollider);
        RefreshContactState();
    }

    public void ExitFacility(FacilityTrigger trigger, Collider playerCollider)
    {
        if (!ValidateContactArguments(trigger, playerCollider))
        {
            return;
        }

        if (!activeContacts.TryGetValue(trigger, out HashSet<Collider> colliders))
        {
            Debug.LogError($"OverworldFacilityInteractionSystem: exit received for '{trigger.name}' before enter.", this);
            return;
        }

        colliders.Remove(playerCollider);
        if (colliders.Count == 0)
        {
            activeContacts.Remove(trigger);
        }

        RefreshContactState();
    }

    private void OpenCurrentFacility()
    {
        if (currentTrigger == null)
        {
            Debug.LogError("OverworldFacilityInteractionSystem: no active facility is selected.", this);
            return;
        }

        if (facilityUIHost.Open(currentTrigger.Facility))
        {
            promptView.Hide();
        }
    }

    private void RefreshContactState()
    {
        if (facilityUIHost.IsOpen)
        {
            promptView.Hide();
            return;
        }

        if (activeContacts.Count == 0)
        {
            currentTrigger = null;
            hasReportedAmbiguousContact = false;
            promptView.Hide();
            return;
        }

        if (activeContacts.Count > 1)
        {
            currentTrigger = null;
            promptView.Hide();

            if (!hasReportedAmbiguousContact)
            {
                Debug.LogError($"OverworldFacilityInteractionSystem: player is touching multiple facilities at once: {BuildActiveFacilityList()}", this);
                hasReportedAmbiguousContact = true;
            }

            return;
        }

        hasReportedAmbiguousContact = false;

        foreach (FacilityTrigger trigger in activeContacts.Keys)
        {
            currentTrigger = trigger;
            promptView.Show(trigger.Facility, trigger.PromptAnchor, OpenCurrentFacility);
            return;
        }
    }

    private void HandlePanelOpened(FacilityDefinition facility)
    {
        promptView.Hide();
    }

    private void HandlePanelClosed()
    {
        RefreshContactState();
    }

    private bool ValidateContactArguments(FacilityTrigger trigger, Collider playerCollider)
    {
        bool isValid = true;

        if (trigger == null)
        {
            Debug.LogError("OverworldFacilityInteractionSystem: trigger is not configured.", this);
            isValid = false;
        }

        if (!IsPlayerCollider(playerCollider))
        {
            Debug.LogError("OverworldFacilityInteractionSystem: contact collider does not belong to the configured playerRoot.", this);
            isValid = false;
        }

        return isValid;
    }

    private string BuildActiveFacilityList()
    {
        StringBuilder builder = new StringBuilder();
        bool needsSeparator = false;

        foreach (FacilityTrigger trigger in activeContacts.Keys)
        {
            if (needsSeparator)
            {
                builder.Append(", ");
            }

            builder.Append(trigger != null && trigger.Facility != null ? trigger.Facility.DisplayName : "(missing facility)");
            needsSeparator = true;
        }

        return builder.ToString();
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (playerRoot == null)
        {
            Debug.LogError("OverworldFacilityInteractionSystem: playerRoot is not configured.", this);
            isValid = false;
        }

        if (promptView == null)
        {
            Debug.LogError("OverworldFacilityInteractionSystem: promptView is not configured.", this);
            isValid = false;
        }

        if (facilityUIHost == null)
        {
            Debug.LogError("OverworldFacilityInteractionSystem: facilityUIHost is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
