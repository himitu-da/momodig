using UnityEngine;
using UnityEngine.EventSystems;

public class MiningEntranceUnassignedDropArea : MonoBehaviour, IDropHandler
{
    private MiningEntranceInventoryPanel owner;

    public void Initialize(MiningEntranceInventoryPanel owner)
    {
        if (owner == null)
        {
            Debug.LogError($"MiningEntranceUnassignedDropArea '{name}': owner is not configured.", this);
            return;
        }

        this.owner = owner;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.DropOnUnassignedGrid();
        }
    }
}
