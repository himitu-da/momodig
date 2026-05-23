using UnityEngine;

public class TorchToolBehaviour : MiningToolBehaviour
{
    [SerializeField, Min(0f)] private float subUseDisplayDuration = 0.15f;

    private TorchPlacementManager torchPlacementManager;

    public override void SetTorchPlacementManager(TorchPlacementManager torchPlacementManager)
    {
        this.torchPlacementManager = torchPlacementManager;
    }

    public override void Use(Vector3 direction, PlayerController playerController)
    {
        if (!IsEquipped)
        {
            return;
        }

        if (torchPlacementManager == null)
        {
            Debug.LogError("TorchToolBehaviour: TorchPlacementManager is not configured.", this);
            return;
        }

        if (playerController == null)
        {
            Debug.LogError("TorchToolBehaviour: PlayerController is not configured.", this);
            return;
        }

        BeginUseDisplay();
        CancelInvoke(nameof(EndSubUseDisplay));
        Invoke(nameof(EndSubUseDisplay), subUseDisplayDuration);

        Vector3 targetPosition = playerController.GetMouseWorldPosition(10f);
        if (!torchPlacementManager.ToggleTorchAtWorldPosition(targetPosition))
        {
            Debug.LogWarning($"TorchToolBehaviour: failed to toggle torch at worldPosition={targetPosition}.", this);
        }
    }

    protected override void RenderForDirection(Vector3 direction)
    {
    }

    private void EndSubUseDisplay()
    {
        EndUseDisplay();
    }
}
