using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneCoordinator : MonoBehaviour
{
    public static GameSceneCoordinator Instance { get; private set; }

    [Header("Content Scenes")]
    [SerializeField] private string initialContentSceneName = "OverWorldScene";
    [SerializeField] private List<string> managedContentSceneNames = new List<string>
    {
        "OverWorldScene",
        "MiningScene"
    };

    [Header("Startup")]
    [SerializeField] private bool loadInitialContentSceneOnStart = true;
    [SerializeField] private bool setContentSceneActive = true;

    private Coroutine transitionCoroutine;
    private string currentContentSceneName;

    public string CurrentContentSceneName => currentContentSceneName;
    public bool IsTransitioning => transitionCoroutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!loadInitialContentSceneOnStart)
        {
            currentContentSceneName = FindLoadedManagedContentSceneName();
            return;
        }

        string loadedContentSceneName = FindLoadedManagedContentSceneName();
        if (!string.IsNullOrEmpty(loadedContentSceneName))
        {
            currentContentSceneName = loadedContentSceneName;
            ActivateSceneIfLoaded(loadedContentSceneName);
            return;
        }

        SwitchToScene(initialContentSceneName);
    }

    public bool CanSwitchToScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && managedContentSceneNames != null
            && managedContentSceneNames.Contains(sceneName);
    }

    public void SwitchToScene(string sceneName)
    {
        if (!CanSwitchToScene(sceneName))
        {
            Debug.LogWarning($"GameSceneCoordinator: Scene '{sceneName}' is not a managed content scene.");
            return;
        }

        if (transitionCoroutine != null)
        {
            Debug.LogWarning($"GameSceneCoordinator: Ignored scene switch to '{sceneName}' because another transition is running.");
            return;
        }

        transitionCoroutine = StartCoroutine(SwitchToSceneRoutine(sceneName));
    }

    public static bool TrySwitchToScene(string sceneName)
    {
        if (Instance == null || !Instance.CanSwitchToScene(sceneName))
        {
            return false;
        }

        Instance.SwitchToScene(sceneName);
        return true;
    }

    private IEnumerator SwitchToSceneRoutine(string targetSceneName)
    {
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogError($"GameSceneCoordinator: Failed to load scene '{targetSceneName}'.");
                transitionCoroutine = null;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            targetScene = SceneManager.GetSceneByName(targetSceneName);
        }

        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError($"GameSceneCoordinator: Scene '{targetSceneName}' was not loaded.");
            transitionCoroutine = null;
            yield break;
        }

        if (setContentSceneActive)
        {
            SceneManager.SetActiveScene(targetScene);
        }

        List<Scene> scenesToUnload = GetLoadedManagedContentScenesExcept(targetSceneName);
        for (int i = 0; i < scenesToUnload.Count; i++)
        {
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scenesToUnload[i]);
            if (unloadOperation == null)
            {
                continue;
            }

            while (!unloadOperation.isDone)
            {
                yield return null;
            }
        }

        currentContentSceneName = targetSceneName;
        transitionCoroutine = null;
    }

    private string FindLoadedManagedContentSceneName()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (IsManagedContentScene(activeScene))
        {
            return activeScene.name;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (IsManagedContentScene(scene))
            {
                return scene.name;
            }
        }

        return string.Empty;
    }

    private bool IsManagedContentScene(Scene scene)
    {
        return scene.IsValid()
            && scene.isLoaded
            && managedContentSceneNames != null
            && managedContentSceneNames.Contains(scene.name);
    }

    private void ActivateSceneIfLoaded(string sceneName)
    {
        if (!setContentSceneActive)
        {
            return;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(scene);
        }
    }

    private List<Scene> GetLoadedManagedContentScenesExcept(string sceneName)
    {
        List<Scene> scenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (IsManagedContentScene(loadedScene) && loadedScene.name != sceneName)
            {
                scenes.Add(loadedScene);
            }
        }

        return scenes;
    }
}
