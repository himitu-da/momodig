using UnityEngine;

[System.Serializable]
public struct DroppedItemData
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public string blockDataName;
    public DroppedItemFaceTextureData[] faceTextureData;
    public Vector2 uvBase;
    public Vector2 uvSize;
    public bool useTexture1;
    public bool hasSolidificationData;
    public bool canSolidify;
    public bool hasSolidificationTarget;
    public Vector3Int solidifiedBlockPosition;
    public Vector3Int solidifiedLocalVoxelPosition;
    public float solidificationElapsedSeconds;
}
