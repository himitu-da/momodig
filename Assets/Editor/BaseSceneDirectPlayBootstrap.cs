using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BaseSceneDirectPlayBootstrap
{
    private const string BaseScenePath = "Assets/Scenes/BaseScene.unity";
    private const string ControlledPlayStartSceneSessionKey = "Momodig.BaseSceneDirectPlayBootstrap.ControlledPlayStartScene";

    static BaseSceneDirectPlayBootstrap()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange stateChange)
    {
        if (stateChange == PlayModeStateChange.ExitingEditMode)
        {
            ConfigurePlayStartScene();
            return;
        }

        if (stateChange == PlayModeStateChange.EnteredEditMode)
        {
            ClearPlayStartScene();
        }
    }

    private static void ConfigurePlayStartScene()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!GameSceneCoordinator.IsDefaultManagedContentSceneName(activeScene.name))
        {
            ClearDirectPlayRequest();
            return;
        }

        SceneAsset baseScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BaseScenePath);
        if (baseScene == null)
        {
            Debug.LogError($"{nameof(BaseSceneDirectPlayBootstrap)}: BaseScene was not found at '{BaseScenePath}'.");
            ClearPlayStartScene();
            return;
        }

        SessionState.SetString(GameSceneCoordinator.EditorDirectPlayContentSceneSessionKey, activeScene.name);
        SessionState.SetString(ControlledPlayStartSceneSessionKey, "1");
        EditorSceneManager.playModeStartScene = baseScene;
    }

    private static void ClearPlayStartScene()
    {
        ClearDirectPlayRequest();
        if (SessionState.GetString(ControlledPlayStartSceneSessionKey, string.Empty) != "1")
        {
            return;
        }

        SessionState.EraseString(ControlledPlayStartSceneSessionKey);
        EditorSceneManager.playModeStartScene = null;
    }

    private static void ClearDirectPlayRequest()
    {
        SessionState.EraseString(GameSceneCoordinator.EditorDirectPlayContentSceneSessionKey);
    }
}
