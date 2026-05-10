using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FacilityUpgradeCatalog", menuName = "Momodig/Facilities/Upgrade Catalog")]
public class FacilityUpgradeCatalog : ScriptableObject
{
    [SerializeField] private List<FacilityUpgradeDefinition> upgrades = new List<FacilityUpgradeDefinition>();

    public IReadOnlyList<FacilityUpgradeDefinition> Upgrades => upgrades;

    public bool ValidateConfiguration(Object context)
    {
        bool isValid = true;
        Object logContext = context != null ? context : this;

        if (upgrades == null || upgrades.Count == 0)
        {
            Debug.LogError($"FacilityUpgradeCatalog '{name}': upgrades is not configured.", logContext);
            return false;
        }

        HashSet<string> usedIds = new HashSet<string>();
        for (int i = 0; i < upgrades.Count; i++)
        {
            FacilityUpgradeDefinition upgrade = upgrades[i];
            if (upgrade == null)
            {
                Debug.LogError($"FacilityUpgradeCatalog '{name}': upgrades contains a null entry at index {i}.", logContext);
                isValid = false;
                continue;
            }

            isValid &= upgrade.ValidateConfiguration(logContext);

            if (!string.IsNullOrWhiteSpace(upgrade.UpgradeId) && !usedIds.Add(upgrade.UpgradeId))
            {
                Debug.LogError($"FacilityUpgradeCatalog '{name}': duplicate upgradeId '{upgrade.UpgradeId}'.", logContext);
                isValid = false;
            }
        }

        return isValid;
    }

    public List<FacilityUpgradeDefinition> GetUpgrades(FacilityType facilityType)
    {
        List<FacilityUpgradeDefinition> results = new List<FacilityUpgradeDefinition>();
        if (upgrades == null)
        {
            return results;
        }

        for (int i = 0; i < upgrades.Count; i++)
        {
            FacilityUpgradeDefinition upgrade = upgrades[i];
            if (upgrade != null && upgrade.FacilityType == facilityType)
            {
                results.Add(upgrade);
            }
        }

        return results;
    }
}
