using System;
using UnityEngine;

[Serializable]
public class FacilityResourceCost
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int baseAmount = 1;
    [SerializeField] private int amountPerLevel;

    public ResourceType ResourceType => resourceType;
    public int BaseAmount => baseAmount;
    public int AmountPerLevel => amountPerLevel;

    public bool ValidateConfiguration(UnityEngine.Object context)
    {
        if (baseAmount <= 0)
        {
            Debug.LogError($"FacilityResourceCost: baseAmount for '{resourceType}' must be greater than zero.", context);
            return false;
        }

        if (amountPerLevel < 0)
        {
            Debug.LogError($"FacilityResourceCost: amountPerLevel for '{resourceType}' must not be negative.", context);
            return false;
        }

        return true;
    }

    public int CalculateAmount(FacilityUpgradeCostScaling scaling, int effectLevelForCost)
    {
        if (scaling == FacilityUpgradeCostScaling.AddAmountPerLevel)
        {
            int level = Mathf.Max(1, effectLevelForCost);
            return checked(baseAmount + amountPerLevel * (level - 1));
        }

        int multiplier = CalculateMultiplier(scaling, effectLevelForCost);
        return checked(baseAmount * multiplier);
    }

    private static int CalculateMultiplier(FacilityUpgradeCostScaling scaling, int effectLevelForCost)
    {
        switch (scaling)
        {
            case FacilityUpgradeCostScaling.NoChange:
                return 1;
            case FacilityUpgradeCostScaling.MultiplyByNextLevel:
                return Mathf.Max(1, effectLevelForCost);
            case FacilityUpgradeCostScaling.FactorialByCurrentLevel:
                return Factorial(Mathf.Max(1, effectLevelForCost));
            case FacilityUpgradeCostScaling.FibonacciByCurrentLevel:
                return Fibonacci(Mathf.Max(1, effectLevelForCost));
            default:
                throw new ArgumentOutOfRangeException(nameof(scaling), scaling, "Unsupported facility upgrade cost scaling.");
        }
    }

    private static int Factorial(int value)
    {
        int result = 1;
        for (int i = 2; i <= value; i++)
        {
            result = checked(result * i);
        }

        return result;
    }

    private static int Fibonacci(int value)
    {
        if (value <= 2)
        {
            return 1;
        }

        int previous = 1;
        int current = 1;
        for (int i = 3; i <= value; i++)
        {
            int next = checked(previous + current);
            previous = current;
            current = next;
        }

        return current;
    }
}
