using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    public void OnClickToChangeScene(string sceneNameToChange)
    {
        OnClickToChangeScene(sceneNameToChange, string.Empty);
    }

    public void OnClickToChangeScene(string sceneNameToChange, string entryPointId)
    {
        TryChangeScene(sceneNameToChange, entryPointId);
    }

    public bool TryChangeScene(string sceneNameToChange)
    {
        return TryChangeScene(sceneNameToChange, string.Empty);
    }

    public bool TryChangeScene(string sceneNameToChange, string entryPointId)
    {
        if (GameSceneCoordinator.TrySwitchToScene(sceneNameToChange, entryPointId))
        {
            return true;
        }

        if (GameSceneCoordinator.IsDefaultManagedContentSceneName(sceneNameToChange))
        {
            LogManagedSceneSwitchFailure(sceneNameToChange);
            return false;
        }

        SceneManager.LoadScene(sceneNameToChange);
        return true;
    }

    public void OnClickToChangeScene(string sceneNameToChange, string entryPointId, Vector3 destinationPlayerPosition)
    {
        TryChangeScene(sceneNameToChange, entryPointId, destinationPlayerPosition);
    }

    public bool TryChangeScene(string sceneNameToChange, string entryPointId, Vector3 destinationPlayerPosition)
    {
        if (GameSceneCoordinator.TrySwitchToScene(sceneNameToChange, entryPointId, destinationPlayerPosition))
        {
            return true;
        }

        if (GameSceneCoordinator.IsDefaultManagedContentSceneName(sceneNameToChange))
        {
            LogManagedSceneSwitchFailure(sceneNameToChange);
            return false;
        }

        SceneManager.LoadScene(sceneNameToChange);
        return true;
    }

    private static void LogManagedSceneSwitchFailure(string sceneNameToChange)
    {
        if (GameSceneCoordinator.Instance != null)
        {
            Debug.LogWarning($"ChangeScene: Managed content scene switch to '{sceneNameToChange}' was not started.");
            return;
        }

        Debug.LogError($"ChangeScene: Cannot load managed content scene '{sceneNameToChange}' without GameSceneCoordinator. Start from BaseScene.");
    }
}
