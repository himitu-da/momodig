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
        if (GameSceneCoordinator.TrySwitchToScene(sceneNameToChange, entryPointId))
        {
            return;
        }

        if (GameSceneCoordinator.IsDefaultManagedContentSceneName(sceneNameToChange))
        {
            Debug.LogError($"ChangeScene: Cannot load managed content scene '{sceneNameToChange}' without GameSceneCoordinator. Start from BaseScene.");
            return;
        }

        SceneManager.LoadScene(sceneNameToChange);
    }

    public void OnClickToChangeScene(string sceneNameToChange, string entryPointId, Vector3 destinationPlayerPosition)
    {
        if (GameSceneCoordinator.TrySwitchToScene(sceneNameToChange, entryPointId, destinationPlayerPosition))
        {
            return;
        }

        if (GameSceneCoordinator.IsDefaultManagedContentSceneName(sceneNameToChange))
        {
            Debug.LogError($"ChangeScene: Cannot load managed content scene '{sceneNameToChange}' without GameSceneCoordinator. Start from BaseScene.");
            return;
        }

        SceneManager.LoadScene(sceneNameToChange);
    }
}
