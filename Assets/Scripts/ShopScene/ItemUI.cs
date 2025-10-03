using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
public class ItemUI : MonoBehaviour
{
    private TMPro.TMP_Text productname;
    private TMPro.TMP_Text contentdescription;
    public string item_name;
    [TextArea] public string FlavorText;
    [SerializeField] private Text nametext;
    private GameObject Stone_Request;
    private GameObject DragonOre_Request;
    private GameObject Iron_Request;
    private GameObject Tin_Request;
    private GameObject Nickel_Request;
    private GameObject Sillicon_Request;
    private GameObject Cobalt_Request;
    private GameObject Titanium_Request;
    private GameObject Sulfur_Request;
    private GameObject Tungsten_Request;
    private GameObject Hihiirokane_Request;
    private GameObject Gold_Request;
    private GameObject Rareearth_Request;
    private List<RequestMaterial> materiallist;
    public void SetItem(ItemData item, TMPro.TMP_Text product, TMPro.TMP_Text content, GameObject Stone_Request, GameObject DragonOre_Request, GameObject Iron_Request, GameObject Tin_Request, GameObject Nickel_Request, GameObject Sillicon_Request, GameObject Cobalt_Request, GameObject Titanium_Request, GameObject Sulfur_Request, GameObject Tungsten_Request, GameObject Hihiirokane_Request, GameObject Gold_Request, GameObject Rareearth_Request)
    {
        nametext.text = item.Itemname;
        productname = product;
        contentdescription = content;
        materiallist = item.requestmaterials;
        this.Stone_Request = Stone_Request;
        this.DragonOre_Request = DragonOre_Request;
        this.Iron_Request = Iron_Request;
        this.Tin_Request = Tin_Request;
        this.Nickel_Request = Nickel_Request;
        this.Sillicon_Request = Sillicon_Request;
        this.Cobalt_Request = Cobalt_Request;
        this.Titanium_Request = Titanium_Request;
        this.Sulfur_Request = Sulfur_Request;
        this.Tungsten_Request = Tungsten_Request;
        this.Hihiirokane_Request = Hihiirokane_Request;
        this.Gold_Request = Gold_Request;
        this.Rareearth_Request = Rareearth_Request;
    }
    public void ChangeInfo()
    {
        if (productname != null && contentdescription != null)
        {
            productname.SetText(item_name);
            contentdescription.SetText(FlavorText);
        }
        else
        {
            Debug.Log("Productname or contentdescription is not set");
        }
    }
}