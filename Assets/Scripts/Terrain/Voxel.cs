using UnityEngine;

/// <summary>
/// ボクセルデータ構造
/// </summary>
[System.Serializable]
public class Voxel
{
    public Vector3Int blockPosition;     // 所属ブロック座標
    public Vector3Int localPosition;    // ブロック内でのローカル座標
    public Vector3 worldPosition;       // ワールド座標
    public bool isActive;               // ボクセルがアクティブかどうか
    public int health;                  // ボクセルの耐久値
    public int maxHealth;               // ボクセルの最大耐久値
    public VoxelType voxelType;         // ボクセルタイプ
    public float lastModifiedTime;      // 最後に変更された時間
    
    public Voxel(Vector3Int blockPos, Vector3Int localPos, Vector3 worldPos, int hp, VoxelType type)
    {
        blockPosition = blockPos;
        localPosition = localPos;
        worldPosition = worldPos;
        isActive = true;
        health = hp;
        maxHealth = hp; // 最大HPも初期HPと同じに設定
        voxelType = type;
        lastModifiedTime = Time.time;
    }
}

/// <summary>
/// ボクセルタイプ列挙型
/// </summary>
public enum VoxelType
{
    Standard,      // 標準ボクセル
    Reinforced,    // 強化ボクセル
    Fragile,       // 脆弱ボクセル
    Unbreakable,   // 破壊不能ボクセル
    Special        // 特殊ボクセル
}
