using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MiningEntranceAssignedSlotTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Text slotLabel;
    [SerializeField] private Text toolLabel;
    [SerializeField] private Image toolIconImage;
    [SerializeField] private Text roleLabel;
    [SerializeField] private Button mainButton;
    [SerializeField] private Button subButton;
    [SerializeField] private CanvasGroup canvasGroup;

    private MiningEntranceInventoryPanel owner;
    private string slotId;
    private MiningTool tool;

    public RectTransform RectTransform => (RectTransform)transform;

    public void Bind(MiningEntranceInventoryPanel owner, ToolSlotPersistenceData slot, int slotIndex, string role)
    {
        if (!ValidateReferences() || owner == null || slot == null)
        {
            Debug.LogError($"MiningEntranceAssignedSlotTile '{name}': cannot bind because required data is missing.", this);
            return;
        }

        this.owner = owner;
        slotId = slot.slotId;
        tool = slot.tool;

        slotLabel.text = $"Slot {slotIndex + 1}";
        toolLabel.text = tool != null ? tool.ToolName : "Empty";
        toolIconImage.sprite = tool != null ? tool.ToolIcon : null;
        toolIconImage.enabled = tool != null && tool.ToolIcon != null;
        roleLabel.text = role;

        mainButton.onClick.RemoveAllListeners();
        mainButton.onClick.AddListener(() => owner.BindMainSlot(slotId));
        subButton.onClick.RemoveAllListeners();
        subButton.onClick.AddListener(() => owner.BindSubSlot(slotId));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || tool == null)
        {
            return;
        }

        transform.SetAsLastSibling();
        owner.BeginDrag(MiningEntranceInventoryPanel.DragPayload.FromAssignedSlot(slotId, tool), canvasGroup);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner == null || tool == null)
        {
            return;
        }

        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner != null && tool != null)
        {
            owner.EndDrag();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.DropOnAssignedSlot(slotId);
        }
    }

    private bool ValidateReferences()
    {
        return slotLabel != null &&
               toolLabel != null &&
               toolIconImage != null &&
               roleLabel != null &&
               mainButton != null &&
               subButton != null &&
               canvasGroup != null;
    }

}
