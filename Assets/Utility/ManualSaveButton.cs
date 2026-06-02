using UnityEngine;
using UnityEngine.UI;

public class ManualSaveButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private bool isSaving;

    private void OnEnable()
    {
        if (!ValidateReferences())
        {
            return;
        }

        button.onClick.RemoveListener(HandleSaveClicked);
        button.onClick.AddListener(HandleSaveClicked);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleSaveClicked);
        }

        isSaving = false;
    }

    private void HandleSaveClicked()
    {
        if (isSaving)
        {
            return;
        }

        if (!ValidateReferences())
        {
            return;
        }

        GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager == null)
        {
            Debug.LogError("ManualSaveButton: GameDataPersistenceManager is not initialized.", this);
            return;
        }

        isSaving = true;
        button.interactable = false;
        try
        {
            if (!persistenceManager.SaveToDisk())
            {
                Debug.LogError("ManualSaveButton: Save failed.", this);
            }
        }
        finally
        {
            isSaving = false;
            if (button != null)
            {
                button.interactable = true;
            }
        }
    }

    private bool ValidateReferences()
    {
        if (button == null)
        {
            Debug.LogError("ManualSaveButton: button is not configured.", this);
            return false;
        }

        return true;
    }
}
