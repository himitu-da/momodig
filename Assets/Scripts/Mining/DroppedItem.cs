using UnityEngine;
using System.Collections.Generic;

public class DroppedItem : MonoBehaviour
{
    public Rigidbody rb { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        // プールから再利用される際に初期化
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
