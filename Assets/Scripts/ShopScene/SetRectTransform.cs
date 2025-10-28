using UnityEngine;

public class SetRectTransformRatio : MonoBehaviour
{
    
    [SerializeField] RectTransform canvas;
    [SerializeField] RectTransform adjustedobject;
    [SerializeField] float RightRatio;
    [SerializeField] float LeftRatio;
    [SerializeField] float TopRatio;
    [SerializeField] float BottomRatio;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake() {
        if(adjustedobject == null)
        {
            adjustedobject = GetComponent<RectTransform>();
        }
    }
    void Start()
    {
        adjustedobject.anchorMin = new Vector2(0,0);
        adjustedobject.anchorMax = new Vector2(1, 1);

        adjustedobject.offsetMin = new Vector2(canvas.rect.width * LeftRatio, canvas.rect.height * BottomRatio);
        adjustedobject.offsetMax = new Vector2(-canvas.rect.width * RightRatio, -canvas.rect.height * TopRatio);
        Debug.Log($"{canvas}: {canvas.rect.width} {canvas.rect.height}");
        Debug.Log($"{adjustedobject}: {canvas.rect.width * LeftRatio} {canvas.rect.height * BottomRatio} {-canvas.rect.width * RightRatio} {-canvas.rect.height * TopRatio}");
    }
}
