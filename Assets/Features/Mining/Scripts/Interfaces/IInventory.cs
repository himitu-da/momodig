using System;
using System.Collections.Generic;

public interface IInventory
{
    int maxCapacity { get; }

    bool CanAddItem(VoxelItemData itemData);
    bool AddItem(VoxelItemData itemData);
    bool TryPeekNextItem(out VoxelItemData itemData);
    bool TryRemoveNextItem(out VoxelItemData itemData);
    bool TryDrainAllItems(out List<VoxelItemData> itemData);

    int GetTotalItemCount();
    int GetResourceCount(ResourceType type);
    bool IsEmpty();
    Dictionary<ResourceType, int> GetAllResources();

    event Action<ResourceType, int> OnResourceAdded;
    event Action<int> OnTotalCountChanged;
}
