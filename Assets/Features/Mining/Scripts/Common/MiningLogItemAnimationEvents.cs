using System.Collections;
using UnityEngine;

/// <summary>
/// Handles animation-driven lifetime flow for a single log item.
/// Attach this to the same GameObject as the Animator that receives Animation Events.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MiningLogItemAnimationEvents : MonoBehaviour
{
    private GameObject rootLogItem;
    private Animator cachedAnimator;
    private Coroutine lifetimeCoroutine;
    private float logLifetime;
    private string outroTriggerName;
    private bool isInitialized;
    private bool isOutroTriggered;
    private bool hasIntroCompleted;

    public void Initialize(GameObject rootObject, float defaultLifetime, string slideOutTriggerName)
    {
        rootLogItem = rootObject;
        logLifetime = defaultLifetime;
        outroTriggerName = slideOutTriggerName;
        cachedAnimator = GetComponent<Animator>();
        isInitialized = true;
        isOutroTriggered = false;
        hasIntroCompleted = false;

        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
    }

    public void introAnimationStarted()
    {
        if (!isInitialized)
        {
            return;
        }

        hasIntroCompleted = false;
    }

    public void introAnimationCompleted()
    {
        if (!isInitialized || isOutroTriggered || hasIntroCompleted)
        {
            return;
        }

        hasIntroCompleted = true;

        if (logLifetime <= 0f)
        {
            TriggerOutro();
            return;
        }

        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
        }

        lifetimeCoroutine = StartCoroutine(BeginOutroAfterDelay());
    }

    public void outroAnimationStarted()
    {
        if (!isInitialized)
        {
            return;
        }

        isOutroTriggered = true;

        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
    }

    public void outroAnimationCompleted()
    {
        if (rootLogItem == null)
        {
            return;
        }

        Destroy(rootLogItem);
    }

    private IEnumerator BeginOutroAfterDelay()
    {
        yield return new WaitForSeconds(logLifetime);
        TriggerOutro();
    }

    private void TriggerOutro()
    {
        if (isOutroTriggered)
        {
            return;
        }

        if (cachedAnimator != null && !string.IsNullOrEmpty(outroTriggerName))
        {
            cachedAnimator.SetTrigger(outroTriggerName);
            return;
        }

        outroAnimationStarted();
        outroAnimationCompleted();
    }
}
