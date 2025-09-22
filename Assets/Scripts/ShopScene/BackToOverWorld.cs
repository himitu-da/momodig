using UnityEngine;

public class BackToOverWorld : MonoBehaviour
{
    public ChangeScene changescene;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void ChangeOverWorld()
    {
        changescene.OnClickToChangeScene("OverWorldScene");
    }
}
