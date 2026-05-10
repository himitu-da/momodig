using TMPro;
using UnityEngine;

public class FacilityUpgradeCostRow : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private TMP_Text resourceNameText;

    [Header("Optional Text")]
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

        string resourceName = ResourceTypeUtility.GetDisplayName(cost.ResourceType);
        string amount = $"{cost.OwnedAmount}/{cost.RequiredAmount}";
        if (amountText == null)
        {
            resourceNameText.SetText($"{resourceName}  {amount}");
            return true;
        }

        resourceNameText.SetText(resourceName);
        amountText.SetText(amount);
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

        return isValid;
    }
}
