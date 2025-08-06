using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeed = 100f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
