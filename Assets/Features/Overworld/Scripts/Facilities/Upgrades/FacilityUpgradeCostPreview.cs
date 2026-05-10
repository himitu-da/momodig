public class FacilityUpgradeCostPreview
{
    public FacilityUpgradeCostPreview(ResourceType resourceType, int requiredAmount, int ownedAmount)
    {
        ResourceType = resourceType;
        RequiredAmount = requiredAmount;
        OwnedAmount = ownedAmount;
    }

    public ResourceType ResourceType { get; }
    public int RequiredAmount { get; }
    public int OwnedAmount { get; }
    public bool HasEnough => OwnedAmount >= RequiredAmount;
}
