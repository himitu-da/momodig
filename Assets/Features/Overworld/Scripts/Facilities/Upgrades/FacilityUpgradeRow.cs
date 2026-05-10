using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityUpgradeRow : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text levelText;

    [Header("Optional State")]
    [SerializeField] private GameObject selectedMarker;

    private FacilityUpgradeDefinition upgrade;
    private FacilityUpgradeService service;
    private Action<FacilityUpgradeDefinition> selectedCallback;

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public bool Initialize(
        FacilityUpgradeDefinition upgradeDefinition,
        FacilityUpgradeService upgradeService,
        Action<FacilityUpgradeDefinition> callback)
    {
        if (!ValidateRequiredReferences())
        {
            return false;
        }

        if (upgradeDefinition == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': upgradeDefinition is not configured.", this);
            return false;
        }

        if (upgradeService == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': upgradeService is not configured.", this);
            return false;
        }

        if (callback == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': callback is not configured.", this);
            return false;
        }

        upgrade = upgradeDefinition;
        service = upgradeService;
        selectedCallback = callback;
        button.onClick.AddListener(HandleClicked);
        Refresh();
        SetSelected(false);
        return true;
    }

    public void Refresh()
    {
        if (upgrade == null || service == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': Refresh called before Initialize.", this);
            return;
        }

        int level = service.GetLevel(upgrade);
        if (levelText == null)
        {
            nameText.SetText($"{upgrade.DisplayName}  Lv {level}/{upgrade.MaxLevel}");
            return;
        }

        nameText.SetText(upgrade.DisplayName);
        levelText.SetText($"Lv {level}/{upgrade.MaxLevel}");
    }

    public void SetSelected(bool selected)
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected);
        }
    }

    private void HandleClicked()
    {
        if (upgrade == null || selectedCallback == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': clicked before Initialize.", this);
            return;
        }

        selectedCallback.Invoke(upgrade);
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (button == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': button is not configured.", this);
            isValid = false;
        }

        if (nameText == null)
        {
            Debug.LogError($"FacilityUpgradeRow '{name}': nameText is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
