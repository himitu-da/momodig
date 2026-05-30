using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneCoordinator : MonoBehaviour
{
    private static readonly ProfilerMarker CapturePreviousFrameMarker =
        new ProfilerMarker("GameSceneCoordinator.CapturePreviousFrame");
    private static readonly ProfilerMarker LoadTargetSceneMarker =
        new ProfilerMarker("GameSceneCoordinator.LoadTargetScene");
    private static readonly ProfilerMarker PrepareTargetSceneMarker =
        new ProfilerMarker("GameSceneCoordinator.PrepareTargetScene");
    private static readonly ProfilerMarker StartPreviousSceneUnloadMarker =
        new ProfilerMarker("GameSceneCoordinator.StartPreviousSceneUnload");
    private static readonly ProfilerMarker RevealAndUnloadMarker =
        new ProfilerMarker("GameSceneCoordinator.RevealAndUnload");

    public static GameSceneCoordinator Instance { get; private set; }
    public const string EditorDirectPlayContentSceneSessionKey = "Momodig.GameSceneCoordinator.DirectPlayContentScene";

    private static readonly string[] DefaultManagedContentSceneNames =
    {
        "TitleScene",
        "OverWorldScene",
        "MiningScene"
    };

    [Header("Content Scenes")]
    [SerializeField] private string initialContentSceneName = "TitleScene";
    [SerializeField] private List<string> managedContentSceneNames = new List<string>
    {
        "TitleScene",
        "OverWorldScene",
        "MiningScene"
    };

    [Header("Startup")]
    [SerializeField] private bool loadInitialContentSceneOnStart = true;
    [SerializeField] private bool setContentSceneActive = true;

    [Header("Transition")]
    [SerializeField] private SceneDotTransitionOverlay transitionOverlay;

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

        if (transitionOverlay == null)
        {
            Debug.LogError("GameSceneCoordinator: transitionOverlay is not configured.", this);
            enabled = false;
        }
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

        bool shouldPlayTransitionOverlay = !string.IsNullOrEmpty(previousContentSceneName);
        if (shouldPlayTransitionOverlay)
        {
            if (transitionOverlay == null)
            {
                Debug.LogError("GameSceneCoordinator: Cannot switch scenes because transitionOverlay is not configured.", this);
                transitionCoroutine = null;
                yield break;
            }

            yield return RunMarkedCoroutine(transitionOverlay.CaptureCurrentFrame(), CapturePreviousFrameMarker);
            if (!transitionOverlay.HasCapturedFrame)
            {
                Debug.LogError("GameSceneCoordinator: Failed to capture the previous scene frame for transition.", this);
                transitionCoroutine = null;
                yield break;
            }
        }

        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            AsyncOperation loadOperation;
            using (LoadTargetSceneMarker.Auto())
            {
                loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
            }

            if (loadOperation == null)
            {
                Debug.LogError($"GameSceneCoordinator: Failed to load scene '{targetSceneName}'.");
                ClearTransitionOverlayIfNeeded(shouldPlayTransitionOverlay);
                transitionCoroutine = null;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                using (LoadTargetSceneMarker.Auto())
                {
                }

                yield return null;
            }

            targetScene = SceneManager.GetSceneByName(targetSceneName);
        }

        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError($"GameSceneCoordinator: Scene '{targetSceneName}' was not loaded.");
            ClearTransitionOverlayIfNeeded(shouldPlayTransitionOverlay);
            transitionCoroutine = null;
            yield break;
        }

        using (PrepareTargetSceneMarker.Auto())
        {
            if (setContentSceneActive)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            ApplyPlayerPlacement(targetScene, entryPointId, hasDestinationPlayerPosition, destinationPlayerPosition);
            NotifyAfterSceneLoad(targetScene, previousContentSceneName);
        }

        if (!HasEnabledCamera(targetScene))
        {
            Debug.LogError($"GameSceneCoordinator: Scene '{targetSceneName}' has no enabled camera after scene load preparation.");
            ClearTransitionOverlayIfNeeded(shouldPlayTransitionOverlay);
            transitionCoroutine = null;
            yield break;
        }

        currentContentSceneName = targetSceneName;

        List<Scene> scenesToUnload = GetLoadedManagedContentScenesExcept(targetSceneName);
        List<AsyncOperation> unloadOperations = StartPreviousSceneUnload(scenesToUnload, targetSceneName);
        yield return RevealAndUnloadRoutine(shouldPlayTransitionOverlay, unloadOperations);

        transitionCoroutine = null;
    }

    private List<AsyncOperation> StartPreviousSceneUnload(List<Scene> scenesToUnload, string targetSceneName)
    {
        List<AsyncOperation> unloadOperations = new List<AsyncOperation>();
        using (StartPreviousSceneUnloadMarker.Auto())
        {
            for (int i = 0; i < scenesToUnload.Count; i++)
            {
                NotifyBeforeSceneUnload(scenesToUnload[i], targetSceneName);
                DisableSceneRendering(scenesToUnload[i]);

                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scenesToUnload[i]);
                if (unloadOperation != null)
                {
                    unloadOperations.Add(unloadOperation);
                }
            }
        }

        return unloadOperations;
    }

    private IEnumerator RevealAndUnloadRoutine(bool shouldPlayTransitionOverlay, List<AsyncOperation> unloadOperations)
    {
        IEnumerator revealRoutine = shouldPlayTransitionOverlay ? transitionOverlay.PlayReveal() : null;
        bool revealDone = !shouldPlayTransitionOverlay;

        try
        {
            while (true)
            {
                bool unloadDone;
                using (RevealAndUnloadMarker.Auto())
                {
                    if (!revealDone)
                    {
                        revealDone = !revealRoutine.MoveNext();
                    }

                    unloadDone = AreAsyncOperationsDone(unloadOperations);
                }

                if (revealDone && unloadDone)
                {
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            if (revealRoutine is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static bool AreAsyncOperationsDone(List<AsyncOperation> operations)
    {
        if (operations == null)
        {
            return true;
        }

        for (int i = 0; i < operations.Count; i++)
        {
            if (operations[i] != null && !operations[i].isDone)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerator RunMarkedCoroutine(IEnumerator routine, ProfilerMarker marker)
    {
        try
        {
            while (true)
            {
                bool hasNext;
                object current = null;
                using (marker.Auto())
                {
                    hasNext = routine.MoveNext();
                    if (hasNext)
                    {
                        current = routine.Current;
                    }
                }

                if (!hasNext)
                {
                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            if (routine is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private void ClearTransitionOverlayIfNeeded(bool shouldClear)
    {
        if (shouldClear && transitionOverlay != null)
        {
            transitionOverlay.ClearOverlay();
        }
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
