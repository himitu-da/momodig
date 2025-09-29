using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ItemUI : MonoBehaviour
{
    private TMPro.TMP_Text productname;
    private TMPro.TMP_Text contentdescription;
    public string item_name;
    [TextArea] public string FlavorText;
    [SerializeField] private Text nametext;
    public void SetItem(ItemData item, TMPro.TMP_Text product, TMPro.TMP_Text content)
    {
        nametext.text = item.Itemname;
        productname = product;
        contentdescription = content;
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