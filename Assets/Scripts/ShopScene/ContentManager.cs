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
    void Start()
    {
        foreach (ItemData item in items)
        {
            GameObject setitem = Instantiate(itemUIPrefab, scrollview);
            ItemUI ui = setitem.GetComponent<ItemUI>();
            Toggle toggleset = setitem.GetComponent<Toggle>();
            ui.SetItem(item,productname,flavortext);
            ui.item_name = item.Itemname;
            ui.FlavorText = item.FlavorText;
            toggleset.group = group;
            setitem.SetActive(true);
        }
    }
}
