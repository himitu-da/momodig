using System.Collections.Generic;
using UnityEngine;

    /// <summary>
    /// Vector3Intをシリアライズ可能にするための構造体
    /// </summary>
    [System.Serializable]
    public struct SerializableVector3Int
    {
        public int x;
        public int y;
        public int z;

        public SerializableVector3Int(Vector3Int vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }

        public Vector3Int ToVector3Int()
        {
            return new Vector3Int(x, y, z);
        }
    }
    
    /// <summary>
    /// Vector3をシリアライズ可能にするための構造体
    /// </summary>
    [System.Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(Vector3 vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    /// <summary>
    /// 個々のブロックの状態を保存するデータクラス
    /// </summary>
    [System.Serializable]
    public class BlockSaveData
    {
        public SerializableVector3Int position;
        public int resourceTypeId; // ResourceTypeのenumのインデックスを保存
        public bool[] voxelPattern; // 3D配列を1Dにフラット化して保存
        public int voxelSize; // パターンの復元に必要
    }

    /// <summary>
    /// ワールド全体のブロックデータを保存するクラス
    /// 差分保存を想定し、変更があったブロックのみをリストに保持する
    /// </summary>
    [System.Serializable]
    public class WorldSaveData
    {
        public List<BlockSaveData> modifiedBlocks = new List<BlockSaveData>();
    }

    /// <summary>
    /// ドロップアイテムの状態を保存するデータクラス
    /// </summary>
    [System.Serializable]
    public class DroppedItemSaveData
    {
        public int itemId;
        public SerializableVector3 position;
    }

    /// <summary>
    /// すべてのドロップアイテムデータを保存するクラス
    /// </summary>
    [System.Serializable]
    public class DroppedItemsSaveData
    {
        public List<DroppedItemSaveData> droppedItems = new List<DroppedItemSaveData>();
    }
