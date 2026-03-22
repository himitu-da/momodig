using UnityEngine;

[System.Serializable]
public struct DroppedItemFaceTextureData
{
    public Vector2 uvBase;
    public Vector2 uvSize;
    public bool useTexture1;
    public bool hasTexture;

    public DroppedItemFaceTextureData(Vector2 uvBase, Vector2 uvSize, bool useTexture1, bool hasTexture)
    {
        this.uvBase = uvBase;
        this.uvSize = uvSize;
        this.useTexture1 = useTexture1;
        this.hasTexture = hasTexture;
    }
}
