using UnityEngine;

public class PopMaterialTextManager : MonoBehaviour
{
    [SerializeField] private string materialname;
    [SerializeField] private TMPro.TMP_Text text;
    [SerializeField] private StorageManager Storage;
    private int parmaterialamount;
    [SerializeField] private ResourceType type;
    [SerializeField] private PopupCounter counter;
    [SerializeField] private PopBuyManager popbuymanager;
    public void GetParAmount(int paramount)
    {
        parmaterialamount = paramount;
        UpdateText(counter.getcount());
    }
    public void UpdateText(int count)
    {
        text.SetText($"{materialname}      {Storage.GetResourceAmount(type)} - {parmaterialamount*count} = {Storage.GetResourceAmount(type)-parmaterialamount*count}");
    }
    /*
    public void TextSet(int amount, ResourceType type)
    {
        parmaterialamount = amount;
        this.type = type;
        text.SetText($"{materialname} {parmaterialamount} ({Storage.GetResourceAmount(type)})");
    }*/
}