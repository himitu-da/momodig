using UnityEngine;

[CreateAssetMenu(fileName = "FluidDefinition", menuName = "MomoDig/Fluid/Definition")]
public class FluidDefinition : ScriptableObject
{
    [Header("Physical Properties")]
    [Min(0.1f)]
    public float densityKgPerCubicMeter = 1000f;

    [Min(0.01f)]
    public float viscosity = 1f;

    [Min(0.1f)]
    public float downwardCellVolumesPerSecond = 8f;

    [Min(0.0f)]
    public float lateralCellVolumesPerSecond = 3f;

    [Min(0.0f)]
    public float velocityDamping = 6f;

    [Min(0.0f)]
    public float explosionImpulseMultiplier = 1f;

    [Header("Visuals")]
    public Color tint = new Color(0.22f, 0.45f, 1.0f, 0.58f);

    public float SpecificGravity => densityKgPerCubicMeter / 1000f;
}
