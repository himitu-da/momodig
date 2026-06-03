using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiningEntranceInventoryPanel : FacilityPanel, IMiningEntrancePanelRuntimeBinding
{
    private const float PanelWidth = 760f;
    private const float PanelHeight = 520f;
    private const float RowHeight = 42f;

    private PlayerInventoryLoadout loadout;
    private RectTransform contentRoot;
    private Text statusText;
    private string selectedSlotId;

    public bool BindRuntime(PlayerInventoryLoadout loadout)
    {
        if (loadout == null)
        {
            Debug.LogError($"MiningEntranceInventoryPanel '{name}': loadout is not configured.", this);
            return false;
        }

        this.loadout = loadout;
        return true;
    }

    protected override bool ValidatePanelConfiguration()
    {
        if (loadout != null)
        {
            return true;
        }

        Debug.LogError($"MiningEntranceInventoryPanel '{name}': PlayerInventoryLoadout is not bound.", this);
        return false;
    }

    protected override void OnInitialized()
    {
        if (!loadout.BeginDraft())
        {
            SetStatus("Loadout could not be loaded.");
            return;
        }

        BuildStaticLayout();
        Refresh();
    }

    private void BuildStaticLayout()
    {
        RectTransform panel = EnsureRectTransform(gameObject);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image background = EnsureImage(gameObject);
        background.color = new Color(0.08f, 0.09f, 0.1f, 0.94f);
        EnsureCloseButtonLabel();

        contentRoot = CreatePanelChild("Content", panel, new Vector2(0f, 0f), new Vector2(PanelWidth - 32f, PanelHeight - 32f));
        CreateText("Title", contentRoot, "Mining Entrance", new Vector2(0f, 224f), new Vector2(PanelWidth - 64f, 34f), 24, TextAnchor.MiddleLeft);
        statusText = CreateText("Status", contentRoot, "", new Vector2(0f, -228f), new Vector2(PanelWidth - 64f, 28f), 14, TextAnchor.MiddleLeft);

        Button commitButton = CreateButton("CommitButton", contentRoot, "Commit", new Vector2(258f, -226f), new Vector2(104f, 32f));
        commitButton.onClick.AddListener(HandleCommitClicked);
    }

    private void Refresh()
    {
        if (contentRoot == null)
        {
            return;
        }

        DestroyNamedChildren(contentRoot, "SlotRow");
        DestroyNamedChildren(contentRoot, "OwnedToolButton");

        IReadOnlyList<ToolSlotPersistenceData> slots = loadout.DraftToolSlots;
        float slotStartY = 168f;
        for (int i = 0; i < slots.Count; i++)
        {
            CreateSlotRow(slots[i], new Vector2(-190f, slotStartY - i * RowHeight));
        }

        List<MiningTool> ownedTools = loadout.GetOwnedTools();
        float toolStartY = 168f;
        for (int i = 0; i < ownedTools.Count; i++)
        {
            CreateOwnedToolButton(ownedTools[i], new Vector2(212f, toolStartY - i * RowHeight));
        }

        if (string.IsNullOrEmpty(selectedSlotId) && slots.Count > 0)
        {
            selectedSlotId = slots[0].slotId;
        }

        SetStatus(string.IsNullOrEmpty(selectedSlotId)
            ? "Select a slot."
            : $"Selected slot: {selectedSlotId}");
    }

    private void CreateSlotRow(ToolSlotPersistenceData slot, Vector2 position)
    {
        if (slot == null)
        {
            return;
        }

        RectTransform row = CreatePanelChild("SlotRow", contentRoot, position, new Vector2(360f, 36f));
        string toolName = slot.tool != null ? slot.tool.ToolName : "Empty";
        string role = "";
        if (slot.slotId == loadout.DraftMainToolSlotId)
        {
            role += "L";
        }

        if (slot.slotId == loadout.DraftSubToolSlotId)
        {
            role += string.IsNullOrEmpty(role) ? "R" : "/R";
        }

        Button selectButton = CreateButton("SelectSlotButton", row, $"{slot.slotId}: {toolName} {role}", new Vector2(-54f, 0f), new Vector2(244f, 32f));
        string capturedSlotId = slot.slotId;
        selectButton.onClick.AddListener(() =>
        {
            selectedSlotId = capturedSlotId;
            Refresh();
        });

        Button mainButton = CreateButton("MainButton", row, "L", new Vector2(88f, 0f), new Vector2(34f, 32f));
        mainButton.onClick.AddListener(() =>
        {
            if (loadout.TryBindDraftSlotToRole(capturedSlotId, ToolActionRole.Main))
            {
                Refresh();
            }
        });

        Button subButton = CreateButton("SubButton", row, "R", new Vector2(126f, 0f), new Vector2(34f, 32f));
        subButton.onClick.AddListener(() =>
        {
            if (loadout.TryBindDraftSlotToRole(capturedSlotId, ToolActionRole.Sub))
            {
                Refresh();
            }
        });

        Button clearButton = CreateButton("ClearButton", row, "X", new Vector2(164f, 0f), new Vector2(34f, 32f));
        clearButton.onClick.AddListener(() =>
        {
            if (loadout.TryClearDraftSlotTool(capturedSlotId))
            {
                Refresh();
            }
        });
    }

    private void CreateOwnedToolButton(MiningTool tool, Vector2 position)
    {
        if (tool == null)
        {
            return;
        }

        Button button = CreateButton("OwnedToolButton", contentRoot, tool.ToolName, position, new Vector2(240f, 34f));
        button.onClick.AddListener(() =>
        {
            if (string.IsNullOrEmpty(selectedSlotId))
            {
                SetStatus("Select a slot before choosing a tool.");
                return;
            }

            if (loadout.TrySetDraftSlotTool(selectedSlotId, tool))
            {
                Refresh();
            }
        });
    }

    private void HandleCommitClicked()
    {
        if (!loadout.CommitDraft())
        {
            SetStatus("Commit failed.");
            return;
        }

        SetStatus("Committed.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void EnsureCloseButtonLabel()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.name != "CloseButton")
            {
                continue;
            }

            if (button.GetComponentInChildren<Text>(true) != null)
            {
                return;
            }

            RectTransform buttonTransform = button.GetComponent<RectTransform>();
            if (buttonTransform != null)
            {
                CreateText("Label", buttonTransform, "X", Vector2.zero, buttonTransform.sizeDelta, 16, TextAnchor.MiddleCenter);
            }

            return;
        }
    }

    private static RectTransform CreatePanelChild(string childName, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        return rectTransform;
    }

    private static Text CreateText(
        string objectName,
        RectTransform parent,
        string text,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAnchor alignment)
    {
        RectTransform rectTransform = CreatePanelChild(objectName, parent, anchoredPosition, size);
        Text label = rectTransform.gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        return label;
    }

    private static Button CreateButton(string objectName, RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rectTransform = CreatePanelChild(objectName, parent, anchoredPosition, size);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.24f, 0.28f, 1f);
        Button button = rectTransform.gameObject.AddComponent<Button>();
        CreateText("Label", rectTransform, label, Vector2.zero, size, 14, TextAnchor.MiddleCenter);
        return button;
    }

    private static RectTransform EnsureRectTransform(GameObject target)
    {
        RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            return rectTransform;
        }

        return target.AddComponent<RectTransform>();
    }

    private static Image EnsureImage(GameObject target)
    {
        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            return image;
        }

        return target.AddComponent<Image>();
    }

    private static void ClearChildren(Transform parent)
    {
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            children.Add(parent.GetChild(i).gameObject);
        }

        for (int i = 0; i < children.Count; i++)
        {
            Destroy(children[i]);
        }
    }

    private static void DestroyNamedChildren(Transform parent, string childName)
    {
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                children.Add(child.gameObject);
            }
        }

        for (int i = 0; i < children.Count; i++)
        {
            Destroy(children[i]);
        }
    }
}
