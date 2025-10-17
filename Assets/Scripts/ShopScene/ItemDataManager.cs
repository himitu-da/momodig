using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Shop/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("基本情報")]    //アイテム名
    public String Itemname;
    [TextArea] public string FlavorText;
    public int price;
    [Header("必要素材")]
    public List<RequestMaterial> requestmaterials = new List<RequestMaterial>();
    [Header("強化レベル")]
    public int itemlevel = 0;
    [Header("レベル補正種")]
    public ItemUpdateType updatetype = ItemUpdateType.factorial;
}
[System.Serializable]
public class RequestMaterial
{
    public ResourceType type;       //Common/ResourceTypesを見る
    public int amount;
    public int correctamount(ItemUpdateType type,int level)
    {
        return amount * updatetypenum(type,level);
    }
    int updatetypenum(ItemUpdateType type, int level)
    {
        int returnnum = 1;
        if (type == ItemUpdateType.factorial)
        {
            returnnum = factorialnum(level);
        }
        else if (type == ItemUpdateType.nochange)
        {
            returnnum = 1;
        }
        else if (type == ItemUpdateType.multiply)
        {
            returnnum = level;
        }else if(type == ItemUpdateType.fibonacci)
        {
            returnnum = fibonacci(level);
        }
        return returnnum;
    }
    int fibonacci(int level)
    {
        int current = 1;
        int next = 1;
        int tmp;

        for (int i = 1; i < level; i++)
        {
            tmp = current + next;
            current = next;
            next = tmp;
        }
        return current;
    }
    int factorialnum(int level)
    {
        int factorialresult = 1;
        for (int i = 0; i < level; i++)
        {
            factorialresult = factorialresult * (i + 1);
        }
        return factorialresult;
    }
}
public enum ItemUpdateType
{
    factorial,
    nochange,
    multiply,
    fibonacci
}