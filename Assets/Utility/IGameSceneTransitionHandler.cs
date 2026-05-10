using UnityEngine;

public interface IGameSceneTransitionHandler
{
    void OnBeforeContentSceneUnload(string nextSceneName);
    void OnAfterContentSceneLoad(string previousSceneName);
}

public enum PassageAreaKind
{
    On,
    Off
}

public interface IPassageAreaTriggerReceiver
{
    void OnPassageAreaTrigger(PassageAreaKind areaKind, Collider other, bool entered);
}

[DisallowMultipleComponent]
public class PassageAreaTrigger : MonoBehaviour
{
    private PassageAreaKind areaKind;
    private IPassageAreaTriggerReceiver receiver;

    public static void Attach(Collider areaCollider, IPassageAreaTriggerReceiver receiver, PassageAreaKind areaKind)
    {
        if (areaCollider == null)
        {
            return;
        }

        areaCollider.isTrigger = true;

        PassageAreaTrigger trigger = areaCollider.GetComponent<PassageAreaTrigger>();
        if (trigger == null)
        {
            trigger = areaCollider.gameObject.AddComponent<PassageAreaTrigger>();
        }

        trigger.Initialize(receiver, areaKind);
    }

    private void Initialize(IPassageAreaTriggerReceiver receiver, PassageAreaKind areaKind)
    {
        this.receiver = receiver;
        this.areaKind = areaKind;
    }

    private void OnTriggerEnter(Collider other)
    {
        receiver?.OnPassageAreaTrigger(areaKind, other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        receiver?.OnPassageAreaTrigger(areaKind, other, false);
    }
}
