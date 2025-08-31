using UnityEngine;
using System.Collections;

public class DroppedItem : MonoBehaviour
{
    private Rigidbody rb;
    private bool isSleeping = false;
    private float sleepCheckInterval = 1.0f; // 1秒ごとに静止状態をチェック
    private float sleepVelocityThreshold = 0.1f; // この速度以下で静止とみなす

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        // プールから再利用される際に初期化
        isSleeping = false;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        StartCoroutine(CheckForSleep());
    }

    private IEnumerator CheckForSleep()
    {
        while (!isSleeping)
        {
            yield return new WaitForSeconds(sleepCheckInterval);

            if (rb != null && rb.linearVelocity.magnitude < sleepVelocityThreshold)
            {
                rb.Sleep();
                isSleeping = true;
            }
        }
    }
}
