using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ManualSaveButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject statusBackground;
    [SerializeField] private TMP_Text statusText;
    [SerializeField, Min(0f)] private float savedVisibleSeconds = 3f;

    private bool isSaving;
    private Coroutine saveRoutine;
    private Coroutine hideStatusRoutine;

    private void OnEnable()
    {
        if (!ValidateReferences())
        {
            return;
        }

        button.onClick.RemoveListener(HandleSaveClicked);
        button.onClick.AddListener(HandleSaveClicked);
        HideStatus();
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleSaveClicked);
        }

        if (saveRoutine != null)
        {
            StopCoroutine(saveRoutine);
            saveRoutine = null;
        }

        if (hideStatusRoutine != null)
        {
            StopCoroutine(hideStatusRoutine);
            hideStatusRoutine = null;
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

        saveRoutine = StartCoroutine(SaveRoutine(persistenceManager));
    }

    private IEnumerator SaveRoutine(GameDataPersistenceManager persistenceManager)
    {
        isSaving = true;
        button.interactable = false;
        ShowStatus("Saving...");

        yield return null;

        bool saved = false;
        try
        {
            saved = persistenceManager.SaveToDisk();
            if (!saved)
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

        ShowStatus(saved ? "Saved!" : "Save Failed");
        RestartHideStatusRoutine();
        saveRoutine = null;
    }

    private bool ValidateReferences()
    {
        if (button == null)
        {
            Debug.LogError("ManualSaveButton: button is not configured.", this);
            return false;
        }

        if (statusBackground == null)
        {
            Debug.LogError("ManualSaveButton: statusBackground is not configured.", this);
            return false;
        }

        if (statusText == null)
        {
            Debug.LogError("ManualSaveButton: statusText is not configured.", this);
            return false;
        }

        return true;
    }

    private void ShowStatus(string message)
    {
        if (hideStatusRoutine != null)
        {
            StopCoroutine(hideStatusRoutine);
            hideStatusRoutine = null;
        }

        statusText.text = message;
        statusBackground.SetActive(true);
    }

    private void RestartHideStatusRoutine()
    {
        if (hideStatusRoutine != null)
        {
            StopCoroutine(hideStatusRoutine);
        }

        hideStatusRoutine = StartCoroutine(HideStatusAfterDelay());
    }

    private IEnumerator HideStatusAfterDelay()
    {
        yield return new WaitForSecondsRealtime(savedVisibleSeconds);
        HideStatus();
        hideStatusRoutine = null;
    }

    private void HideStatus()
    {
        if (statusBackground != null)
        {
            statusBackground.SetActive(false);
        }
    }
}
