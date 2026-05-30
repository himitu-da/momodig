using UnityEngine;
using UnityEngine.UI;

public class FacilityOpenButton : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Button button;
    [SerializeField] private FacilityUIHost facilityUIHost;
    [SerializeField] private FacilityDefinition facility;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        button.onClick.AddListener(OpenConfiguredFacility);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenConfiguredFacility);
        }
    }

    public void OpenConfiguredFacility()
    {
        if (!enabled)
        {
            Debug.LogError($"FacilityOpenButton '{name}': cannot open because the component is disabled.", this);
            return;
        }

        facilityUIHost.Open(facility);
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (button == null)
        {
            Debug.LogError($"FacilityOpenButton '{name}': button is not configured.", this);
            isValid = false;
        }

        if (facilityUIHost == null)
        {
            Debug.LogError($"FacilityOpenButton '{name}': facilityUIHost is not configured.", this);
            isValid = false;
        }

        if (facility == null)
        {
            Debug.LogError($"FacilityOpenButton '{name}': facility is not configured.", this);
            isValid = false;
        }
        else
        {
            isValid &= facility.ValidateConfiguration(this);
        }

        return isValid;
    }
}
