using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ブロックの種類ごとの設定を管理するデータアセット。
/// ResourceTypeをキーとして、HP、テクスチャ、ドロップアイテムなどの情報を定義する。
/// </summary>
[CreateAssetMenu(fileName = "New BlockData", menuName = "Momodig/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Block Properties")]
    public ResourceType resourceType;
    public int voxelHp = 2;
    public MaterialType materialType;
    
    // 複数のテクスチャに対応
    public List<Texture2D> textures;
    
    [Header("Dropped Item Settings")]
    public GameObject droppedItemPrefab; // このブロックがドロップするアイテムのPrefab
    public bool disableRotation = true;
    public bool autoScale = true;
    public float scaleMultiplier = 1.0f;

    [Header("Voxel Physics")]
    public float density = 2700f; // 密度 (kg/m^3) e.g. Stone

    [Header("Sound")]
    public AudioClip destroyedSound;
    [Range(0.0f, 2.0f)]
    public float destroyedSoundVolume = 1.0f;
}
