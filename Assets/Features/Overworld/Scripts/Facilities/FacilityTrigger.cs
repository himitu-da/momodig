using UnityEngine;

public class FacilityTrigger : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private FacilityDefinition facility;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private OverworldFacilityInteractionSystem interactionSystem;

    public FacilityDefinition Facility => facility;
    public Transform PromptAnchor => promptAnchor;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !interactionSystem.IsPlayerCollider(other))
        {
            return;
        }

        interactionSystem.EnterFacility(this, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !interactionSystem.IsPlayerCollider(other))
        {
            return;
        }

        interactionSystem.ExitFacility(this, other);
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

        if (promptAnchor == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': promptAnchor is not configured.", this);
            isValid = false;
        }

        if (interactionSystem == null)
        {
            Debug.LogError($"FacilityTrigger '{name}': interactionSystem is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
