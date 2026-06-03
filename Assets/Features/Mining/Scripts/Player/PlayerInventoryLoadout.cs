using System.Collections.Generic;
using UnityEngine;

public class PlayerInventoryLoadout : MonoBehaviour
{
    [SerializeField] private StorageManager storageManager;
    [SerializeField] private GameDataPersistenceManager persistenceManager;

    private readonly List<ToolSlotPersistenceData> draftToolSlots = new List<ToolSlotPersistenceData>();
    private string draftMainToolSlotId = string.Empty;
    private string draftSubToolSlotId = string.Empty;

    public IReadOnlyList<ToolSlotPersistenceData> DraftToolSlots => draftToolSlots;
    public string DraftMainToolSlotId => draftMainToolSlotId;
    public string DraftSubToolSlotId => draftSubToolSlotId;

    private void Awake()
    {
        ResolveDependencies();
    }

    public bool BeginDraft()
    {
        if (!ResolveDependencies())
        {
            return false;
        }

        draftToolSlots.Clear();
        if (persistenceManager.hasToolInventoryData)
        {
            return LoadDraftFromPersistence();
        }

        return BuildDefaultDraftFromStorage();
    }

    public List<MiningTool> GetOwnedTools()
    {
        if (!ResolveDependencies())
        {
            return new List<MiningTool>();
        }

        return storageManager.GetOwnedTools();
    }

    public bool TrySetDraftSlotTool(string slotId, MiningTool tool)
    {
        if (!ValidateDraftSlotTool(slotId, tool))
        {
            return false;
        }

        ToolSlotPersistenceData slot = FindDraftSlot(slotId);
        if (slot == null)
        {
            Debug.LogError($"PlayerInventoryLoadout: slotId '{slotId}' does not exist in the draft.", this);
            return false;
        }

        slot.tool = tool;
        slot.toolId = GameDataPersistenceManager.GetToolId(tool);
        return true;
    }

    public bool TryClearDraftSlotTool(string slotId)
    {
        ToolSlotPersistenceData slot = FindDraftSlot(slotId);
        if (slot == null)
        {
            Debug.LogError($"PlayerInventoryLoadout: slotId '{slotId}' does not exist in the draft.", this);
            return false;
        }

        slot.tool = null;
        slot.toolId = string.Empty;
        return true;
    }

    public bool TryBindDraftSlotToRole(string slotId, ToolActionRole role)
    {
        if (FindDraftSlot(slotId) == null)
        {
            Debug.LogError($"PlayerInventoryLoadout: slotId '{slotId}' does not exist in the draft.", this);
            return false;
        }

        switch (role)
        {
            case ToolActionRole.Main:
                draftMainToolSlotId = slotId;
                if (draftSubToolSlotId == slotId)
                {
                    draftSubToolSlotId = FindFirstDraftSlotIdExcept(slotId);
                }
                return true;
            case ToolActionRole.Sub:
                draftSubToolSlotId = slotId;
                if (draftMainToolSlotId == slotId)
                {
                    draftMainToolSlotId = FindFirstDraftSlotIdExcept(slotId);
                }
                return true;
            default:
                Debug.LogError($"PlayerInventoryLoadout: unsupported role '{role}'.", this);
                return false;
        }
    }

    public bool CommitDraft()
    {
        if (!ResolveDependencies() || !ValidateDraft())
        {
            return false;
        }

        persistenceManager.hasToolInventoryData = true;
        persistenceManager.mainToolSlotId = draftMainToolSlotId;
        persistenceManager.subToolSlotId = draftSubToolSlotId;
        persistenceManager.toolSlots = CopyDraftSlots();
        return true;
    }

    private bool LoadDraftFromPersistence()
    {
        if (persistenceManager.toolSlots == null)
        {
            Debug.LogError("PlayerInventoryLoadout: persistence toolSlots is not configured.", this);
            return false;
        }

        for (int i = 0; i < persistenceManager.toolSlots.Count; i++)
        {
            ToolSlotPersistenceData savedSlot = persistenceManager.toolSlots[i];
            if (savedSlot == null)
            {
                Debug.LogError($"PlayerInventoryLoadout: persistence toolSlots contains a null record at index {i}.", this);
                return false;
            }

            MiningTool tool = ResolveOwnedTool(savedSlot.toolId, savedSlot.tool);
            if (!string.IsNullOrWhiteSpace(savedSlot.toolId) && tool == null)
            {
                return false;
            }

            draftToolSlots.Add(new ToolSlotPersistenceData
            {
                slotId = savedSlot.slotId,
                toolId = savedSlot.toolId,
                tool = tool
            });
        }

        draftMainToolSlotId = persistenceManager.mainToolSlotId;
        draftSubToolSlotId = persistenceManager.subToolSlotId;
        return ValidateDraft();
    }

    private bool BuildDefaultDraftFromStorage()
    {
        List<MiningTool> ownedTools = storageManager.GetOwnedTools();
        if (ownedTools.Count == 0)
        {
            Debug.LogError("PlayerInventoryLoadout: StorageManager has no owned tools.", this);
            return false;
        }

        for (int i = 0; i < ownedTools.Count; i++)
        {
            MiningTool tool = ownedTools[i];
            draftToolSlots.Add(new ToolSlotPersistenceData
            {
                slotId = BuildDefaultSlotId(i),
                toolId = GameDataPersistenceManager.GetToolId(tool),
                tool = tool
            });
        }

        draftMainToolSlotId = draftToolSlots[0].slotId;
        draftSubToolSlotId = draftToolSlots.Count > 1 ? draftToolSlots[1].slotId : string.Empty;
        return ValidateDraft();
    }

