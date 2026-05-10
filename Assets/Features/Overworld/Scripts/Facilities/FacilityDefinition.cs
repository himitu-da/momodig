using UnityEngine;

[CreateAssetMenu(fileName = "NewFacility", menuName = "Overworld/Facility Definition")]
public class FacilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string facilityId;
    [SerializeField] private string displayName;
    [SerializeField] private string promptLabel;

    [Header("UI")]
    [SerializeField] private FacilityPanel panelPrefab;

    public string FacilityId => facilityId;
    public string DisplayName => displayName;
    public string PromptLabel => promptLabel;
    public FacilityPanel PanelPrefab => panelPrefab;

    public bool ValidateConfiguration(Object context)
    {
        bool isValid = true;
        Object logContext = context != null ? context : this;

        if (string.IsNullOrWhiteSpace(facilityId))
        {
            Debug.LogError("FacilityDefinition: facilityId is not configured.", logContext);
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            Debug.LogError($"FacilityDefinition '{name}': displayName is not configured.", logContext);
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(promptLabel))
        {
            Debug.LogError($"FacilityDefinition '{name}': promptLabel is not configured.", logContext);
            isValid = false;
        }

        if (panelPrefab == null)
        {
            Debug.LogError($"FacilityDefinition '{name}': panelPrefab is not configured.", logContext);
            isValid = false;
        }

        return isValid;
    }
}
