using UnityEngine;

[CreateAssetMenu(fileName = "Dynamite", menuName = "MomoDig/Mining/Tools/Dynamite")]
public class Dynamite : MiningTool
{
    [Header("Throw Settings")]
    [SerializeField] private GameObject dynamitePrefab;
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float upwardForce = 2f; // 8方向化により基本未使用（必要なら水平時の微調整に）
    // Behaviour から参照できるように公開プロパティを用意
    public GameObject ProjectilePrefab => dynamitePrefab;
    public float ThrowForce => throwForce;
    public float UpwardForce => upwardForce;
}
