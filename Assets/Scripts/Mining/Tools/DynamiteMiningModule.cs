using UnityEngine;

[CreateAssetMenu(fileName = "DynamiteMiningModule", menuName = "MomoDig/Mining/Modules/Dynamite")]
public class DynamiteMiningModule : MiningModule
{
    [Header("Throw Settings")]
    [SerializeField] private GameObject dynamitePrefab;
    [SerializeField] private float throwForce = 8f;
    
    [Header("Ballistic Settings")]
    [SerializeField] private float maxThrowDistance = 15f;
    [SerializeField] private float gravity = 9.81f;
    
    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 10f;

    // Digging settings (not used directly by Dynamite throw, but for explosion)
    [Header("Box Dig Settings")]
    [SerializeField] private Vector3 diggingCenter = Vector3.zero;
    [SerializeField] private Vector3 diggingSize = new Vector3(5, 5, 5);

    // Public properties
    public GameObject ProjectilePrefab => dynamitePrefab;
    public float ThrowForce => throwForce;
    public float MaxThrowDistance => maxThrowDistance;
    public float Gravity => gravity;
    public float ExplosionForce => explosionForce;

    public override Vector3 DiggingCenter => diggingCenter;
    public override Vector3 DiggingSize => diggingSize;

    public override void Execute(GameObject user)
    {
        // Dynamite's execution is handled by DynamiteToolBehaviour instantiating a projectile.
    }
}
