using UnityEngine;
using UnityEngine.UI;

public class ContentManager : MonoBehaviour
{
    [SerializeField] private Transform scrollview;
    [SerializeField] private GameObject itemUIPrefab;
    [SerializeField] private ToggleGroup group;
    [Header("Assets/GameData/ShopDataにアイテムのデータ")] 
    [SerializeField] private ItemData[] items;
    void Start()
    {
        foreach (ItemData item in items)
        {
            GameObject setitem = Instantiate(itemUIPrefab, scrollview);
            ItemUI ui = setitem.GetComponent<ItemUI>();
            Toggle toggleset = setitem.GetComponent<Toggle>();
            ui.SetItem(item);
            toggleset.group = group;
            setitem.SetActive(true);
        }
    }
}
