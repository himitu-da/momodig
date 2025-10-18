using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuyManager : MonoBehaviour
{
    [SerializeField] private GameObject Stone_Request;
    [SerializeField] private GameObject DragonOre_Request;
    [SerializeField] private GameObject Copper_Request;
    [SerializeField] private GameObject Iron_Request;
    [SerializeField] private GameObject Tin_Request;
    [SerializeField] private GameObject Nickel_Request;
    [SerializeField] private GameObject Sillicon_Request;
    [SerializeField] private GameObject Cobalt_Request;
    [SerializeField] private GameObject Titanium_Request;
    [SerializeField] private GameObject Sulfur_Request;
    [SerializeField] private GameObject Tungsten_Request;
    [SerializeField] private GameObject Hihiirokane_Request;
    [SerializeField] private GameObject Gold_Request;
    [SerializeField] private GameObject Rareearth_Request;
    [SerializeField] private GameObject Diamond_Request;
    [SerializeField] private StorageManager storage;

    [SerializeField] private MaterialCheck CheckList;
    [SerializeField] private GameObject popup;
    [SerializeField] private PopBuyManager popbuy;
    private List<RequestMaterial> requestmaterials;
    private List<GameObject> requests;
    public ItemData candidateitem;
    public void setmaterials(List<RequestMaterial> materialrequest)
    {
        requestmaterials = materialrequest;
    }
    void Start()
    {
        requests = new List<GameObject>(){Stone_Request,DragonOre_Request,Copper_Request,Iron_Request,Tin_Request,Nickel_Request,Sillicon_Request,Cobalt_Request,Titanium_Request,Sulfur_Request,Tungsten_Request,Hihiirokane_Request,Gold_Request,Rareearth_Request,Diamond_Request};
    }
    public void ChangeRequest()
    {
        foreach (GameObject request in requests)
        {
            if (request != null)
            {
                request.SetActive(false);
            }
            else
            {
                Debug.Log(request);
            }
        }
        foreach (RequestMaterial material in candidateitem.requestmaterials)
        {
            SetRequest(material.type == ResourceType.Stone, Stone_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.DragonGem, DragonOre_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Copper, Copper_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Iron, Iron_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Tin, Tin_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Nickel, Nickel_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Silicon, Sillicon_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Cobalt, Cobalt_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Titanium, Titanium_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            //SetRequest(material.type == ResourceType.Sulfur, Sulfur_Request);
            //SetRequest(material.type == ResourceType.Tungsten, Tungsten_Request);
            //SetRequest(material.type == ResourceType.Hihiirokane, Hihiirokane_Request);
            SetRequest(material.type == ResourceType.Gold, Gold_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);
            SetRequest(material.type == ResourceType.Diamond, Diamond_Request, material.correctamount(candidateitem.updatetype,candidateitem.itemlevel), material.type);

            //SetRequest(material.type == ResourceType.Rareearth, Rareearth_Request);
        }
    }
    public void BuyClick()
    {
        popup.SetActive(true);
        CheckList.requestchange(candidateitem.requestmaterials,candidateitem);
        popbuy.setitem(candidateitem);
        //CheckList.ChangeRequest();
    }
    void SetRequest(bool typecheck, GameObject request, int amount, ResourceType type)
    {
        if (typecheck)
        {
            request.SetActive(true);
            RequestTextManager requesttext = request.GetComponentInChildren<RequestTextManager>();
            if (requesttext != null)
            {
                requesttext.TextSet(amount, type);
            }
        }
    }
}
