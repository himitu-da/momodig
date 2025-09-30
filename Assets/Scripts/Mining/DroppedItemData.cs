using UnityEngine;

/// <summary>
/// シーン間で永続化するためのドロップアイテムの情報を保持する構造体
/// </summary>
[System.Serializable]
public struct DroppedItemData
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public string blockDataName; // BlockDataのアセット名を保存
    public Vector2 uvBase;
    public Vector2 uvSize;
    public bool useTexture1;
}
