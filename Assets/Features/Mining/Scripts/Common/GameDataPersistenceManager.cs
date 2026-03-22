using UnityEngine;
using System.Collections.Generic;
using System;

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

    // --- Events ---
    public static event Action OnPurchasedItemsChanged;

    // --- 永続化するデータ ---

    [Header("地形データ")]
    public int terrainSeed;
    public bool hasInitializedSeed = false; // シードが初期化されたかどうか

    [Header("破壊済みブロック")]
    public HashSet<Vector3Int> destroyedBlockPositions = new HashSet<Vector3Int>();
    public Dictionary<Vector3Int, HashSet<Vector3Int>> partiallyDestroyedBlocks = new Dictionary<Vector3Int, HashSet<Vector3Int>>();

    [Header("プレイヤーデータ")]
    public Dictionary<ResourceType, int> storedResources = new Dictionary<ResourceType, int>();

    [Header("ドロップアイテムデータ")]
    public List<DroppedItemData> droppedItems = new List<DroppedItemData>();

    [Header("購入済み商品データ")]
    public Dictionary<ItemData, int> purchaseditems = new Dictionary<ItemData, int>();
    
    /// <summary>
    /// purchaseditemsが変更されたことを通知します。
    /// アイテムの購入やロード後に呼び出してください。
    /// </summary>
    public void NotifyPurchasedItemsChanged()
    {
        OnPurchasedItemsChanged?.Invoke();
    }

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
