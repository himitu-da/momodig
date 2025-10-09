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
    private BuyManager buymanager;
    private List<RequestMaterial> requestmaterials;
    private ItemData candidateitem;
    public void SetItem(ItemData item, TMPro.TMP_Text product, TMPro.TMP_Text content, BuyManager buymanager)
    {
        candidateitem = item;
        nametext.text = item.Itemname;
        productname = product;
        contentdescription = content;
        this.requestmaterials = item.requestmaterials;
        this.buymanager = buymanager;
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
        buymanager.setmaterials(requestmaterials);
        buymanager.ChangeRequest();
        buymanager.candidateitem = this.candidateitem;
    }
}