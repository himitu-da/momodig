using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ゲームのセッション中、シーンをまたいでデータを保持するクラス。
/// シード値や破壊されたブロックの情報などを管理します。
/// </summary>
public class GameDataPersistenceManager : MonoBehaviour
{
    // シングルトンインスタンス
    private static GameDataPersistenceManager _instance;
    public static GameDataPersistenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameDataPersistenceManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameDataPersistenceManager");
                    _instance = go.AddComponent<GameDataPersistenceManager>();
                }
            }
            return _instance;
        }
    }

    // --- 永続化するデータ ---

    [Header("地形データ")]
    public int terrainSeed;
    public bool hasInitializedSeed = false; // シードが初期化されたかどうか

    [Header("破壊済みブロック")]
    public HashSet<Vector3Int> destroyedBlockPositions = new HashSet<Vector3Int>();
    public Dictionary<Vector3Int, HashSet<Vector3Int>> partiallyDestroyedBlocks = new Dictionary<Vector3Int, HashSet<Vector3Int>>();

    [Header("プレイヤーデータ")]
    public Dictionary<ResourceType, int> storedResources = new Dictionary<ResourceType, int>();


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
