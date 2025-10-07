using UnityEngine;

public class PopupCounter : MonoBehaviour
{
    private int countnum = 1;
    [SerializeField] private TMPro.TMP_Text countertxt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void countchange(int number)
    {
        countnum = Mathf.Max(countnum+number,1);

        countertxt.SetText(countnum.ToString());
    }
    public int getcount()
    {
        return countnum;
    }
    void Start()
    {
        countertxt.SetText(countnum.ToString());
    }
    void Update()
    {
        
    }
}
