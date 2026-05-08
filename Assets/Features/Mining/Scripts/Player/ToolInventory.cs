using System;
using System.Collections.Generic;
using UnityEngine;

public enum ToolActionRole
{
    Main,
    Sub
}

[Serializable]
public class ToolSlot
{
    [SerializeField] private string slotId;
    [SerializeField] private MiningTool tool;

    public string SlotId => slotId;
    public MiningTool Tool => tool;

    public ToolSlot(string slotId, MiningTool tool)
    {
        this.slotId = slotId;
        this.tool = tool;
    }

    public void SetSlotId(string newSlotId)
    {
        slotId = newSlotId;
    }

    public void SetTool(MiningTool newTool)
    {
        tool = newTool;
    }
}

public class ToolInventory : MonoBehaviour
{
    [SerializeField] private List<ToolSlot> slots = new List<ToolSlot>();
    [SerializeField] private string mainSlotId = "slot_0";
    [SerializeField] private string subSlotId = "slot_1";
    [SerializeField] private bool allowSameSlotForRoles = false;
    [SerializeField] private bool persistToGameData = true;

    public event Action OnSlotsChanged;
    public event Action OnRoleBindingsChanged;

    public IReadOnlyList<ToolSlot> Slots => slots;
    public string MainSlotId => mainSlotId;
    public string SubSlotId => subSlotId;
    public MiningTool MainTool => GetToolInSlot(mainSlotId);
    public MiningTool SubTool => GetToolInSlot(subSlotId);

    public void EnsureInitializedFromTools(IList<MiningTool> tools, MiningTool preferredMainTool, MiningTool preferredSubTool)
    {
        EnsureSlotList();

        string oldMainSlotId = mainSlotId;
        string oldSubSlotId = subSlotId;
        bool loadedFromPersistence = TryLoadFromGameData();
        bool slotsChanged = loadedFromPersistence || EnsureSlotIds();
        bool slotsWereEmpty = slots.Count == 0;

        if (slots.Count == 0 && tools != null)
        {
            for (int i = 0; i < tools.Count; i++)
            {
                slots.Add(new ToolSlot(BuildDefaultSlotId(i), tools[i]));
            }

            slotsChanged = tools.Count > 0;
        }

        if (!slotsWereEmpty && tools != null)
        {
            slotsChanged = AppendMissingTools(tools) || slotsChanged;
        }

        if (slotsWereEmpty && slotsChanged)
        {
            string preferredMainSlotId = FindSlotIdForTool(preferredMainTool);
            string preferredSubSlotId = FindSlotIdForTool(preferredSubTool);

            if (!string.IsNullOrEmpty(preferredMainSlotId))
            {
                mainSlotId = preferredMainSlotId;
            }

            if (!string.IsNullOrEmpty(preferredSubSlotId))
            {
                subSlotId = preferredSubSlotId;
            }
        }

        bool rolesChanged = EnsureRoleBindings(preferredMainTool, preferredSubTool);
        rolesChanged = rolesChanged || mainSlotId != oldMainSlotId || subSlotId != oldSubSlotId;

        SaveToGameData();

        if (slotsChanged)
        {
            OnSlotsChanged?.Invoke();
        }

        if (rolesChanged)
        {
            OnRoleBindingsChanged?.Invoke();
        }
    }

    public MiningTool GetToolInSlot(string slotId)
    {
        ToolSlot slot = FindSlot(slotId);
        return slot != null ? slot.Tool : null;
    }

    public bool SetSlotTool(string slotId, MiningTool tool)
    {
        ToolSlot slot = FindSlot(slotId);
        if (slot == null)
        {
            return false;
        }

        if (slot.Tool == tool)
        {
            return true;
        }

        slot.SetTool(tool);
        SaveToGameData();
        OnSlotsChanged?.Invoke();
        return true;
    }

