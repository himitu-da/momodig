using UnityEngine;
using UnityEngine.UI;

public class PlayerRespawnButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private PlayerRespawnController respawnController;

    private bool isBound;

    private void OnEnable()
    {
        if (!ValidateReferences())
        {
            return;
        }

        button.onClick.RemoveListener(HandleClicked);
        button.onClick.AddListener(HandleClicked);
        respawnController.RespawnStateChanged -= HandleRespawnStateChanged;
        respawnController.RespawnStateChanged += HandleRespawnStateChanged;
        SyncButtonState();
        isBound = true;
    }

    private void OnDisable()
    {
        if (isBound && button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            isBound = false;
        }

        if (respawnController != null)
        {
            respawnController.RespawnStateChanged -= HandleRespawnStateChanged;
        }
    }

    private void HandleClicked()
    {
        if (respawnController == null)
        {
            Debug.LogError("PlayerRespawnButton: respawnController is not configured.", this);
            return;
        }

        if (respawnController.RequestRespawn())
        {
            SyncButtonState();
        }
    }

    private void HandleRespawnStateChanged(bool isRespawning)
    {
        SyncButtonState();
    }

    private void SyncButtonState()
    {
        if (button == null || respawnController == null)
        {
            return;
        }

        button.interactable = !respawnController.IsRespawning;
    }

    private bool ValidateReferences()
    {
        bool isValid = true;
        isValid &= ValidateReference(button, nameof(button));
        isValid &= ValidateReference(respawnController, nameof(respawnController));
        return isValid;
    }

    private bool ValidateReference(Object target, string fieldName)
    {
        if (target != null)
        {
            return true;
        }

        Debug.LogError($"PlayerRespawnButton: {fieldName} is not configured.", this);
        return false;
    }
}
