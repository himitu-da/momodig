using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FacilityUpgrade", menuName = "Momodig/Facilities/Upgrade Definition")]
public class FacilityUpgradeDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string upgradeId;
    [SerializeField] private FacilityType facilityType;
    [SerializeField] private string categoryId;
    [SerializeField] private string categoryLabel;

    [Header("Display")]
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Progression")]
    [SerializeField] private int initialLevel;
    [SerializeField] private int maxLevel = 99;
    [SerializeField] private FacilityUpgradeCostScaling costScaling = FacilityUpgradeCostScaling.NoChange;

    [Header("Cost")]
    [SerializeField] private List<FacilityResourceCost> resourceCosts = new List<FacilityResourceCost>();

    [Header("Effects")]
    [SerializeField] private List<Enhancement> enhancements = new List<Enhancement>();

    public string UpgradeId => upgradeId;
    public FacilityType FacilityType => facilityType;
    public string CategoryId => categoryId;
    public string CategoryLabel => categoryLabel;
    public string DisplayName => displayName;
    public string Description => description;
    public int InitialLevel => initialLevel;
    public int MaxLevel => maxLevel;
    public FacilityUpgradeCostScaling CostScaling => costScaling;
    public IReadOnlyList<FacilityResourceCost> ResourceCosts => resourceCosts;
    public IReadOnlyList<Enhancement> Enhancements => enhancements;

    public bool IsMaxLevel(int currentLevel)
    {
        return maxLevel > 0 && currentLevel >= maxLevel;
    }

    public bool ValidateConfiguration(Object context)
    {
        bool isValid = true;
        Object logContext = context != null ? context : this;

        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': upgradeId is not configured.", logContext);
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(categoryId))
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': categoryId is not configured.", logContext);
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(categoryLabel))
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': categoryLabel is not configured.", logContext);
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': displayName is not configured.", logContext);
            isValid = false;
        }

        if (initialLevel < 0)
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': initialLevel must not be negative.", logContext);
            isValid = false;
        }

        if (maxLevel <= 0)
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': maxLevel must be greater than zero.", logContext);
            isValid = false;
        }

        if (maxLevel > 0 && initialLevel > maxLevel)
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': initialLevel must not exceed maxLevel.", logContext);
            isValid = false;
        }

        if (resourceCosts == null || resourceCosts.Count == 0)
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': resourceCosts is not configured.", logContext);
            isValid = false;
        }
        else
        {
            for (int i = 0; i < resourceCosts.Count; i++)
            {
                if (resourceCosts[i] == null)
                {
                    Debug.LogError($"FacilityUpgradeDefinition '{name}': resourceCosts contains a null entry at index {i}.", logContext);
                    isValid = false;
                    continue;
                }

                isValid &= resourceCosts[i].ValidateConfiguration(logContext);
            }
        }

        if (enhancements == null || enhancements.Count == 0)
        {
            Debug.LogError($"FacilityUpgradeDefinition '{name}': enhancements is not configured.", logContext);
            isValid = false;
        }
        else
        {
            for (int i = 0; i < enhancements.Count; i++)
            {
                if (enhancements[i] == null)
                {
                    Debug.LogError($"FacilityUpgradeDefinition '{name}': enhancements contains a null entry at index {i}.", logContext);
                    isValid = false;
                }
            }
        }

        return isValid;
    }
}
