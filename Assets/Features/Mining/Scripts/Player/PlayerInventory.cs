using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class PlayerInventory : IInventory
{
    private const float InventoryFullNotificationCooldownSeconds = 5f;

    [SerializeField] private int _maxCapacity = 200;
    private readonly Queue<VoxelItemData> items = new Queue<VoxelItemData>();

    public event Action<ResourceType, int> OnResourceAdded;
    public event Action<ResourceType, int> OnResourceRemoved;
    public event Action<int> OnTotalCountChanged;
    public event Action<bool> OnInventoryFullStateChanged;

    private float lastInventoryFullNotificationTime = float.NegativeInfinity;

    public int maxCapacity => _maxCapacity;

    public bool CanAddItem(VoxelItemData itemData)
    {
        return itemData != null && itemData.IsValid("PlayerInventory.CanAddItem") && GetTotalItemCount() < maxCapacity;
    }

    public bool AddItem(VoxelItemData itemData)
    {
        if (itemData == null || !itemData.IsValid("PlayerInventory.AddItem"))
        {
            return false;
        }

        if (!CanAddItem(itemData))
        {
            return false;
        }

        bool wasEmpty = IsEmpty();
        bool wasFull = IsFull();
        VoxelItemData storedItem = itemData.Clone();
        items.Enqueue(storedItem);

        OnResourceAdded?.Invoke(storedItem.resourceType, 1);
        OnTotalCountChanged?.Invoke(GetTotalItemCount());

        if (wasEmpty)
        {
            NotifyInventoryFullStateChanged(false);
        }

        if (!wasFull && IsFull())
        {
            NotifyInventoryFullStateChanged(true);
        }

        return true;
    }

    public bool TryPeekNextItem(out VoxelItemData itemData)
    {
        itemData = null;
        if (items.Count == 0)
        {
            return false;
        }

        VoxelItemData next = items.Peek();
        if (next == null || !next.IsValid("PlayerInventory.TryPeekNextItem"))
        {
            return false;
        }

        itemData = next.Clone();
        return true;
    }

    public bool TryRemoveNextItem(out VoxelItemData itemData)
    {
        itemData = null;
        if (!TryPeekNextItem(out VoxelItemData next))
        {
            return false;
        }

        bool wasFull = IsFull();
        items.Dequeue();
        itemData = next;

        OnResourceRemoved?.Invoke(itemData.resourceType, 1);
        OnTotalCountChanged?.Invoke(GetTotalItemCount());

        if (wasFull && !IsFull())
        {
            NotifyInventoryFullStateChanged(false);
        }

        return true;
    }

    public bool TryDrainAllItems(out List<VoxelItemData> itemData)
    {
        itemData = new List<VoxelItemData>();
        foreach (VoxelItemData item in items)
        {
            if (item == null || !item.IsValid("PlayerInventory.TryDrainAllItems"))
            {
                itemData.Clear();
                return false;
            }

            itemData.Add(item.Clone());
        }

        if (items.Count == 0)
        {
            return true;
        }

        Dictionary<ResourceType, int> removedCounts = GetAllResources();
        bool wasFull = IsFull();
        items.Clear();

        foreach (KeyValuePair<ResourceType, int> removed in removedCounts)
        {
            if (removed.Value > 0)
            {
                OnResourceRemoved?.Invoke(removed.Key, removed.Value);
            }
        }

        OnTotalCountChanged?.Invoke(0);
        if (wasFull)
        {
            NotifyInventoryFullStateChanged(false);
        }

        return true;
    }

    public int GetTotalItemCount()
    {
        return items.Count;
    }

    public int GetResourceCount(ResourceType type)
    {
        int count = 0;
        foreach (VoxelItemData item in items)
        {
            if (item != null && item.resourceType == type)
            {
                count++;
            }
        }

        return count;
    }

    public Dictionary<ResourceType, int> GetAllResources()
    {
        Dictionary<ResourceType, int> resourceCounts = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            resourceCounts[type] = 0;
        }

        foreach (VoxelItemData item in items)
        {
            if (item != null)
            {
                resourceCounts[item.resourceType]++;
            }
        }

        return resourceCounts;
    }

    public bool IsEmpty()
    {
        return items.Count == 0;
    }

    public bool IsFull()
    {
        return items.Count >= maxCapacity;
    }

    public void Clear()
    {
        if (items.Count == 0)
        {
            return;
        }

        bool wasFull = IsFull();
        Dictionary<ResourceType, int> removedCounts = GetAllResources();
        items.Clear();

        foreach (KeyValuePair<ResourceType, int> removed in removedCounts)
        {
            if (removed.Value > 0)
            {
                OnResourceRemoved?.Invoke(removed.Key, removed.Value);
            }
        }

        OnTotalCountChanged?.Invoke(0);
        if (wasFull)
        {
            NotifyInventoryFullStateChanged(false);
        }
    }

    private void NotifyInventoryFullStateChanged(bool isFull)
    {
        if (isFull)
        {
            if (Time.time - lastInventoryFullNotificationTime < InventoryFullNotificationCooldownSeconds)
            {
                return;
            }

            lastInventoryFullNotificationTime = Time.time;
        }

        OnInventoryFullStateChanged?.Invoke(isFull);
    }

    public override string ToString()
    {
        var result = $"Inventory ({GetTotalItemCount()}/{maxCapacity}): ";
        Dictionary<ResourceType, int> resourceCounts = GetAllResources();
        foreach (KeyValuePair<ResourceType, int> kvp in resourceCounts)
        {
            if (kvp.Value > 0)
            {
                result += $"{kvp.Key}:{kvp.Value} ";
            }
        }

        return result;
    }
}
