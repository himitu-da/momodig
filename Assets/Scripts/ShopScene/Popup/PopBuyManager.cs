using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using JetBrains.Annotations;
public class PopBuyManager : MonoBehaviour
{
    [SerializeField]private Button popbuybutton;
    [SerializeField] private StorageManager storage;
    [SerializeField] private PopupCounter counter;
    private ItemData item;
    public List<RequestMaterial> Materialrequest;
    [SerializeField] private GameObject popup;
    public void setitem(ItemData candidate)
    {
        item = candidate;
    }
    public void buyitem()
    {
        if (GameDataPersistenceManager.Instance.purchaseditems.ContainsKey(item))
        {
            GameDataPersistenceManager.Instance.purchaseditems[item] = item.itemlevel;
        }
        else
        {
            GameDataPersistenceManager.Instance.purchaseditems.Add(item, item.itemlevel);
        }
        foreach (RequestMaterial requestmaterial in item.requestmaterials)
        {
            storage.AddResource(requestmaterial.type, requestmaterial.correctamount(item.itemlevel) * (-1));
        }
    }
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
    int factorialnum(int level){
        int factorialresult = 1;
        for(int i = 0; i < level; i++){
            factorialresult = factorialresult * (i+1);
        }
        return factorialresult;
    }
}