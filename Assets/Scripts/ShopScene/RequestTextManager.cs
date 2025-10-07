using UnityEngine;
public class RequestTextManager : MonoBehaviour
{
    [SerializeField] private string materialname;
    [SerializeField] private TMPro.TMP_Text text;
    [SerializeField] private StorageManager Storage;
    private int materialamount;
    private ResourceType type;
    public void TextSet(int amount,ResourceType type)
    {
        materialamount = amount;
        this.type = type;
        text.SetText($"{materialname} {materialamount} ({Storage.GetResourceAmount(type)})");
        //text.SetText($"{materialname} {materialamount} ()");    //StorageManager見つかるまでの仮
    }
}
