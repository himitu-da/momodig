using UnityEngine;

public class FluidSource : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FluidManager fluidManager;
    [SerializeField] private FluidDefinition fluidDefinition;

    [Header("Emission")]
    [SerializeField] private bool emitContinuously = true;
    [SerializeField] private float litersPerSecond = 8f;
    [SerializeField] private float burstLiters = 16f;

    void Update()
    {
        if (!emitContinuously || fluidManager == null)
        {
            return;
        }

        fluidManager.AddFluidAtWorldPosition(transform.position, litersPerSecond * Time.deltaTime, fluidDefinition);
    }

    [ContextMenu("Inject Burst")]
    public void InjectBurst()
    {
        if (fluidManager == null)
        {
            return;
        }

        fluidManager.AddFluidAtWorldPosition(transform.position, burstLiters, fluidDefinition);
    }
}

