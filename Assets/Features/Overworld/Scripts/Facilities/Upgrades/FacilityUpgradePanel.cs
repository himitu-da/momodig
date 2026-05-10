using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityUpgradePanel : FacilityPanel, IFacilityPanelRuntimeBinding
{
    [Header("Facility")]
    [SerializeField] private FacilityType facilityType;

    [Header("Required Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text selectedNameText;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text selectedLevelText;
    [SerializeField] private TMP_Text selectedDescriptionText;
    [SerializeField] private TMP_Text purchaseStatusText;

    [Header("Required Lists")]
    [SerializeField] private Transform categoryRoot;
    [SerializeField] private Transform upgradeRoot;
    [SerializeField] private Transform costRoot;
    [SerializeField] private FacilityUpgradeCategoryTab categoryTabPrefab;
    [SerializeField] private FacilityUpgradeRow upgradeRowPrefab;
    [SerializeField] private FacilityUpgradeCostRow costRowPrefab;

    [Header("Required Actions")]
    [SerializeField] private Button purchaseButton;

    private readonly List<FacilityUpgradeCategoryTab> createdCategoryTabs = new List<FacilityUpgradeCategoryTab>();
    private readonly List<string> createdCategoryIds = new List<string>();
    private readonly List<FacilityUpgradeRow> createdUpgradeRows = new List<FacilityUpgradeRow>();
    private readonly List<FacilityUpgradeCostRow> createdCostRows = new List<FacilityUpgradeCostRow>();
    private readonly List<FacilityUpgradeDefinition> currentFacilityUpgrades = new List<FacilityUpgradeDefinition>();
    private readonly List<string> categoryOrder = new List<string>();
    private readonly Dictionary<string, string> categoryLabels = new Dictionary<string, string>();

    private FacilityUpgradeService upgradeService;
    private string selectedCategoryId;
    private FacilityUpgradeDefinition selectedUpgrade;
    private bool isSubscribedToService;
    private bool isPurchaseButtonBound;

    public bool BindRuntime(FacilityUpgradeService service)
    {
        if (service == null)
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': upgradeService is not configured.", this);
            return false;
        }

        if (!service.ValidateConfiguration(this))
        {
            return false;
        }

        upgradeService = service;
        return true;
    }

    protected override bool ValidatePanelConfiguration()
    {
        bool isValid = true;

        if (upgradeService == null)
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': upgradeService is not configured.", this);
            isValid = false;
        }

        isValid &= ValidateReference(titleText, nameof(titleText));
        isValid &= ValidateReference(selectedNameText, nameof(selectedNameText));
        isValid &= ValidateReference(categoryRoot, nameof(categoryRoot));
        isValid &= ValidateReference(upgradeRoot, nameof(upgradeRoot));
        isValid &= ValidateReference(costRoot, nameof(costRoot));
        isValid &= ValidateReference(categoryTabPrefab, nameof(categoryTabPrefab));
        isValid &= ValidateReference(upgradeRowPrefab, nameof(upgradeRowPrefab));
        isValid &= ValidateReference(costRowPrefab, nameof(costRowPrefab));
        isValid &= ValidateReference(purchaseButton, nameof(purchaseButton));

        return isValid;
    }

    protected override void OnInitialized()
    {
        titleText.SetText(Facility.DisplayName);
        purchaseButton.onClick.AddListener(HandlePurchaseClicked);
        isPurchaseButtonBound = true;
        SubscribeToService();
        Rebuild();
    }

    protected override void OnClosing()
    {
        if (isPurchaseButtonBound && purchaseButton != null)
        {
            purchaseButton.onClick.RemoveListener(HandlePurchaseClicked);
            isPurchaseButtonBound = false;
        }

        UnsubscribeFromService();
        ClearCreatedViews();
    }

    private void SubscribeToService()
    {
        if (upgradeService == null || isSubscribedToService)
        {
            return;
        }

        upgradeService.UpgradesChanged += HandleUpgradesChanged;
        isSubscribedToService = true;
    }

    private void UnsubscribeFromService()
    {
        if (upgradeService == null || !isSubscribedToService)
        {
            return;
        }

        upgradeService.UpgradesChanged -= HandleUpgradesChanged;
        isSubscribedToService = false;
    }

    private void Rebuild()
    {
        ClearCreatedViews();
        ClearRootChildren(categoryRoot);
        ClearRootChildren(upgradeRoot);
        ClearRootChildren(costRoot);
        currentFacilityUpgrades.Clear();
        categoryOrder.Clear();
        categoryLabels.Clear();

        List<FacilityUpgradeDefinition> upgrades = upgradeService.GetUpgrades(facilityType);
        for (int i = 0; i < upgrades.Count; i++)
        {
            FacilityUpgradeDefinition upgrade = upgrades[i];
            if (upgrade == null)
            {
                Debug.LogError($"FacilityUpgradePanel '{name}': upgrade list contains a null entry at index {i}.", this);
                continue;
            }

            currentFacilityUpgrades.Add(upgrade);
            if (!categoryLabels.ContainsKey(upgrade.CategoryId))
            {
                categoryOrder.Add(upgrade.CategoryId);
                categoryLabels.Add(upgrade.CategoryId, upgrade.CategoryLabel);
            }
        }

        if (currentFacilityUpgrades.Count == 0)
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': no upgrades are configured for '{facilityType}'.", this);
            SetNoSelection();
            return;
        }

        selectedCategoryId = categoryOrder[0];
        BuildCategoryTabs();
        RefreshUpgradeList();
    }

    private void BuildCategoryTabs()
    {
        for (int i = 0; i < categoryOrder.Count; i++)
        {
            string categoryId = categoryOrder[i];
            FacilityUpgradeCategoryTab tab = Instantiate(categoryTabPrefab, categoryRoot);
            if (!tab.Initialize(categoryId, categoryLabels[categoryId], SelectCategory))
            {
                Debug.LogError($"FacilityUpgradePanel '{name}': failed to initialize category tab '{categoryId}'.", this);
                Destroy(tab.gameObject);
                continue;
            }

            tab.gameObject.SetActive(true);
            createdCategoryTabs.Add(tab);
            createdCategoryIds.Add(categoryId);
        }

        RefreshCategorySelection();
    }

    private void SelectCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': categoryId is not configured.", this);
            return;
        }

        if (!categoryLabels.ContainsKey(categoryId))
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': unknown categoryId '{categoryId}'.", this);
            return;
        }

        selectedCategoryId = categoryId;
        RefreshCategorySelection();
        RefreshUpgradeList();
    }

    private void RefreshCategorySelection()
    {
        for (int i = 0; i < createdCategoryTabs.Count; i++)
        {
            FacilityUpgradeCategoryTab tab = createdCategoryTabs[i];
            tab.SetSelected(createdCategoryIds[i] == selectedCategoryId);
        }
    }

    private void RefreshUpgradeList()
    {
        ClearUpgradeRows();
        selectedUpgrade = null;

        for (int i = 0; i < currentFacilityUpgrades.Count; i++)
        {
            FacilityUpgradeDefinition upgrade = currentFacilityUpgrades[i];
            if (upgrade.CategoryId != selectedCategoryId)
            {
                continue;
            }

            FacilityUpgradeRow row = Instantiate(upgradeRowPrefab, upgradeRoot);
            if (!row.Initialize(upgrade, upgradeService, SelectUpgrade))
            {
                Debug.LogError($"FacilityUpgradePanel '{name}': failed to initialize upgrade row '{upgrade.UpgradeId}'.", this);
                Destroy(row.gameObject);
                continue;
            }

            row.gameObject.SetActive(true);
            createdUpgradeRows.Add(row);

            if (selectedUpgrade == null)
            {
                selectedUpgrade = upgrade;
            }
        }

        if (selectedUpgrade == null)
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': no upgrades are configured for category '{selectedCategoryId}'.", this);
            SetNoSelection();
            return;
        }

        RefreshSelection();
    }

    private void SelectUpgrade(FacilityUpgradeDefinition upgrade)
    {
        if (upgrade == null)
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': upgrade is not configured.", this);
            return;
        }

        selectedUpgrade = upgrade;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (selectedUpgrade == null)
        {
            SetNoSelection();
            return;
        }

        int level = upgradeService.GetLevel(selectedUpgrade);
        string levelText = $"Level {level} / {selectedUpgrade.MaxLevel}";
        if (selectedLevelText == null)
        {
            selectedNameText.SetText($"{selectedUpgrade.DisplayName}\n{levelText}");
        }
        else
        {
            selectedNameText.SetText(selectedUpgrade.DisplayName);
            selectedLevelText.SetText(levelText);
        }

        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.SetText(selectedUpgrade.Description);
        }

        RefreshCostRows();
        RefreshUpgradeRows();
        RefreshPurchaseState();
    }

    private void RefreshUpgradeRows()
    {
        for (int i = 0; i < createdUpgradeRows.Count; i++)
        {
            FacilityUpgradeRow row = createdUpgradeRows[i];
            row.Refresh();
        }
    }

    private void RefreshCostRows()
    {
        ClearCostRows();

        List<FacilityUpgradeCostPreview> costs = upgradeService.GetCostPreview(selectedUpgrade, 1);
        for (int i = 0; i < costs.Count; i++)
        {
            FacilityUpgradeCostRow row = Instantiate(costRowPrefab, costRoot);
            if (!row.SetCost(costs[i]))
            {
                Debug.LogError($"FacilityUpgradePanel '{name}': failed to initialize cost row for '{costs[i].ResourceType}'.", this);
                Destroy(row.gameObject);
                continue;
            }

            row.gameObject.SetActive(true);
            createdCostRows.Add(row);
        }
    }

    private void RefreshPurchaseState()
    {
        if (selectedUpgrade == null)
        {
            purchaseButton.interactable = false;
            SetPurchaseStatus("Select an upgrade");
            return;
        }

        int level = upgradeService.GetLevel(selectedUpgrade);
        if (selectedUpgrade.IsMaxLevel(level))
        {
            purchaseButton.interactable = false;
            SetPurchaseStatus("Max level");
            return;
        }

        bool canPurchase = upgradeService.CanPurchase(selectedUpgrade, 1);
        purchaseButton.interactable = canPurchase;
        SetPurchaseStatus(canPurchase ? "Ready" : "Not enough materials");
    }

    private void HandlePurchaseClicked()
    {
        if (selectedUpgrade == null)
        {
            Debug.LogError($"FacilityUpgradePanel '{name}': purchase clicked without a selected upgrade.", this);
            return;
        }

        if (!upgradeService.TryPurchase(selectedUpgrade, 1))
        {
            RefreshPurchaseState();
            return;
        }

        RefreshSelection();
    }

    private void HandleUpgradesChanged()
    {
        RefreshSelection();
    }

    private void SetNoSelection()
    {
        selectedUpgrade = null;
        selectedNameText.SetText("No upgrade");
        if (selectedLevelText != null)
        {
            selectedLevelText.SetText("-");
        }

        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.SetText("No upgrade is configured for this facility.");
        }

        SetPurchaseStatus("Unavailable");
        purchaseButton.interactable = false;
        ClearUpgradeRows();
        ClearCostRows();
    }

    private void SetPurchaseStatus(string message)
    {
        if (purchaseStatusText != null)
        {
            purchaseStatusText.SetText(message);
        }
    }

    private void ClearCreatedViews()
    {
        ClearCategoryTabs();
        ClearUpgradeRows();
        ClearCostRows();
    }

    private void ClearCategoryTabs()
    {
        for (int i = 0; i < createdCategoryTabs.Count; i++)
        {
            if (createdCategoryTabs[i] != null)
            {
                Destroy(createdCategoryTabs[i].gameObject);
            }
        }

        createdCategoryTabs.Clear();
        createdCategoryIds.Clear();
    }

    private void ClearUpgradeRows()
    {
        for (int i = 0; i < createdUpgradeRows.Count; i++)
        {
            if (createdUpgradeRows[i] != null)
            {
                Destroy(createdUpgradeRows[i].gameObject);
            }
        }

        createdUpgradeRows.Clear();
    }

    private void ClearCostRows()
    {
        for (int i = 0; i < createdCostRows.Count; i++)
        {
            if (createdCostRows[i] != null)
            {
                Destroy(createdCostRows[i].gameObject);
            }
        }

        createdCostRows.Clear();
    }

    private void ClearRootChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private bool ValidateReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogError($"FacilityUpgradePanel '{name}': {fieldName} is not configured.", this);
        return false;
    }
}
