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

        SceneManager.LoadScene(sceneNameToChange);
    }

    public void OnClickToChangeScene(string sceneNameToChange, string entryPointId, Vector3 destinationPlayerPosition)
    {
        if (GameSceneCoordinator.TrySwitchToScene(sceneNameToChange, entryPointId, destinationPlayerPosition))
        {
            return;
        }

        SceneManager.LoadScene(sceneNameToChange);
    }
}
