using UnityEngine;

public class OverWorldChangeScene : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private ChangeScene changescene;
    [SerializeField] private FacilityUIHost facilityUIHost;

    [Header("Scene Names")]
    [SerializeField] private string miningSceneName;

    [Header("Facilities")]
    [SerializeField] private FacilityDefinition workshopFacility;
    [SerializeField] private FacilityDefinition garageFacility;

    private bool isConfigured;

    private void Awake()
    {
        isConfigured = ValidateRequiredReferences();
    }

    public void SelectMineplace()
    {
        if (!EnsureConfigured())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(miningSceneName))
        {
            Debug.LogError("OverWorldChangeScene: miningSceneName is not configured.", this);
            return;
        }

        changescene.OnClickToChangeScene(miningSceneName);
    }

    public void SelectWorkshop()
    {
        OpenFacility(workshopFacility, nameof(workshopFacility));
    }

    public void SelectGarage()
    {
        OpenFacility(garageFacility, nameof(garageFacility));
    }

    private void OpenFacility(FacilityDefinition facility, string fieldName)
    {
        if (!EnsureConfigured())
        {
            return;
        }

        if (facility == null)
        {
            Debug.LogError($"OverWorldChangeScene: {fieldName} is not configured.", this);
            return;
        }

        if (!facility.ValidateConfiguration(this))
        {
            return;
        }

        facilityUIHost.Open(facility);
    }

    private bool EnsureConfigured()
    {
        if (isConfigured)
        {
            return true;
        }

        Debug.LogError("OverWorldChangeScene: required references are not configured.", this);
        return false;
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (changescene == null)
        {
            Debug.LogError("OverWorldChangeScene: changescene is not configured.", this);
            isValid = false;
        }

        if (facilityUIHost == null)
        {
            Debug.LogError("OverWorldChangeScene: facilityUIHost is not configured.", this);
            isValid = false;
        }

        isValid &= ValidateSceneName(miningSceneName, nameof(miningSceneName));
        isValid &= ValidateFacility(workshopFacility, nameof(workshopFacility));
        isValid &= ValidateFacility(garageFacility, nameof(garageFacility));

        return isValid;
    }

    private bool ValidateSceneName(string sceneName, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            return true;
        }

        Debug.LogError($"OverWorldChangeScene: {fieldName} is not configured.", this);
        return false;
    }

    private bool ValidateFacility(FacilityDefinition facility, string fieldName)
    {
        if (facility == null)
        {
            Debug.LogError($"OverWorldChangeScene: {fieldName} is not configured.", this);
            return false;
        }

        return facility.ValidateConfiguration(this);
    }
}
