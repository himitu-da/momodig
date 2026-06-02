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
    private bool missingFluidManagerLogged;

    void Awake()
    {
        ValidateFluidManager();
    }

    void Update()
    {
        if (!emitContinuously)
        {
            return;
        }

        if (!ValidateFluidManager())
        {
            return;
        }

        fluidManager.AddFluidAtWorldPosition(transform.position, litersPerSecond * Time.deltaTime, fluidDefinition);
    }

    [ContextMenu("Inject Burst")]
    public void InjectBurst()
    {
        if (!ValidateFluidManager())
        {
            return;
        }

        fluidManager.AddFluidAtWorldPosition(transform.position, burstLiters, fluidDefinition);
    }

    private bool ValidateFluidManager()
    {
        if (fluidManager != null)
        {
            missingFluidManagerLogged = false;
            return true;
        }

        if (!missingFluidManagerLogged)
        {
            missingFluidManagerLogged = true;
            Debug.LogError("FluidSource: FluidManager is not assigned.", this);
        }

        return false;
    }
}

