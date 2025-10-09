using System.Collections.Generic;
using UnityEngine;

public class PopupCounter : MonoBehaviour
{
    private int countnum = 1;
    public List<PopMaterialTextManager> ActiveMaterialText;
    [SerializeField] private TMPro.TMP_Text countertxt;
    [SerializeField] PopBuyManager buymanager;
    public int getcount()
    {
        return countnum;
    }
    public void countchange(int number)
    {
        countnum = Mathf.Max(countnum + number, 1);

        countertxt.SetText(countnum.ToString());

        foreach (PopMaterialTextManager materialtext in ActiveMaterialText)
        {
            materialtext.UpdateText(countnum);
        }
        buymanager.boolbuyable(buymanager.buyable(buymanager.Materialrequest,countnum));
    }
    private void OnEnable()
    {
        countnum = 1;

        countertxt.SetText(countnum.ToString());
        foreach (PopMaterialTextManager materialtext in ActiveMaterialText)
        {
            materialtext.UpdateText(countnum);
        }
    }
    void Update()
    {
        
    }
}
