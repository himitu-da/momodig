using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    public void OnClickToChangeScene(string sceneNameToChange)
    {
        if (GameSceneCoordinator.TrySwitchToScene(sceneNameToChange))
        {
            return;
        }

        SceneManager.LoadScene(sceneNameToChange);
    }
}
