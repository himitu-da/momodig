using UnityEngine;

/// <summary>
/// デバッグ用にセーブとロードをトリガーするためのクラス
/// </summary>
public class SaveLoadTester : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.Log("S key pressed. Saving game...");
            SaveManager.Instance.SaveGame();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("L key pressed. Loading game...");
            SaveManager.Instance.LoadGame();
        }
    }
}
