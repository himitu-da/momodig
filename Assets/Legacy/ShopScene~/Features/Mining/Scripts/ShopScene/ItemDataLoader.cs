using System.Collections.Generic;
using UnityEngine;

public class ItemDataLoader : MonoBehaviour
{
    //public ItemData testitem;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        //GameDataPersistenceManager.Instance.purchaseditems.Add(testitem, 2);
        foreach (KeyValuePair<ItemData, int> item in GameDataPersistenceManager.Instance.purchaseditems)
        {
            item.Key.itemlevel = item.Value;
            Debug.Log($"{item.Key}: {item.Key.itemlevel}({item.Value})");
        }
    }
}
