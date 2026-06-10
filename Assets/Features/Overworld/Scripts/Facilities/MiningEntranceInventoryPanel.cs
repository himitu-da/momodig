using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiningEntranceInventoryPanel : FacilityPanel, IMiningEntrancePanelRuntimeBinding
{
    [Header("Grid References")]
    [SerializeField] private RectTransform assignedGridRoot;
    [SerializeField] private RectTransform unassignedGridRoot;
    [SerializeField] private MiningEntranceUnassignedDropArea unassignedDropArea;

    [Header("Tile Prefabs")]
    [SerializeField] private MiningEntranceAssignedSlotTile assignedSlotPrefab;
    [SerializeField] private MiningEntranceToolTile unassignedToolPrefab;

    [Header("Controls")]
    [SerializeField] private Text statusText;
    [SerializeField] private Button commitButton;

    [Header("Grid Layout")]
    [SerializeField] private Vector2 assignedGridSpacing = new Vector2(14f, 0f);
    [SerializeField] private int assignedGridColumns = 3;
    [SerializeField] private Vector2 unassignedGridSpacing = new Vector2(10f, 10f);
    [SerializeField] private int unassignedGridColumns = 4;

    private PlayerInventoryLoadout loadout;
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
        bool isValid = true;
        if (loadout == null)
        {
            Debug.LogError($"MiningEntranceInventoryPanel '{name}': PlayerInventoryLoadout is not bound.", this);
            isValid = false;
        }

        isValid = ValidateReference(assignedGridRoot, nameof(assignedGridRoot)) && isValid;
        isValid = ValidateReference(unassignedGridRoot, nameof(unassignedGridRoot)) && isValid;
        isValid = ValidateReference(unassignedDropArea, nameof(unassignedDropArea)) && isValid;
        isValid = ValidateReference(assignedSlotPrefab, nameof(assignedSlotPrefab)) && isValid;
        isValid = ValidateReference(unassignedToolPrefab, nameof(unassignedToolPrefab)) && isValid;
        isValid = ValidateReference(statusText, nameof(statusText)) && isValid;
        isValid = ValidateReference(commitButton, nameof(commitButton)) && isValid;
        return isValid;
    }

    protected override void OnInitialized()
    {
        commitButton.onClick.AddListener(HandleCommitClicked);
        unassignedDropArea.Initialize(this);

        if (!loadout.BeginDraft())
        {
            SetStatus("Loadout could not be loaded.");
            return;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (commitButton != null)
        {
            commitButton.onClick.RemoveListener(HandleCommitClicked);
        }
    }

    private void Refresh()
    {
        ClearChildren(assignedGridRoot);
        ClearChildren(unassignedGridRoot);

        Vector2 assignedCellSize = assignedSlotPrefab.RectTransform.rect.size;
        IReadOnlyList<ToolSlotPersistenceData> slots = loadout.DraftToolSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            MiningEntranceAssignedSlotTile tile = Instantiate(assignedSlotPrefab, assignedGridRoot, false);
            SetGridPosition(tile.RectTransform, i, assignedCellSize, assignedGridSpacing, assignedGridColumns);
            tile.Bind(this, slots[i], i, GetRoleLabel(slots[i].slotId));
        }

        Vector2 unassignedCellSize = unassignedToolPrefab.RectTransform.rect.size;
        int unassignedIndex = 0;
        List<MiningTool> ownedTools = loadout.GetOwnedTools();
        for (int i = 0; i < ownedTools.Count; i++)
        {
            MiningTool tool = ownedTools[i];
            if (tool == null || loadout.IsDraftToolAssigned(tool))
            {
                continue;
            }

            MiningEntranceToolTile tile = Instantiate(unassignedToolPrefab, unassignedGridRoot, false);
            SetGridPosition(tile.RectTransform, unassignedIndex, unassignedCellSize, unassignedGridSpacing, unassignedGridColumns);
            tile.Bind(this, tool);
            unassignedIndex++;
        }

        SetStatus("Ready.");
    }

    public void BeginDrag(DragPayload payload, CanvasGroup canvasGroup)
    {
        activeDrag = payload;
        activeDragCanvasGroup = canvasGroup;
        if (activeDragCanvasGroup != null)
        {
            activeDragCanvasGroup.alpha = 0.55f;
            activeDragCanvasGroup.blocksRaycasts = false;
        }
    }

    public void EndDrag()
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

    public void DropOnAssignedSlot(string targetSlotId)
    {
        if (activeDrag == null)
        {
            return;
        }

        if (activeDrag.SourceKind == DragSourceKind.AssignedSlot)
        {
            loadout.TrySwapDraftSlotTools(activeDrag.SourceSlotId, targetSlotId);
        }
        else if (activeDrag.SourceKind == DragSourceKind.UnassignedTool)
        {
            loadout.TryAssignDraftToolToSlot(activeDrag.Tool, targetSlotId);
        }
    }

    public void DropOnUnassignedGrid()
    {
        if (activeDrag == null || activeDrag.SourceKind != DragSourceKind.AssignedSlot)
        {
            return;
        }

        loadout.TryClearDraftSlotTool(activeDrag.SourceSlotId);
    }

    public void BindMainSlot(string slotId)
    {
        if (loadout.TryBindDraftSlotToRole(slotId, ToolActionRole.Main))
        {
            Refresh();
        }
    }

    public void BindSubSlot(string slotId)
    {
        if (loadout.TryBindDraftSlotToRole(slotId, ToolActionRole.Sub))
        {
            Refresh();
        }
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
        statusText.text = message;
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

    private static void SetGridPosition(RectTransform target, int index, Vector2 cellSize, Vector2 spacing, int columns)
    {
        int safeColumns = Mathf.Max(1, columns);
        int column = index % safeColumns;
        int row = index / safeColumns;
        target.anchorMin = new Vector2(0f, 1f);
        target.anchorMax = new Vector2(0f, 1f);
        target.pivot = new Vector2(0f, 1f);
        target.sizeDelta = cellSize;
        target.anchoredPosition = new Vector2(
            column * (cellSize.x + spacing.x),
            -row * (cellSize.y + spacing.y));
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

    private bool ValidateReference(Object value, string fieldName)
    {
        if (value != null)
        {
            return true;
        }

        Debug.LogError($"MiningEntranceInventoryPanel '{name}': {fieldName} is not configured.", this);
        return false;
    }

    public enum DragSourceKind
    {
        UnassignedTool,
        AssignedSlot
    }

    public sealed class DragPayload
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
}
