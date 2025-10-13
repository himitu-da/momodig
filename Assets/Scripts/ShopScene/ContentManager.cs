using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContentManager : MonoBehaviour
{
    [SerializeField] private Transform scrollview;

    [SerializeField] private GameObject itemUIPrefab;

    [SerializeField] private ToggleGroup group;
    [Header("Assets/GameData/ShopDataにアイテムのデータ")] 
    [SerializeField] private ItemData[] items;  //ItemDataManagerを確認

    [SerializeField] private TMPro.TMP_Text productname;
    [SerializeField] private TMPro.TMP_Text flavortext;

    [SerializeField] private BuyManager buymanager;
    void Start()
    {
        foreach (ItemData item in items)
        {
            GameObject setitem = Instantiate(itemUIPrefab, scrollview);
            setitem.SetActive(true);
            ItemUI ui = setitem.GetComponent<ItemUI>();
            Toggle toggleset = setitem.GetComponent<Toggle>();
            if (toggleset == null) { Debug.Log("toggleset is not set"); }
            if(group != null)
            {
                toggleset.group = group;
            }
            else
            {
                Debug.Log("group is not set");
            }
            ui.SetItem(item,productname,flavortext,buymanager);
            ui.item_name = item.Itemname;
            ui.FlavorText = item.FlavorText;
        }
    }
}
