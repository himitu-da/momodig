using UnityEngine;

[CreateAssetMenu(fileName = "DynamiteMiningModule", menuName = "MomoDig/Mining/Modules/Dynamite")]
public class DynamiteMiningModule : MiningModule
{
    [Header("Throw Settings")]
    [SerializeField] private GameObject dynamitePrefab;
    [SerializeField] private Stat throwForce = new Stat { BaseValue = 8f };
    
    [Header("Ballistic Settings")]
    [SerializeField] private Stat maxThrowDistance = new Stat { BaseValue = 15f };
    [SerializeField] private float gravity = 9.81f;
    
    [Header("Explosion Settings")]
    [SerializeField] private Stat explosionForce = new Stat { BaseValue = 10f };

    [Header("Light")]
    [SerializeField] private MiningLightProfile projectileLightProfile;
    [SerializeField] private bool enableExplosionAfterglowLight = true;
    [SerializeField] private MiningLightProfile explosionAfterglowLightProfile;
    [SerializeField, Min(0.01f)] private float explosionAfterglowLifetimeSeconds = 0.45f;
    [SerializeField] private AnimationCurve explosionAfterglowBrightnessCurve =
        AnimationCurve.EaseInOut(0f, 0.75f, 1f, 0f);
    [SerializeField, Min(0.01f)] private float explosionAfterglowUpdateIntervalSeconds = 0.03f;

    // Digging settings (not used directly by Dynamite throw, but for explosion)
    [Header("Box Dig Settings")]
    [SerializeField] private Vector3 diggingCenter = Vector3.zero;
    [SerializeField] private StatVector3 diggingSize = new StatVector3();

    // Public properties
    public GameObject ProjectilePrefab => dynamitePrefab;
    public Stat ThrowForce => throwForce;
    public Stat MaxThrowDistance => maxThrowDistance;
    public float Gravity => gravity;
    public Stat ExplosionForce => explosionForce;
    public MiningLightProfile ProjectileLightProfile => projectileLightProfile;
    public bool EnableExplosionAfterglowLight => enableExplosionAfterglowLight;
    public MiningLightProfile ExplosionAfterglowLightProfile => explosionAfterglowLightProfile;
    public float ExplosionAfterglowLifetimeSeconds => explosionAfterglowLifetimeSeconds;
    public AnimationCurve ExplosionAfterglowBrightnessCurve => explosionAfterglowBrightnessCurve;
    public float ExplosionAfterglowUpdateIntervalSeconds => explosionAfterglowUpdateIntervalSeconds;

    public override Vector3 DiggingCenter => diggingCenter;
    public override StatVector3 DiggingSize => diggingSize;

    public override void Execute(GameObject user)
    {
        // Dynamite's execution is handled by DynamiteToolBehaviour instantiating a projectile.
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        explosionAfterglowLifetimeSeconds = Mathf.Max(0.01f, explosionAfterglowLifetimeSeconds);
        explosionAfterglowUpdateIntervalSeconds = Mathf.Max(0.01f, explosionAfterglowUpdateIntervalSeconds);
    }
#endif
}
