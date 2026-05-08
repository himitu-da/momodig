using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToolSlotUI : MonoBehaviour
{
    [SerializeField] private Button slotButton;
    [SerializeField] private Button bindMainButton;
    [SerializeField] private Button bindSubButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private TMP_Text toolNameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text slotIdText;
    [SerializeField] private Image toolIconImage;
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private GameObject mainIndicator;
    [SerializeField] private GameObject subIndicator;

    private Action<string> onSlotClicked;
    private Action<string> onBindMainClicked;
    private Action<string> onBindSubClicked;
    private Action<string> onClearClicked;
    private Action<string> onRemoveClicked;
    private string slotId;

    public string SlotId => slotId;

    public void Initialize(
        Action<string> onSlotClicked,
        Action<string> onBindMainClicked,
        Action<string> onBindSubClicked,
        Action<string> onClearClicked,
        Action<string> onRemoveClicked)
    {
        this.onSlotClicked = onSlotClicked;
        this.onBindMainClicked = onBindMainClicked;
        this.onBindSubClicked = onBindSubClicked;
        this.onClearClicked = onClearClicked;
        this.onRemoveClicked = onRemoveClicked;

        if (slotButton == null)
        {
            slotButton = GetComponent<Button>();
        }

        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(HandleSlotClicked);
            slotButton.onClick.AddListener(HandleSlotClicked);
        }

        if (bindMainButton != null)
        {
            bindMainButton.onClick.RemoveListener(HandleBindMainClicked);
            bindMainButton.onClick.AddListener(HandleBindMainClicked);
        }

        if (bindSubButton != null)
        {
            bindSubButton.onClick.RemoveListener(HandleBindSubClicked);
            bindSubButton.onClick.AddListener(HandleBindSubClicked);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(HandleClearClicked);
            clearButton.onClick.AddListener(HandleClearClicked);
        }

        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(HandleRemoveClicked);
            removeButton.onClick.AddListener(HandleRemoveClicked);
        }
    }

    public void Render(ToolSlot slot, bool selected, bool isMainSlot, bool isSubSlot)
    {
        slotId = slot != null ? slot.SlotId : string.Empty;
        MiningTool tool = slot != null ? slot.Tool : null;

        if (toolNameText != null)
        {
            toolNameText.text = tool != null ? tool.ToolName : "Empty";
        }

        if (slotIdText != null)
        {
            slotIdText.text = slotId;
        }

        if (roleText != null)
        {
            roleText.text = BuildRoleText(isMainSlot, isSubSlot);
        }

        if (toolIconImage != null)
        {
            toolIconImage.sprite = tool != null ? tool.ToolIcon : null;
            toolIconImage.enabled = tool != null && tool.ToolIcon != null;
        }

        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(selected);
        }

        if (mainIndicator != null)
        {
            mainIndicator.SetActive(isMainSlot);
        }

        if (subIndicator != null)
        {
            subIndicator.SetActive(isSubSlot);
        }
    }

    private void HandleSlotClicked()
    {
        onSlotClicked?.Invoke(slotId);
    }

    private void HandleBindMainClicked()
    {
        onBindMainClicked?.Invoke(slotId);
    }

    private void HandleBindSubClicked()
    {
        onBindSubClicked?.Invoke(slotId);
    }

    private void HandleClearClicked()
    {
        onClearClicked?.Invoke(slotId);
    }

    private void HandleRemoveClicked()
    {
        onRemoveClicked?.Invoke(slotId);
    }

    private static string BuildRoleText(bool isMainSlot, bool isSubSlot)
    {
        if (isMainSlot && isSubSlot)
        {
            return "L/R";
        }

        if (isMainSlot)
        {
            return "L";
        }

        if (isSubSlot)
        {
            return "R";
        }

        return string.Empty;
    }
}