    public bool MoveTool(string fromSlotId, string toSlotId, bool swapIfOccupied = true)
    {
        ToolSlot fromSlot = FindSlot(fromSlotId);
        ToolSlot toSlot = FindSlot(toSlotId);
        if (fromSlot == null || toSlot == null || fromSlot == toSlot)
        {
            return false;
        }

        MiningTool movingTool = fromSlot.Tool;
        if (movingTool == null)
        {
            return false;
        }

        if (toSlot.Tool != null && !swapIfOccupied)
        {
            return false;
        }

        MiningTool targetTool = toSlot.Tool;
        toSlot.SetTool(movingTool);
        fromSlot.SetTool(swapIfOccupied ? targetTool : null);
        SaveToGameData();
        OnSlotsChanged?.Invoke();
        return true;
    }

    public bool SwapSlots(string firstSlotId, string secondSlotId)
    {
        ToolSlot firstSlot = FindSlot(firstSlotId);
        ToolSlot secondSlot = FindSlot(secondSlotId);
        if (firstSlot == null || secondSlot == null || firstSlot == secondSlot)
        {
            return false;
        }

        MiningTool firstTool = firstSlot.Tool;
        firstSlot.SetTool(secondSlot.Tool);
        secondSlot.SetTool(firstTool);
        SaveToGameData();
        OnSlotsChanged?.Invoke();
        return true;
    }

    public bool BindSlotToRole(string slotId, ToolActionRole role)
    {
        if (FindSlot(slotId) == null)
        {
            return false;
        }

        string oldMainSlotId = mainSlotId;
        string oldSubSlotId = subSlotId;

        switch (role)
        {
            case ToolActionRole.Main:
                mainSlotId = slotId;
                if (!allowSameSlotForRoles && subSlotId == slotId)
                {
                    subSlotId = oldMainSlotId;
                }
                break;
            case ToolActionRole.Sub:
                subSlotId = slotId;
                if (!allowSameSlotForRoles && mainSlotId == slotId)
                {
                    mainSlotId = oldSubSlotId;
                }
                break;
            default:
                return false;
        }

        if (mainSlotId == oldMainSlotId && subSlotId == oldSubSlotId)
        {
            return true;
        }

        SaveToGameData();
        OnRoleBindingsChanged?.Invoke();
        return true;
    }

    public List<MiningTool> GetAllTools()
    {
        EnsureSlotList();

        List<MiningTool> tools = new List<MiningTool>();
        foreach (ToolSlot slot in slots)
        {
            if (slot != null && slot.Tool != null && !tools.Contains(slot.Tool))
            {
                tools.Add(slot.Tool);
            }
        }

        return tools;
    }

    private bool TryLoadFromGameData()
    {
        if (!persistToGameData)
        {
            return false;
        }

        GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
        if (persistence == null || !persistence.hasToolInventoryData)
        {
            return false;
        }

        EnsureSlotList();
        slots.Clear();

        if (persistence.toolSlots != null)
        {
            foreach (ToolSlotPersistenceData savedSlot in persistence.toolSlots)
            {
                if (savedSlot == null)
                {
                    continue;
                }

                slots.Add(new ToolSlot(savedSlot.slotId, savedSlot.tool));
            }
        }

        mainSlotId = persistence.mainToolSlotId;
        subSlotId = persistence.subToolSlotId;
        EnsureSlotIds();
        return true;
    }

    private void SaveToGameData()
    {
        if (!persistToGameData)
        {
            return;
        }

        GameDataPersistenceManager persistence = GameDataPersistenceManager.Instance;
        if (persistence == null)
        {
            return;
        }

        persistence.hasToolInventoryData = true;
        persistence.mainToolSlotId = mainSlotId;
        persistence.subToolSlotId = subSlotId;

        if (persistence.toolSlots == null)
        {
            persistence.toolSlots = new List<ToolSlotPersistenceData>();
        }

        persistence.toolSlots.Clear();
        EnsureSlotList();

        foreach (ToolSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            persistence.toolSlots.Add(new ToolSlotPersistenceData
            {
                slotId = slot.SlotId,
                tool = slot.Tool
            });
        }
    }

