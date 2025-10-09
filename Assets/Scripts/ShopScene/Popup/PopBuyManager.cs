using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
public class PopBuyManager : MonoBehaviour
{
    [SerializeField]private Button popbuybutton;
    [SerializeField] private StorageManager storage;
    [SerializeField] private PopupCounter counter;
    public List<RequestMaterial> Materialrequest;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void boolbuyable(bool buyable)
    {
        popbuybutton.interactable = buyable;
    }
    public bool buyable(List<RequestMaterial> materialrequests,int count)
    {
        if (materialrequests != null && materialrequests.Count != 0)
        {
            foreach (RequestMaterial request in materialrequests)
            {
                if (storage.GetResourceAmount(request.type) - request.amount * count < 0)
                {
                    return false;
                }
            }
            return true;
        }
        else
        {
            Debug.Log("PopBuyManager.cs materialrequests is not set L14~31");
            return false;
        }
    }
    public void SetMaterialrequest(List<RequestMaterial> requests)
    {
        this.Materialrequest = requests;
        Debug.Log(Materialrequest.Count);
        boolbuyable(buyable(requests,counter.getcount()));
    }
}