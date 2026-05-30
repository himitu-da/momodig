using UnityEngine;
using System.Collections.Generic;
using TMPro;

public enum MinecartState
{
    Following,
    GoingToGround,
    Unloading
}

public class Minecart
{
    public GameObject gameObject;
    public MinecartMovement movement;
    public float time;
    public MinecartState state;
    public TextMeshProUGUI capacityText;

    private readonly Queue<VoxelItemData> items = new Queue<VoxelItemData>();

    public Minecart(GameObject obj)
    {
        gameObject = obj;
        movement = obj.GetComponent<MinecartMovement>();
        if (movement == null)
        {
            movement = obj.AddComponent<MinecartMovement>();
        }

        time = 0f;
        state = MinecartState.Following;
    }

    public int CurrentLoad => items.Count;

    public bool HasCapacity(int capacity)
    {
        return items.Count < capacity;
    }

    public bool AddItem(VoxelItemData itemData, int capacity)
    {
        if (itemData == null || !itemData.IsValid("Minecart.AddItem"))
        {
            return false;
        }

        if (!HasCapacity(capacity))
        {
            return false;
        }

        items.Enqueue(itemData.Clone());
        return true;
    }

    public bool TryDrainItems(out List<VoxelItemData> drainedItems)
    {
        drainedItems = new List<VoxelItemData>();
        foreach (VoxelItemData item in items)
        {
            if (item == null || !item.IsValid("Minecart.TryDrainItems"))
            {
                drainedItems.Clear();
                return false;
            }

            drainedItems.Add(item.Clone());
        }

        items.Clear();
        return true;
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

    public void ClearItems()
    {
        items.Clear();
    }
}
