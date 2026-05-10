using TMPro;
using UnityEngine;

public class FacilityUpgradeCostRow : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private TMP_Text resourceNameText;
    [SerializeField] private TMP_Text amountText;

    public bool SetCost(FacilityUpgradeCostPreview cost)
    {
        if (!ValidateRequiredReferences())
        {
            return false;
        }

        if (cost == null)
        {
            Debug.LogError($"FacilityUpgradeCostRow '{name}': cost is not configured.", this);
            return false;
        }

        resourceNameText.SetText(ResourceTypeUtility.GetDisplayName(cost.ResourceType));
        amountText.SetText($"{cost.OwnedAmount}/{cost.RequiredAmount}");
        return true;
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (resourceNameText == null)
        {
            Debug.LogError($"FacilityUpgradeCostRow '{name}': resourceNameText is not configured.", this);
            isValid = false;
        }

        if (amountText == null)
        {
            Debug.LogError($"FacilityUpgradeCostRow '{name}': amountText is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
