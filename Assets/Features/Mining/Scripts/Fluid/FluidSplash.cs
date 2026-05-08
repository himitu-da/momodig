using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FluidSplash : MonoBehaviour
{
    private FluidManager fluidManager;
    private FluidDefinition fluidDefinition;
    private float volumeLiters;
    private Rigidbody rb;

    [SerializeField] private float minimumLifetime = 0.5f;
    [SerializeField] private float settlingVelocityThreshold = 0.5f;
    [SerializeField] private float maxLifetime = 5.0f;

    private float age = 0f;
    private Mesh clonedMesh;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(FluidManager manager, FluidDefinition definition, float liters, Vector3 initialVelocity)
    {
        fluidManager = manager;
        fluidDefinition = definition;
        volumeLiters = Mathf.Max(0.01f, liters);

        // Set physical properties
        float density = definition != null ? definition.SpecificGravity : 1.0f;
        float massKg = volumeLiters * density;
        rb.mass = Mathf.Max(0.1f, massKg); // 水滴が軽すぎて物理演算がバグらないよう最低0.1kg保証
        rb.linearVelocity = initialVelocity;

        // Freeze Z axis and rotations to keep it strictly 2.5D like voxels
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

        // Cube(立方体)としての物理的なサイズを計算
        // 体積(m^3) = 質量(kg) / (密度(kg/L) * 1000) 
        float volumeM3 = massKg / (density * 1000f);

        // 立方体の体積 V = s^3 なので、一辺の長さ(scale)は体積の3乗根
        float sideLength = Mathf.Pow(volumeM3, 1f / 3f);
        
        // 周囲の地形ブロック(0.33m角)に違和感なく混ざるよう、
        // CubeスプラッシュのScale下限〜上限をボクセル基準(約0.33)に制限
        float scale = Mathf.Clamp(sideLength, 0.05f, 0.33f);
        transform.localScale = new Vector3(scale, scale, scale);

        // Apply tint from FluidDefinition to vertex colors (for Custom/FluidUnlit)
        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter != null)
        {
            clonedMesh = meshFilter.mesh;
            if (clonedMesh != null)
            {
                if (definition == null)
                {
                    Debug.LogWarning("FluidSplash: FluidDefinition is NULL! Using Magenta as fallback so it's obvious.");
                }
                
                // Use bright magenta as a fallback to make it 100% obvious if the definition is missing
                Color tintColor = definition != null ? definition.tint : Color.magenta;
                Color[] colors = new Color[clonedMesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = tintColor;
                }
                clonedMesh.colors = colors;
                // Force update mesh
                meshFilter.mesh = clonedMesh;
            }
        }
    }

    private void OnDestroy()
    {
        if (clonedMesh != null)
        {
            Destroy(clonedMesh);
        }
    }

    private void FixedUpdate()
    {
        if (fluidManager == null) return;

        age += Time.fixedDeltaTime;

        // Check if it should settle
        bool shouldSettle = age > maxLifetime;

        if (age > minimumLifetime && rb.linearVelocity.sqrMagnitude < (settlingVelocityThreshold * settlingVelocityThreshold))
        {
            shouldSettle = true;
        }

        if (shouldSettle)
        {
            SettleFluid();
        }
    }

    private void SettleFluid()
    {
        if (fluidManager != null)
        {
            // Returns fluid back into the grid
            fluidManager.AddFluidAtWorldPosition(transform.position, volumeLiters, fluidDefinition);
        }
        
        Destroy(gameObject);
    }
}

