using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName =  "NewItem",menuName = "Shop/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("基本情報")]    //アイテム名
    public String Itemname;
    [TextArea] public string FlavorText;
    public int price;
    [Header("必要素材")]
    public List<RequestMaterial> requestmaterials = new List<RequestMaterial>();
}
[System.Serializable]
public class RequestMaterial
{
    public ResourceType type;       //Common/ResourceTypesを見る
    public int amount;
}