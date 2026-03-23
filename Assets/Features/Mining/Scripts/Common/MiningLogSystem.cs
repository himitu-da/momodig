using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Log notification system for the MiningScene.
/// Creates log item prefabs under the assigned LogField and keeps the newest logs at the top.
/// </summary>
public class MiningLogSystem : MonoBehaviour
{
    private const int MaxLogCount = 3;
    private const string DefaultOutroTriggerName = "SlideOut";

    [Header("Log UI References")]
    [SerializeField] private Transform logField;
    [SerializeField] private GameObject logItemPrefab;
    [SerializeField] private float defaultLogLifetime = 10f;
    [SerializeField] private string outroTriggerName = DefaultOutroTriggerName;

    [Header("Log Item Child Names")]
    [SerializeField] private string logTextObjectName = "LogText";
    [SerializeField] private string logIconObjectName = "LogIcon";

    /// <summary>
    /// Adds a text-only log item.
    /// </summary>
    /// <param name="message">Log message to display.</param>
    public void ShowLog(string message)
    {
        ShowLog(message, null);
    }

    /// <summary>
    /// Adds a log item with an optional icon.
    /// Kept for future expansion even if the initial use is text-only.
    /// </summary>
    /// <param name="message">Log message to display.</param>
    /// <param name="iconSprite">Optional icon sprite.</param>
    public void ShowLog(string message, Sprite iconSprite)
    {
        if (!ValidateReferences())
        {
            return;
        }

        GameObject logItemInstance = Instantiate(logItemPrefab, logField);
        logItemInstance.transform.SetAsFirstSibling();

        ApplyText(logItemInstance.transform, message);
        ApplyIcon(logItemInstance.transform, iconSprite);
        ConfigureLogLifecycle(logItemInstance);
        TrimOverflowLogs();
    }

    /// <summary>
    /// Removes all currently displayed log items.
    /// </summary>
    public void ClearLogs()
    {
        if (logField == null)
        {
            return;
        }

        for (int i = logField.childCount - 1; i >= 0; i--)
        {
            Destroy(logField.GetChild(i).gameObject);
        }
    }

    private bool ValidateReferences()
    {
        if (logField == null)
        {
            Debug.LogWarning("MiningLogSystem: LogField is not assigned.");
            return false;
        }

        if (logItemPrefab == null)
        {
            Debug.LogWarning("MiningLogSystem: LogItem Prefab is not assigned.");
            return false;
        }

        return true;
    }

    private void ApplyText(Transform logItemTransform, string message)
    {
        Transform logTextTransform = FindChildRecursive(logItemTransform, logTextObjectName);
        if (logTextTransform == null)
        {
            Debug.LogWarning($"MiningLogSystem: Child object '{logTextObjectName}' was not found on the log item prefab.");
            return;
        }

        TMP_Text logText = logTextTransform.GetComponent<TMP_Text>();
        if (logText == null)
        {
            Debug.LogWarning($"MiningLogSystem: TMP_Text was not found on '{logTextObjectName}'.");
            return;
        }

        logText.SetText(message);
    }

    private void ApplyIcon(Transform logItemTransform, Sprite iconSprite)
    {
        Transform logIconTransform = FindChildRecursive(logItemTransform, logIconObjectName);
        if (logIconTransform == null)
        {
            return;
        }

        Image logIcon = logIconTransform.GetComponent<Image>();
        if (logIcon == null)
        {
            Debug.LogWarning($"MiningLogSystem: Image was not found on '{logIconObjectName}'.");
            return;
        }

        bool hasIcon = iconSprite != null;
        logIcon.sprite = iconSprite;
        logIcon.enabled = hasIcon;
    }

    private void TrimOverflowLogs()
    {
        while (logField.childCount > MaxLogCount)
        {
            Transform oldestLog = logField.GetChild(logField.childCount - 1);
            Destroy(oldestLog.gameObject);
        }
    }

    private void ConfigureLogLifecycle(GameObject logItemInstance)
    {
        MiningLogItemAnimationEvents animationEvents = FindAnimationEvents(logItemInstance);
        if (animationEvents == null)
        {
            Debug.LogWarning("MiningLogSystem: MiningLogItemAnimationEvents was not found on the log item prefab. Falling back to direct lifetime destroy.");
            if (defaultLogLifetime > 0f)
            {
                Destroy(logItemInstance, defaultLogLifetime);
            }
            return;
        }

        animationEvents.Initialize(logItemInstance, defaultLogLifetime, outroTriggerName);
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private MiningLogItemAnimationEvents FindAnimationEvents(GameObject logItemInstance)
    {
        return logItemInstance.GetComponentInChildren<MiningLogItemAnimationEvents>(true);
    }
}
