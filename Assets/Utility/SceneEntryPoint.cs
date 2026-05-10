using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneEntryPoint : MonoBehaviour
{
    [SerializeField] private string entryPointId = "Default";
    [SerializeField] private bool useWhenNoEntryPointId = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool resetRigidbodyVelocity = true;

    public string EntryPointId => entryPointId;
    public bool UseWhenNoEntryPointId => useWhenNoEntryPointId;

    public void PlacePlayer()
    {
        GameObject player = FindTaggedObjectInScene(gameObject.scene, playerTag);
        if (player == null)
        {
            Debug.LogWarning($"SceneEntryPoint: Player tagged '{playerTag}' was not found in scene '{gameObject.scene.name}'.");
            return;
        }

        player.transform.SetPositionAndRotation(transform.position, transform.rotation);

        if (!resetRigidbodyVelocity || !player.TryGetComponent(out Rigidbody playerRigidbody))
        {
            return;
        }

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
    }

    public static SceneEntryPoint FindInScene(Scene scene, string requestedEntryPointId)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        SceneEntryPoint firstEntryPoint = null;
        SceneEntryPoint defaultEntryPoint = null;
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            SceneEntryPoint[] entryPoints = rootObjects[i].GetComponentsInChildren<SceneEntryPoint>(true);
            for (int j = 0; j < entryPoints.Length; j++)
            {
                SceneEntryPoint entryPoint = entryPoints[j];
                if (entryPoint == null)
                {
                    continue;
                }

                if (firstEntryPoint == null)
                {
                    firstEntryPoint = entryPoint;
                }

                if (string.IsNullOrEmpty(requestedEntryPointId) && entryPoint.useWhenNoEntryPointId)
                {
                    defaultEntryPoint = entryPoint;
                }

                if (!string.IsNullOrEmpty(requestedEntryPointId) && entryPoint.entryPointId == requestedEntryPointId)
                {
                    return entryPoint;
                }
            }
        }

        return defaultEntryPoint != null ? defaultEntryPoint : firstEntryPoint;
    }

    private static GameObject FindTaggedObjectInScene(Scene scene, string tagName)
    {
        if (string.IsNullOrEmpty(tagName))
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform[] transforms = rootObjects[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j] != null && transforms[j].CompareTag(tagName))
                {
                    return transforms[j].gameObject;
                }
            }
        }

        return null;
    }
}
