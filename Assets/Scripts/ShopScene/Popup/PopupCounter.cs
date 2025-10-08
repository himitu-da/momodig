using System.Collections.Generic;
using UnityEngine;

public class PopupCounter : MonoBehaviour
{
    public int countnum = 1;
    public List<PopMaterialTextManager> ActiveMaterialText;
    [SerializeField] private TMPro.TMP_Text countertxt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void countchange(int number)
    {
        countnum = Mathf.Max(countnum + number, 1);

        countertxt.SetText(countnum.ToString());

        foreach (PopMaterialTextManager materialtext in ActiveMaterialText)
        {
            materialtext.UpdateText(countnum);
        }
    }
    void Start()
    {
        countertxt.SetText(countnum.ToString());
    }
    void Update()
    {
        
    }
}
