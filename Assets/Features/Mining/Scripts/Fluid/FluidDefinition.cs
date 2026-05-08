using UnityEngine;

[CreateAssetMenu(fileName = "FluidDefinition", menuName = "MomoDig/Fluid/Definition")]
public class FluidDefinition : ScriptableObject
{
    [Header("物性設定")]
    [Min(0.1f), InspectorName("密度 (kg/m^3)"), Tooltip("1立方メートルあたりの質量です。将来の比重や浮力計算の基準になります。")]
    public float densityKgPerCubicMeter = 1000f;

    [Min(0.01f), InspectorName("粘性"), Tooltip("大きいほど流れにくく、爆発で付いた速度も残りにくくなります。")]
    public float viscosity = 1f;

    [Min(0.1f), InspectorName("下方向の流れやすさ"), Tooltip("下方向へ落ちる基本速度です。大きいほど速く落ちます。")]
    public float downwardCellVolumesPerSecond = 4f;

    [Min(0.0f), InspectorName("横方向の広がりやすさ"), Tooltip("横に広がる基本速度です。大きいほど平らに広がります。")]
    public float lateralCellVolumesPerSecond = 3f;

    [Min(0.0f), InspectorName("速度減衰"), Tooltip("爆発などで付いた速度がどれだけ早く消えるかです。大きいほど早く止まります。")]
    public float velocityDamping = 3f;

    [Min(0.0f), InspectorName("爆発の受けやすさ"), Tooltip("大きいほど爆風で動きやすくなります。")]
    public float explosionImpulseMultiplier = 5f;

    [Header("見た目設定")]
    [InspectorName("色"), Tooltip("この流体の描画色です。アルファ値で透明度も調整できます。")]
    public Color tint = new Color(0.22f, 0.45f, 1.0f, 0.58f);

    public float SpecificGravity => densityKgPerCubicMeter / 1000f;
}
