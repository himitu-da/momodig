using UnityEngine;

[CreateAssetMenu(fileName = "Dynamite", menuName = "MomoDig/Mining/Tools/Dynamite")]
public class Dynamite : MiningTool
{
    [Header("Throw Settings")]
    [SerializeField] private GameObject dynamitePrefab;
    [SerializeField] private float throwForce = 8f;
    
    [Header("Ballistic Settings")]
    [SerializeField] private float maxThrowDistance = 15f;        // 最大投射距離
    [SerializeField] private float gravity = 9.81f;              // 重力加速度

    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 10f;         // 爆発の力

    // Behaviour から参照できるように公開プロパティを用意
    public GameObject ProjectilePrefab => dynamitePrefab;
    public float ThrowForce => throwForce;
    public float MaxThrowDistance => maxThrowDistance;
    public float Gravity => gravity;
    public float ExplosionForce => explosionForce;
}
