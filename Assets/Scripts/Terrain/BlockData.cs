using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロックの種類ごとの設定を管理するデータアセット。
/// ResourceTypeをキーとして、HP、テクスチャ、ドロップアイテムなどの情報を定義する。
/// </summary>
[CreateAssetMenu(fileName = "New BlockData", menuName = "Momodig/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Block Identity")]
    public ResourceType resourceType; // Stone, Ironなど

    [Header("Block Properties")]
    public int voxelHp = 2;
    
    // 複数のテクスチャに対応
    public List<Texture2D> textures;
    
    [Header("Dropped Item Settings")]
    public GameObject droppedItemPrefab; // このブロックがドロップするアイテムのPrefab
    public bool disableRotation = true;
    public bool autoScale = true;
    public float scaleMultiplier = 1.0f;
}
