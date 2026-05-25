using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneCoordinator : MonoBehaviour
{
    public static GameSceneCoordinator Instance { get; private set; }
    public const string EditorDirectPlayContentSceneSessionKey = "Momodig.GameSceneCoordinator.DirectPlayContentScene";

    private static readonly string[] DefaultManagedContentSceneNames =
    {
        "OverWorldScene",
        "MiningScene"
    };

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

        SwitchToScene(GetStartupContentSceneName());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool CanSwitchToScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && managedContentSceneNames != null
            && managedContentSceneNames.Contains(sceneName);
    }

    public void SwitchToScene(string sceneName)
    {
        SwitchToScene(sceneName, string.Empty);
    }

    public void SwitchToScene(string sceneName, string entryPointId)
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

        transitionCoroutine = StartCoroutine(SwitchToSceneRoutine(sceneName, entryPointId));
    }

    public void SwitchToScene(string sceneName, string entryPointId, Vector3 destinationPlayerPosition)
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

        transitionCoroutine = StartCoroutine(SwitchToSceneRoutine(sceneName, entryPointId, true, destinationPlayerPosition));
    }

    public static bool TrySwitchToScene(string sceneName)
    {
        return TrySwitchToScene(sceneName, string.Empty);
    }

    public static bool TrySwitchToScene(string sceneName, string entryPointId)
    {
        if (Instance == null || !Instance.CanSwitchToScene(sceneName))
        {
            return false;
        }

        Instance.SwitchToScene(sceneName, entryPointId);
        return true;
    }

    public static bool TrySwitchToScene(string sceneName, string entryPointId, Vector3 destinationPlayerPosition)
    {
        if (Instance == null || !Instance.CanSwitchToScene(sceneName))
        {
            return false;
        }

        Instance.SwitchToScene(sceneName, entryPointId, destinationPlayerPosition);
        return true;
    }

    private IEnumerator SwitchToSceneRoutine(string targetSceneName, string entryPointId)
    {
        return SwitchToSceneRoutine(targetSceneName, entryPointId, false, Vector3.zero);
    }

    private IEnumerator SwitchToSceneRoutine(string targetSceneName, string entryPointId, bool hasDestinationPlayerPosition, Vector3 destinationPlayerPosition)
    {
        string previousContentSceneName = currentContentSceneName;
        if (string.IsNullOrEmpty(previousContentSceneName))
        {
            previousContentSceneName = FindLoadedManagedContentSceneName();
        }

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

        ApplyPlayerPlacement(targetScene, entryPointId, hasDestinationPlayerPosition, destinationPlayerPosition);
        NotifyAfterSceneLoad(targetScene, previousContentSceneName);

        if (!HasEnabledCamera(targetScene))
        {
            Debug.LogError($"GameSceneCoordinator: Scene '{targetSceneName}' has no enabled camera after scene load preparation.");
            transitionCoroutine = null;
            yield break;
        }

        currentContentSceneName = targetSceneName;

        List<Scene> scenesToUnload = GetLoadedManagedContentScenesExcept(targetSceneName);
        for (int i = 0; i < scenesToUnload.Count; i++)
        {
            NotifyBeforeSceneUnload(scenesToUnload[i], targetSceneName);
            DisableSceneRendering(scenesToUnload[i]);

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

    private void ApplyEntryPoint(Scene scene, string entryPointId)
    {
        SceneEntryPoint entryPoint = SceneEntryPoint.FindInScene(scene, entryPointId);
        if (entryPoint != null)
        {
            entryPoint.PlacePlayer();
        }
    }

    private void ApplyPlayerPlacement(Scene scene, string entryPointId, bool hasDestinationPlayerPosition, Vector3 destinationPlayerPosition)
    {
        if (!hasDestinationPlayerPosition)
        {
            ApplyEntryPoint(scene, entryPointId);
            return;
        }

        GameObject player = SceneEntryPoint.FindTaggedObjectInScene(scene, "Player");
        if (player == null)
        {
            Debug.LogWarning($"GameSceneCoordinator: Player tagged 'Player' was not found in scene '{scene.name}'.");
            return;
        }

        player.transform.position = destinationPlayerPosition;

        if (player.TryGetComponent(out Rigidbody playerRigidbody))
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void NotifyBeforeSceneUnload(Scene scene, string nextSceneName)
    {
        List<IGameSceneTransitionHandler> handlers = GetTransitionHandlers(scene);
        for (int i = 0; i < handlers.Count; i++)
        {
            try
            {
                handlers[i].OnBeforeContentSceneUnload(nextSceneName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private void NotifyAfterSceneLoad(Scene scene, string previousSceneName)
    {
        List<IGameSceneTransitionHandler> handlers = GetTransitionHandlers(scene);
        for (int i = 0; i < handlers.Count; i++)
        {
            try
            {
                handlers[i].OnAfterContentSceneLoad(previousSceneName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static void DisableSceneRendering(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Camera[] cameras = rootObjects[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cameras.Length; j++)
            {
                if (cameras[j] != null)
                {
                    cameras[j].enabled = false;
                }
            }

            AudioListener[] audioListeners = rootObjects[i].GetComponentsInChildren<AudioListener>(true);
            for (int j = 0; j < audioListeners.Length; j++)
            {
                if (audioListeners[j] != null)
                {
                    audioListeners[j].enabled = false;
                }
            }
        }
    }

    private static bool HasEnabledCamera(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Camera[] cameras = rootObjects[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cameras.Length; j++)
            {
                if (cameras[j] != null && cameras[j].enabled)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<IGameSceneTransitionHandler> GetTransitionHandlers(Scene scene)
    {
        List<IGameSceneTransitionHandler> handlers = new List<IGameSceneTransitionHandler>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return handlers;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            MonoBehaviour[] behaviours = rootObjects[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                if (behaviours[j] is IGameSceneTransitionHandler handler)
                {
                    handlers.Add(handler);
                }
            }
        }

        return handlers;
    }

    private string GetStartupContentSceneName()
    {
#if UNITY_EDITOR
        string editorDirectPlaySceneName = UnityEditor.SessionState.GetString(EditorDirectPlayContentSceneSessionKey, string.Empty);
        UnityEditor.SessionState.EraseString(EditorDirectPlayContentSceneSessionKey);
        if (!string.IsNullOrEmpty(editorDirectPlaySceneName))
        {
            if (CanSwitchToScene(editorDirectPlaySceneName))
            {
                return editorDirectPlaySceneName;
            }

            Debug.LogError($"GameSceneCoordinator: Editor direct play scene '{editorDirectPlaySceneName}' is not a managed content scene.", this);
        }
#endif

        return initialContentSceneName;
    }

    public static bool IsDefaultManagedContentSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        for (int i = 0; i < DefaultManagedContentSceneNames.Length; i++)
        {
            if (DefaultManagedContentSceneNames[i] == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}
