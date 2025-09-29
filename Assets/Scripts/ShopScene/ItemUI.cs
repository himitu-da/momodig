using UnityEngine;
using UnityEngine.UI;
public class ItemUI : MonoBehaviour
{
    [SerializeField] private Text nametext;
    public void SetItem(ItemData item)
    {
        nametext.text = item.Itemname;
    }
}