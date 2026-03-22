using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using JetBrains.Annotations;
public class PopBuyManager : MonoBehaviour
{
    [SerializeField]private Button popbuybutton;
    [SerializeField] private StorageManager storage;
    //[SerializeField] private PopupCounter counter;
    [SerializeField] private TMPro.TMP_Text productname;
    [SerializeField] private BuyManager buymanager;
    public ItemData item;
    public List<RequestMaterial> Materialrequest;
    //[SerializeField] private GameObject popup;
    //[SerializeField] private ItemUI itemui;
    
    public void setitem(ItemData candidate)
    {
        item = candidate;
    }
    public void buyitem()
    {
        foreach (RequestMaterial requestmaterial in item.requestmaterials)
        {
            storage.AddResource(requestmaterial.type, requestmaterial.correctamount(item.updatetype,item.itemlevel) * (-1));
        }
        //購入後アイテムデータ更新
        item.itemlevel += 1;
        if (GameDataPersistenceManager.Instance.purchaseditems.ContainsKey(item))
        {
            GameDataPersistenceManager.Instance.purchaseditems[item] = item.itemlevel;
        }
        else
        {
            GameDataPersistenceManager.Instance.purchaseditems.Add(item, item.itemlevel);
        }
        productname.SetText($"{item.Itemname} ({item.itemlevel}Lv)");
        buymanager.ChangeRequest();
        //itemui.ChangeInfo();
    }
    public void boolbuyable(bool buyable)
    {
        popbuybutton.interactable = buyable;
    }
    public bool buyable(ItemData item,int count)
    {
        if (item.requestmaterials != null && item.requestmaterials.Count != 0)
        {
            Debug.Log($"materialrequests:{item.requestmaterials.Count}");
            foreach (RequestMaterial request in item.requestmaterials)
            {
                Debug.Log($"{request.type}:{storage.GetResourceAmount(request.type)} - {request.correctamount(item.updatetype,item.itemlevel) * count} = {storage.GetResourceAmount(request.type) - request.amount * count}");
                if (storage.GetResourceAmount(request.type) - request.correctamount(item.updatetype,item.itemlevel) * count < 0)
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
    public void SetMaterialrequest(ItemData item)
    {
        //this.Materialrequest = requests;
        //Debug.Log(Materialrequest.Count);
        //boolbuyable(buyable(requests,counter.getcount()));
        boolbuyable(buyable(item,1));
    }
}