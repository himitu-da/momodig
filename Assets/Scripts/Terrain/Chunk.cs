using UnityEngine;

/// <summary>
/// チャンクのデータを管理するクラス
/// </summary>
public class Chunk : MonoBehaviour
{
    public Vector3Int chunkPosition;

    public void Initialize(Vector3Int position)
    {
        chunkPosition = position;
    }
}