    private bool AppendMissingTools(IList<MiningTool> tools)
    {
        bool changed = false;
        for (int i = 0; i < tools.Count; i++)
        {
            MiningTool tool = tools[i];
            if (tool == null || !string.IsNullOrEmpty(FindSlotIdForTool(tool)))
            {
                continue;
            }

            slots.Add(new ToolSlot(BuildNextAvailableSlotId(), tool));
            changed = true;
        }

        return changed;
    }

    private bool EnsureRoleBindings(MiningTool preferredMainTool, MiningTool preferredSubTool)
    {
        string oldMainSlotId = mainSlotId;
        string oldSubSlotId = subSlotId;

        if (!HasSlot(mainSlotId))
        {
            mainSlotId = FindSlotIdForTool(preferredMainTool);
            if (string.IsNullOrEmpty(mainSlotId))
            {
                mainSlotId = slots.Count > 0 ? slots[0].SlotId : string.Empty;
            }
        }

        if (!HasSlot(subSlotId))
        {
            subSlotId = FindSlotIdForTool(preferredSubTool);
            if (string.IsNullOrEmpty(subSlotId))
            {
                subSlotId = FindFirstSlotIdExcept(mainSlotId);
            }
        }

        if (!allowSameSlotForRoles && mainSlotId == subSlotId)
        {
            string alternateSubSlotId = FindFirstSlotIdExcept(mainSlotId);
            if (!string.IsNullOrEmpty(alternateSubSlotId))
            {
                subSlotId = alternateSubSlotId;
            }
        }

        return mainSlotId != oldMainSlotId || subSlotId != oldSubSlotId;
    }

    private bool EnsureSlotIds()
    {
        EnsureSlotList();

        bool changed = false;
        HashSet<string> usedIds = new HashSet<string>();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new ToolSlot(BuildDefaultSlotId(i), null);
                changed = true;
            }

            string slotId = slots[i].SlotId;
            if (string.IsNullOrWhiteSpace(slotId) || usedIds.Contains(slotId))
            {
                slotId = BuildDefaultSlotId(i);
                int suffix = i;
                while (usedIds.Contains(slotId))
                {
                    suffix++;
                    slotId = BuildDefaultSlotId(suffix);
                }

                slots[i].SetSlotId(slotId);
                changed = true;
            }

            usedIds.Add(slotId);
        }

        return changed;
    }

    private ToolSlot FindSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
        {
            return null;
        }

        EnsureSlotList();
        foreach (ToolSlot slot in slots)
        {
            if (slot != null && slot.SlotId == slotId)
            {
                return slot;
            }
        }

        return null;
    }

    private bool HasSlot(string slotId)
    {
        return FindSlot(slotId) != null;
    }

    private string FindSlotIdForTool(MiningTool tool)
    {
        if (tool == null)
        {
            return string.Empty;
        }

        EnsureSlotList();
        foreach (ToolSlot slot in slots)
        {
            if (slot != null && slot.Tool == tool)
            {
                return slot.SlotId;
            }
        }

        return string.Empty;
    }

    private string FindFirstSlotIdExcept(string excludedSlotId)
    {
        EnsureSlotList();
        foreach (ToolSlot slot in slots)
        {
            if (slot != null && slot.SlotId != excludedSlotId)
            {
                return slot.SlotId;
            }
        }

        return string.Empty;
    }

    private void EnsureSlotList()
    {
        if (slots == null)
        {
            slots = new List<ToolSlot>();
        }
    }

    private string BuildNextAvailableSlotId()
    {
        EnsureSlotList();

        int index = slots.Count;
        string slotId = BuildDefaultSlotId(index);
        while (HasSlot(slotId))
        {
            index++;
            slotId = BuildDefaultSlotId(index);
        }

        return slotId;
    }

    private static string BuildDefaultSlotId(int index)
    {
        return $"slot_{index}";
    }
}
