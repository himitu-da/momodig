public interface IGameSceneTransitionHandler
{
    void OnBeforeContentSceneUnload(string nextSceneName);
    void OnAfterContentSceneLoad(string previousSceneName);
}
