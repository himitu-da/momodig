using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MiningEntranceToolTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Text toolLabel;
    [SerializeField] private Image toolIconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    private MiningEntranceInventoryPanel owner;
    private MiningTool tool;

    public RectTransform RectTransform => (RectTransform)transform;

    public void Bind(MiningEntranceInventoryPanel owner, MiningTool tool)
    {
        if (owner == null || tool == null || toolLabel == null || toolIconImage == null || canvasGroup == null)
        {
            Debug.LogError($"MiningEntranceToolTile '{name}': cannot bind because required references are missing.", this);
            return;
        }

        this.owner = owner;
        this.tool = tool;
        toolLabel.text = tool.ToolName;
        toolIconImage.sprite = tool.ToolIcon;
        toolIconImage.enabled = tool.ToolIcon != null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || tool == null)
        {
            return;
        }

        transform.SetAsLastSibling();
        owner.BeginDrag(MiningEntranceInventoryPanel.DragPayload.FromUnassignedTool(tool), canvasGroup);
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
}
