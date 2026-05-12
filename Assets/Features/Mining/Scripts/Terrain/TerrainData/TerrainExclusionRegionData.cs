using UnityEngine;

[CreateAssetMenu(fileName = "New Terrain Exclusion Region", menuName = "Momodig/Terrain Exclusion Region")]
public class TerrainExclusionRegionData : ScriptableObject
{
    [Header("Region Settings")]
    public string regionName;

    [Header("Block Bounds")]
    public Vector3Int minBlockPosition;
    public Vector3Int maxBlockPosition;

    public bool ContainsBlock(Vector3Int blockPosition)
    {
        Vector3Int min = Vector3Int.Min(minBlockPosition, maxBlockPosition);
        Vector3Int max = Vector3Int.Max(minBlockPosition, maxBlockPosition);

        return blockPosition.x >= min.x && blockPosition.x <= max.x &&
               blockPosition.y >= min.y && blockPosition.y <= max.y &&
               blockPosition.z >= min.z && blockPosition.z <= max.z;
    }
}
