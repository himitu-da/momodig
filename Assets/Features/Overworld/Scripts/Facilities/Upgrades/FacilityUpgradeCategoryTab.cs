using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityUpgradeCategoryTab : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;

    [Header("Optional State")]
    [SerializeField] private GameObject selectedMarker;

    private string categoryId;
    private Action<string> selectedCallback;

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public bool Initialize(string id, string label, Action<string> callback)
    {
        if (!ValidateRequiredReferences())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError($"FacilityUpgradeCategoryTab '{name}': id is not configured.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            Debug.LogError($"FacilityUpgradeCategoryTab '{name}': label is not configured.", this);
            return false;
        }

        if (callback == null)
        {
            Debug.LogError($"FacilityUpgradeCategoryTab '{name}': callback is not configured.", this);
            return false;
        }

        categoryId = id;
        selectedCallback = callback;
        labelText.SetText(label);
        button.onClick.AddListener(HandleClicked);
        SetSelected(false);
        return true;
    }

    public void SetSelected(bool selected)
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected);
        }

        if (button != null)
        {
            button.interactable = !selected;
        }
    }

    private void HandleClicked()
    {
        if (selectedCallback == null)
        {
            Debug.LogError($"FacilityUpgradeCategoryTab '{name}': clicked before Initialize.", this);
            return;
        }

        selectedCallback.Invoke(categoryId);
    }

    private bool ValidateRequiredReferences()
    {
        bool isValid = true;

        if (button == null)
        {
            Debug.LogError($"FacilityUpgradeCategoryTab '{name}': button is not configured.", this);
            isValid = false;
        }

        if (labelText == null)
        {
            Debug.LogError($"FacilityUpgradeCategoryTab '{name}': labelText is not configured.", this);
            isValid = false;
        }

        return isValid;
    }
}
