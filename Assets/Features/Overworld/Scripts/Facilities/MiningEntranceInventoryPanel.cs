using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MiningEntranceInventoryPanel : FacilityPanel, IMiningEntrancePanelRuntimeBinding
{
    private const float PanelWidth = 760f;
    private const float PanelHeight = 520f;

    private PlayerInventoryLoadout loadout;
    private RectTransform contentRoot;
    private RectTransform assignedGridRoot;
    private RectTransform unassignedGridRoot;
    private Text statusText;
    private DragPayload activeDrag;
    private CanvasGroup activeDragCanvasGroup;

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

        contentRoot = CreatePanelChild("Content", panel, Vector2.zero, new Vector2(PanelWidth - 32f, PanelHeight - 32f));
        CreateText("Title", contentRoot, "Mining Entrance", new Vector2(0f, 224f), new Vector2(PanelWidth - 64f, 34f), 24, TextAnchor.MiddleLeft);

        CreateText("AssignedLabel", contentRoot, "Assigned Tools", new Vector2(-230f, 174f), new Vector2(280f, 24f), 16, TextAnchor.MiddleLeft);
        assignedGridRoot = CreateGridArea(
            "AssignedGrid",
            contentRoot,
            new Vector2(0f, 112f),
            new Vector2(680f, 104f),
            new Vector2(206f, 88f),
            new Vector2(14f, 0f),
            3);

        CreateText("UnassignedLabel", contentRoot, "Unassigned Tools", new Vector2(-230f, 28f), new Vector2(280f, 24f), 16, TextAnchor.MiddleLeft);
        unassignedGridRoot = CreateGridArea(
            "UnassignedGrid",
            contentRoot,
            new Vector2(0f, -74f),
            new Vector2(680f, 172f),
            new Vector2(154f, 56f),
            new Vector2(10f, 10f),
            4);
        DropZone unassignedDropZone = unassignedGridRoot.gameObject.AddComponent<DropZone>();
        unassignedDropZone.Initialize(this, string.Empty, true);

        statusText = CreateText("Status", contentRoot, "", new Vector2(-118f, -226f), new Vector2(480f, 28f), 14, TextAnchor.MiddleLeft);

        Button commitButton = CreateButton("CommitButton", contentRoot, "Commit", new Vector2(258f, -226f), new Vector2(104f, 32f));
        commitButton.onClick.AddListener(HandleCommitClicked);
    }

    private void Refresh()
    {
        if (assignedGridRoot == null || unassignedGridRoot == null)
        {
            return;
        }

        ClearChildren(assignedGridRoot);
        ClearChildren(unassignedGridRoot);

        IReadOnlyList<ToolSlotPersistenceData> slots = loadout.DraftToolSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            CreateAssignedSlotTile(slots[i], i);
        }

        List<MiningTool> ownedTools = loadout.GetOwnedTools();
        for (int i = 0; i < ownedTools.Count; i++)
        {
            MiningTool tool = ownedTools[i];
            if (tool != null && !loadout.IsDraftToolAssigned(tool))
            {
                CreateUnassignedToolTile(tool);
            }
        }

        SetStatus("Ready.");
    }

    private void CreateAssignedSlotTile(ToolSlotPersistenceData slot, int index)
    {
        if (slot == null)
        {
            return;
        }

        RectTransform tile = CreateGridChild("AssignedSlotTile", assignedGridRoot);
        Image background = tile.gameObject.AddComponent<Image>();
        background.color = new Color(0.16f, 0.18f, 0.2f, 1f);

        DropZone dropZone = tile.gameObject.AddComponent<DropZone>();
        dropZone.Initialize(this, slot.slotId, false);

        if (slot.tool != null)
        {
            DragSource dragSource = tile.gameObject.AddComponent<DragSource>();
            dragSource.Initialize(this, DragPayload.FromAssignedSlot(slot.slotId, slot.tool));
        }

        CreateText("SlotLabel", tile, $"Slot {index + 1}", new Vector2(-52f, 28f), new Vector2(92f, 20f), 13, TextAnchor.MiddleLeft);
        CreateText("ToolLabel", tile, GetToolLabel(slot.tool), new Vector2(-20f, 4f), new Vector2(146f, 24f), 14, TextAnchor.MiddleLeft);
        CreateText("RoleLabel", tile, GetRoleLabel(slot.slotId), new Vector2(62f, 28f), new Vector2(46f, 20f), 13, TextAnchor.MiddleRight);

        Button mainButton = CreateButton("MainButton", tile, "L", new Vector2(48f, -26f), new Vector2(32f, 24f));
        string capturedSlotId = slot.slotId;
        mainButton.onClick.AddListener(() =>
        {
            if (loadout.TryBindDraftSlotToRole(capturedSlotId, ToolActionRole.Main))
            {
                Refresh();
            }
        });

        Button subButton = CreateButton("SubButton", tile, "R", new Vector2(86f, -26f), new Vector2(32f, 24f));
        subButton.onClick.AddListener(() =>
        {
            if (loadout.TryBindDraftSlotToRole(capturedSlotId, ToolActionRole.Sub))
            {
                Refresh();
            }
        });
    }

    private void CreateUnassignedToolTile(MiningTool tool)
    {
        RectTransform tile = CreateGridChild("UnassignedToolTile", unassignedGridRoot);
        Image background = tile.gameObject.AddComponent<Image>();
        background.color = new Color(0.2f, 0.24f, 0.28f, 1f);

        DragSource dragSource = tile.gameObject.AddComponent<DragSource>();
        dragSource.Initialize(this, DragPayload.FromUnassignedTool(tool));

        CreateText("ToolLabel", tile, GetToolLabel(tool), Vector2.zero, new Vector2(138f, 44f), 14, TextAnchor.MiddleCenter);
    }

    private void HandleDrop(DragPayload payload, string targetSlotId, bool isUnassignedTarget)
    {
        if (payload == null)
        {
            return;
        }

        bool changed = false;
        if (isUnassignedTarget)
        {
            if (payload.SourceKind == DragSourceKind.AssignedSlot)
            {
                changed = loadout.TryClearDraftSlotTool(payload.SourceSlotId);
            }
        }
        else if (payload.SourceKind == DragSourceKind.AssignedSlot)
        {
            changed = loadout.TrySwapDraftSlotTools(payload.SourceSlotId, targetSlotId);
        }
        else if (payload.SourceKind == DragSourceKind.UnassignedTool)
        {
            changed = loadout.TryAssignDraftToolToSlot(payload.Tool, targetSlotId);
        }

        if (changed)
        {
            Refresh();
        }
    }

    private void BeginDrag(DragPayload payload, CanvasGroup canvasGroup)
    {
        activeDrag = payload;
        activeDragCanvasGroup = canvasGroup;
        if (activeDragCanvasGroup != null)
        {
            activeDragCanvasGroup.alpha = 0.55f;
            activeDragCanvasGroup.blocksRaycasts = false;
        }
    }

    private void EndDrag()
    {
        if (activeDragCanvasGroup != null)
        {
            activeDragCanvasGroup.alpha = 1f;
            activeDragCanvasGroup.blocksRaycasts = true;
        }

        activeDrag = null;
        activeDragCanvasGroup = null;
        Refresh();
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

    private string GetRoleLabel(string slotId)
    {
        string role = string.Empty;
        if (slotId == loadout.DraftMainToolSlotId)
        {
            role = "L";
        }

        if (slotId == loadout.DraftSubToolSlotId)
        {
            role = string.IsNullOrEmpty(role) ? "R" : "L/R";
        }

        return role;
    }

    private static string GetToolLabel(MiningTool tool)
    {
        return tool != null ? tool.ToolName : "Empty";
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

    private static RectTransform CreateGridArea(
        string childName,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 cellSize,
        Vector2 spacing,
        int constraintCount)
    {
        RectTransform grid = CreatePanelChild(childName, parent, anchoredPosition, size);
        Image background = grid.gameObject.AddComponent<Image>();
        background.color = new Color(0.11f, 0.12f, 0.13f, 1f);

        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = cellSize;
        layout.spacing = spacing;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = constraintCount;
        return grid;
    }

    private static RectTransform CreateGridChild(string childName, RectTransform parent)
    {
        GameObject child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return rectTransform;
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
        label.raycastTarget = false;
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

    private enum DragSourceKind
    {
        UnassignedTool,
        AssignedSlot
    }

    private sealed class DragPayload
    {
        public DragSourceKind SourceKind { get; private set; }
        public MiningTool Tool { get; private set; }
        public string SourceSlotId { get; private set; }

        public static DragPayload FromUnassignedTool(MiningTool tool)
        {
            return new DragPayload
            {
                SourceKind = DragSourceKind.UnassignedTool,
                Tool = tool,
                SourceSlotId = string.Empty
            };
        }

        public static DragPayload FromAssignedSlot(string slotId, MiningTool tool)
        {
            return new DragPayload
            {
                SourceKind = DragSourceKind.AssignedSlot,
                Tool = tool,
                SourceSlotId = slotId
            };
        }
    }

    private sealed class DragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private MiningEntranceInventoryPanel panel;
        private DragPayload payload;
        private CanvasGroup canvasGroup;

        public void Initialize(MiningEntranceInventoryPanel panel, DragPayload payload)
        {
            this.panel = panel;
            this.payload = payload;
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            panel.BeginDrag(payload, canvasGroup);
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            panel.EndDrag();
        }
    }

    private sealed class DropZone : MonoBehaviour, IDropHandler
    {
        private MiningEntranceInventoryPanel panel;
        private string targetSlotId;
        private bool isUnassignedTarget;

        public void Initialize(MiningEntranceInventoryPanel panel, string targetSlotId, bool isUnassignedTarget)
        {
            this.panel = panel;
            this.targetSlotId = targetSlotId;
            this.isUnassignedTarget = isUnassignedTarget;
        }

        public void OnDrop(PointerEventData eventData)
        {
            panel.HandleDrop(panel.activeDrag, targetSlotId, isUnassignedTarget);
        }
    }
}
