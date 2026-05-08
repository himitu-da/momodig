using System.Collections.Generic;
using UnityEngine;

public class ToolInventoryUI : MonoBehaviour
{
    [SerializeField] private MiningToolsController toolsController;
    [SerializeField] private ToolInventory toolInventory;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private ToolSlotUI slotPrefab;
    [SerializeField] private bool swapSlotsOnSecondClick = true;

    private readonly List<ToolSlotUI> slotViews = new List<ToolSlotUI>();
    private string selectedSlotId;
    private bool subscribedToInventory;

    private void Awake()
    {
        ResolveReferences();
        Rebuild();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToInventory();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
    }

    public void HandleSlotClicked(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
        {
            return;
        }

        if (string.IsNullOrEmpty(selectedSlotId))
        {
            selectedSlotId = slotId;
            Refresh();
            return;
        }

        if (selectedSlotId == slotId)
        {
            selectedSlotId = string.Empty;
            Refresh();
            return;
        }

        if (swapSlotsOnSecondClick)
        {
            toolsController?.SwapToolSlots(selectedSlotId, slotId);
        }
        else
        {
            toolsController?.MoveToolSlot(selectedSlotId, slotId);
        }

        selectedSlotId = string.Empty;
        Refresh();
    }

    public void BindSlotToMain(string slotId)
    {
        if (toolsController != null && toolsController.BindMainSlot(slotId))
        {
            selectedSlotId = string.Empty;
            Refresh();
        }
    }

    public void BindSlotToSub(string slotId)
    {
        if (toolsController != null && toolsController.BindSubSlot(slotId))
        {
            selectedSlotId = string.Empty;
            Refresh();
        }
    }

    public void Rebuild()
    {
        ResolveReferences();
        ClearViews();

        if (toolInventory == null || slotPrefab == null || slotContainer == null)
        {
            return;
        }

        foreach (ToolSlot slot in toolInventory.Slots)
        {
            if (slot == null)
            {
                continue;
            }

            ToolSlotUI view = Instantiate(slotPrefab, slotContainer);
            view.gameObject.SetActive(true);
            view.Initialize(HandleSlotClicked, BindSlotToMain, BindSlotToSub);
            slotViews.Add(view);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (toolInventory == null)
        {
            return;
        }

        IReadOnlyList<ToolSlot> slots = toolInventory.Slots;
        if (slotViews.Count != slots.Count)
        {
            Rebuild();
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            ToolSlot slot = slots[i];
            ToolSlotUI view = slotViews[i];
            if (slot == null || view == null)
            {
                continue;
            }

            view.Render(
                slot,
                selectedSlotId == slot.SlotId,
                toolInventory.MainSlotId == slot.SlotId,
                toolInventory.SubSlotId == slot.SlotId);
        }
    }

    private void ResolveReferences()
    {
        if (toolsController == null)
        {
            toolsController = FindFirstObjectByType<MiningToolsController>();
        }

        if (toolInventory == null && toolsController != null)
        {
            toolInventory = toolsController.ToolInventory;
        }

        if (slotContainer == null)
        {
            slotContainer = transform;
        }
    }

    private void SubscribeToInventory()
    {
        if (toolInventory == null || subscribedToInventory)
        {
            return;
        }

        toolInventory.OnSlotsChanged += Rebuild;
        toolInventory.OnRoleBindingsChanged += Refresh;
        subscribedToInventory = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (toolInventory == null || !subscribedToInventory)
        {
            return;
        }

        toolInventory.OnSlotsChanged -= Rebuild;
        toolInventory.OnRoleBindingsChanged -= Refresh;
        subscribedToInventory = false;
    }

    private void ClearViews()
    {
        foreach (ToolSlotUI view in slotViews)
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        slotViews.Clear();
    }
}
