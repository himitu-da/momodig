using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    public void OnClickToChangeScene(string sceneNameToChange)
    {
        SceneManager.LoadScene(sceneNameToChange);
    }
}