    private bool ValidateDraftSlotTool(string slotId, MiningTool tool)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            Debug.LogError("PlayerInventoryLoadout: slotId is not configured.", this);
            return false;
        }

        if (tool == null)
        {
            Debug.LogError("PlayerInventoryLoadout: tool is not configured.", this);
            return false;
        }

        if (!ResolveDependencies())
        {
            return false;
        }

        if (!storageManager.OwnsTool(tool))
        {
            Debug.LogError($"PlayerInventoryLoadout: tool '{GameDataPersistenceManager.GetToolId(tool)}' is not owned in StorageManager.", this);
            return false;
        }

        return true;
    }

    private bool ValidateDraft()
    {
        HashSet<string> slotIds = new HashSet<string>();
        for (int i = 0; i < draftToolSlots.Count; i++)
        {
            ToolSlotPersistenceData slot = draftToolSlots[i];
            if (slot == null)
            {
                Debug.LogError($"PlayerInventoryLoadout: draftToolSlots contains a null record at index {i}.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(slot.slotId))
            {
                Debug.LogError($"PlayerInventoryLoadout: draftToolSlots[{i}] has no slotId.", this);
                return false;
            }

            if (!slotIds.Add(slot.slotId))
            {
                Debug.LogError($"PlayerInventoryLoadout: duplicate draft slotId '{slot.slotId}'.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(slot.toolId))
            {
                continue;
            }

            MiningTool tool = ResolveOwnedTool(slot.toolId, slot.tool);
            if (tool == null)
            {
                return false;
            }

            slot.tool = tool;
        }

        if (!string.IsNullOrEmpty(draftMainToolSlotId) && !slotIds.Contains(draftMainToolSlotId))
        {
            Debug.LogError($"PlayerInventoryLoadout: main slot '{draftMainToolSlotId}' does not exist in draft slots.", this);
            return false;
        }

        if (!string.IsNullOrEmpty(draftSubToolSlotId) && !slotIds.Contains(draftSubToolSlotId))
        {
            Debug.LogError($"PlayerInventoryLoadout: sub slot '{draftSubToolSlotId}' does not exist in draft slots.", this);
            return false;
        }

        return true;
    }

    private MiningTool ResolveOwnedTool(string toolId, MiningTool fallbackTool)
    {
        if (fallbackTool != null)
        {
            if (!storageManager.OwnsTool(fallbackTool))
            {
                Debug.LogError($"PlayerInventoryLoadout: tool '{GameDataPersistenceManager.GetToolId(fallbackTool)}' is not owned in StorageManager.", this);
                return null;
            }

            return fallbackTool;
        }

        if (string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        List<MiningTool> ownedTools = storageManager.GetOwnedTools();
        for (int i = 0; i < ownedTools.Count; i++)
        {
            MiningTool tool = ownedTools[i];
            if (GameDataPersistenceManager.GetToolId(tool) == toolId)
            {
                return tool;
            }
        }

        Debug.LogError($"PlayerInventoryLoadout: toolId '{toolId}' is not owned in StorageManager.", this);
        return null;
    }

    private ToolSlotPersistenceData FindDraftSlot(string slotId)
    {
        for (int i = 0; i < draftToolSlots.Count; i++)
        {
            ToolSlotPersistenceData slot = draftToolSlots[i];
            if (slot != null && slot.slotId == slotId)
            {
                return slot;
            }
        }

        return null;
    }

    private string FindFirstDraftSlotIdExcept(string excludedSlotId)
    {
        for (int i = 0; i < draftToolSlots.Count; i++)
        {
            ToolSlotPersistenceData slot = draftToolSlots[i];
            if (slot != null && slot.slotId != excludedSlotId)
            {
                return slot.slotId;
            }
        }

        return string.Empty;
    }

    private List<ToolSlotPersistenceData> CopyDraftSlots()
    {
        List<ToolSlotPersistenceData> copy = new List<ToolSlotPersistenceData>();
        for (int i = 0; i < draftToolSlots.Count; i++)
        {
            ToolSlotPersistenceData slot = draftToolSlots[i];
            copy.Add(new ToolSlotPersistenceData
            {
                slotId = slot.slotId,
                toolId = !string.IsNullOrEmpty(slot.toolId) ? slot.toolId : GameDataPersistenceManager.GetToolId(slot.tool),
                tool = slot.tool
            });
        }

        return copy;
    }

    private bool ResolveDependencies()
    {
        bool isValid = true;

        if (storageManager == null)
        {
            Debug.LogError("PlayerInventoryLoadout: storageManager is not configured.", this);
            isValid = false;
        }

        if (persistenceManager == null)
        {
            persistenceManager = GameDataPersistenceManager.Instance;
        }

        if (persistenceManager == null)
        {
            Debug.LogError("PlayerInventoryLoadout: GameDataPersistenceManager is not initialized.", this);
            isValid = false;
        }

        return isValid;
    }

    private static string BuildDefaultSlotId(int index)
    {
        return $"slot_{index}";
    }
}
