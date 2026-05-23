using UnityEngine;

[CreateAssetMenu(fileName = "MiningLightProfile", menuName = "Momodig/Mining Light Profile")]
public class MiningLightProfile : ScriptableObject
{
    [Header("Light Propagation")]
    [SerializeField, Range(0f, 1f)] private float brightness = 1f;
    [SerializeField, Min(0)] private int sourceRadiusCells = 1;
    [SerializeField, Range(0f, 1f)] private float airCellTransmission = 0.9f;
    [SerializeField, Range(0f, 1f)] private float solidCellTransmission = 0.8f;
    [SerializeField, Range(0.001f, 1f)] private float minBrightness = 0.05f;
    [SerializeField, Min(1)] private int maxPropagationCellsPerLightPerFrame = 432;
    [SerializeField, Min(1)] private int maxPropagationCellsPerRunPerFrame = 16;

    [Header("Gizmos")]
    [SerializeField] private Color sourceGizmoColor = new Color(1f, 1f, 1f, 0.8f);

    public float Brightness => brightness;
    public int SourceRadiusCells => sourceRadiusCells;
    public float AirCellTransmission => airCellTransmission;
    public float SolidCellTransmission => solidCellTransmission;
    public float MinBrightness => minBrightness;
    public int MaxPropagationCellsPerLightPerFrame => Mathf.Max(1, maxPropagationCellsPerLightPerFrame);
    public int MaxPropagationCellsPerRunPerFrame => Mathf.Max(1, maxPropagationCellsPerRunPerFrame);
    public Color SourceGizmoColor => sourceGizmoColor;

#if UNITY_EDITOR
    private void OnValidate()
    {
        brightness = Mathf.Clamp01(brightness);
        sourceRadiusCells = Mathf.Max(0, sourceRadiusCells);
        airCellTransmission = Mathf.Clamp01(airCellTransmission);
        solidCellTransmission = Mathf.Clamp01(solidCellTransmission);
        minBrightness = Mathf.Clamp(minBrightness, 0.001f, Mathf.Max(0.001f, brightness));
        maxPropagationCellsPerLightPerFrame = Mathf.Max(1, maxPropagationCellsPerLightPerFrame);
        maxPropagationCellsPerRunPerFrame = Mathf.Max(1, maxPropagationCellsPerRunPerFrame);
    }
#endif
}
