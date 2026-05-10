using System;
using System.Collections.Generic;
using UnityEngine;

public class FacilityUpgradeService : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private FacilityUpgradeCatalog catalog;
    [SerializeField] private StorageManager storageManager;
    [SerializeField] private GameDataPersistenceManager persistenceManager;

    public event Action UpgradesChanged;

    public FacilityUpgradeCatalog Catalog => catalog;

    private void Awake()
    {
        if (!ValidateConfiguration(this))
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged += HandleFacilityUpgradesChanged;
    }

    private void OnDisable()
    {
        GameDataPersistenceManager.OnFacilityUpgradesChanged -= HandleFacilityUpgradesChanged;
    }

    public bool ValidateConfiguration(UnityEngine.Object context)
    {
        bool isValid = true;
        UnityEngine.Object logContext = context != null ? context : this;

        if (catalog == null)
        {
            Debug.LogError("FacilityUpgradeService: catalog is not configured.", logContext);
            isValid = false;
        }
        else
        {
            isValid &= catalog.ValidateConfiguration(logContext);
        }

        if (storageManager == null)
        {
            Debug.LogError("FacilityUpgradeService: storageManager is not configured.", logContext);
            isValid = false;
        }

        if (persistenceManager == null)
        {
            Debug.LogError("FacilityUpgradeService: persistenceManager is not configured.", logContext);
            isValid = false;
        }

        return isValid;
    }

    public List<FacilityUpgradeDefinition> GetUpgrades(FacilityType facilityType)
    {
        if (!EnsureReady())
        {
            return new List<FacilityUpgradeDefinition>();
        }

        return catalog.GetUpgrades(facilityType);
    }

    public int GetLevel(FacilityUpgradeDefinition upgrade)
    {
        if (!EnsureReady() || !ValidateUpgrade(upgrade))
        {
            return 0;
        }

        return persistenceManager.GetFacilityUpgradeLevel(upgrade.UpgradeId, upgrade.InitialLevel);
    }

    public List<FacilityUpgradeCostPreview> GetCostPreview(FacilityUpgradeDefinition upgrade, int purchaseCount)
    {
        Dictionary<ResourceType, int> totalCost = CalculateTotalCost(upgrade, purchaseCount);
        List<FacilityUpgradeCostPreview> preview = new List<FacilityUpgradeCostPreview>();

        foreach (KeyValuePair<ResourceType, int> cost in totalCost)
        {
            preview.Add(new FacilityUpgradeCostPreview(
                cost.Key,
                cost.Value,
                storageManager.GetResourceAmount(cost.Key)));
        }

        return preview;
    }

    public bool CanPurchase(FacilityUpgradeDefinition upgrade, int purchaseCount)
    {
        if (!EnsureReady() || !ValidatePurchaseRequest(upgrade, purchaseCount, false))
        {
            return false;
        }

        Dictionary<ResourceType, int> totalCost = CalculateTotalCost(upgrade, purchaseCount);
        return storageManager.CanSpendResources(totalCost);
    }

    public bool TryPurchase(FacilityUpgradeDefinition upgrade, int purchaseCount)
    {
        if (!EnsureReady() || !ValidatePurchaseRequest(upgrade, purchaseCount, true))
        {
            return false;
        }

        Dictionary<ResourceType, int> totalCost = CalculateTotalCost(upgrade, purchaseCount);
        if (!storageManager.TrySpendResources(totalCost))
        {
            Debug.LogError($"FacilityUpgradeService: not enough resources to purchase '{upgrade.DisplayName}'.", this);
            return false;
        }

        int currentLevel = GetLevel(upgrade);
        int nextLevel = currentLevel + purchaseCount;
        if (!persistenceManager.SetFacilityUpgradeLevel(upgrade.UpgradeId, nextLevel))
        {
            Debug.LogError($"FacilityUpgradeService: failed to persist level {nextLevel} for '{upgrade.UpgradeId}'.", this);
            return false;
        }

        return true;
    }

    public Dictionary<ResourceType, int> CalculateTotalCost(FacilityUpgradeDefinition upgrade, int purchaseCount)
    {
        Dictionary<ResourceType, int> totalCost = new Dictionary<ResourceType, int>();
        if (!EnsureReady() || !ValidatePurchaseRequest(upgrade, purchaseCount, false))
        {
            return totalCost;
        }

        int currentLevel = GetLevel(upgrade);
        try
        {
            for (int levelOffset = 0; levelOffset < purchaseCount; levelOffset++)
            {
                int levelForCost = currentLevel + levelOffset;
                IReadOnlyList<FacilityResourceCost> resourceCosts = upgrade.ResourceCosts;
                for (int i = 0; i < resourceCosts.Count; i++)
                {
                    FacilityResourceCost cost = resourceCosts[i];
                    int amount = cost.CalculateAmount(upgrade.CostScaling, levelForCost);
                    if (totalCost.ContainsKey(cost.ResourceType))
                    {
                        totalCost[cost.ResourceType] = checked(totalCost[cost.ResourceType] + amount);
                    }
                    else
                    {
                        totalCost.Add(cost.ResourceType, amount);
                    }
                }
            }
        }
        catch (OverflowException exception)
        {
            Debug.LogError($"FacilityUpgradeService: cost overflow while calculating '{upgrade.DisplayName}'. {exception.Message}", this);
            totalCost.Clear();
        }

        return totalCost;
    }

    private void HandleFacilityUpgradesChanged()
    {
        UpgradesChanged?.Invoke();
    }

    private bool EnsureReady()
    {
        if (enabled)
        {
            return true;
        }

        Debug.LogError("FacilityUpgradeService: service is not ready because required references are not configured.", this);
        return false;
    }

    private bool ValidateUpgrade(FacilityUpgradeDefinition upgrade)
    {
        if (upgrade == null)
        {
            Debug.LogError("FacilityUpgradeService: upgrade is not configured.", this);
            return false;
        }

        return upgrade.ValidateConfiguration(this);
    }

    private bool ValidatePurchaseRequest(FacilityUpgradeDefinition upgrade, int purchaseCount, bool logMaxLevelErrors)
    {
        if (!ValidateUpgrade(upgrade))
        {
            return false;
        }

        if (purchaseCount <= 0)
        {
            Debug.LogError("FacilityUpgradeService: purchaseCount must be greater than zero.", this);
            return false;
        }

        int currentLevel = GetLevel(upgrade);
        if (upgrade.IsMaxLevel(currentLevel))
        {
            if (logMaxLevelErrors)
            {
                Debug.LogError($"FacilityUpgradeService: '{upgrade.DisplayName}' is already at max level.", this);
            }

            return false;
        }

        if (upgrade.MaxLevel > 0 && currentLevel + purchaseCount > upgrade.MaxLevel)
        {
            if (logMaxLevelErrors)
            {
                Debug.LogError($"FacilityUpgradeService: purchaseCount exceeds max level for '{upgrade.DisplayName}'.", this);
            }

            return false;
        }

        return true;
    }
}
