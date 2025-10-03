using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContentManager : MonoBehaviour
{
    [SerializeField] private Transform scrollview;
    [SerializeField] private GameObject itemUIPrefab;
    [SerializeField] private ToggleGroup group;
    [Header("Assets/GameData/ShopDataにアイテムのデータ")] 
    [SerializeField] private ItemData[] items;
    [SerializeField] private TMPro.TMP_Text productname;
    [SerializeField] private TMPro.TMP_Text flavortext;
    [SerializeField] private GameObject Stone_Request;
    [SerializeField] private GameObject DragonOre_Request;
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
    void Start()
    {
        foreach (ItemData item in items)
        {
            GameObject setitem = Instantiate(itemUIPrefab, scrollview);
            ItemUI ui = setitem.GetComponent<ItemUI>();
            Toggle toggleset = setitem.GetComponent<Toggle>();
            ui.SetItem(item,productname,flavortext,Stone_Request,DragonOre_Request,Iron_Request,Tin_Request,Nickel_Request,Sillicon_Request,Cobalt_Request,Titanium_Request,Sulfur_Request,Tungsten_Request,Hihiirokane_Request,Gold_Request,Rareearth_Request);
            ui.item_name = item.Itemname;
            ui.FlavorText = item.FlavorText;
            toggleset.group = group;
            setitem.SetActive(true);
        }
    }
}
