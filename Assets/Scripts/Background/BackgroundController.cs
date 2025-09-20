using UnityEngine;
using System.Collections.Generic;

public class BackgroundController : MonoBehaviour
{
    public TerrainDataManager terrainDataManager;
    public Transform playerTransform;
    public GameObject backgroundTilePrefab; // SpriteRendererを持つプレハブ

    [Tooltip("プレイヤーを中心に、タイル単位で表示する範囲")]
    public Vector2Int viewDistance = new Vector2Int(5, 3);
    [Tooltip("背景タイルのワールドユニットでのサイズ")]
    public Vector2 tileSize = Vector2.one;

    private Dictionary<Vector2Int, GameObject> activeTiles = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int playerGridPos;
    private MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else
            {
                Debug.LogError("Player not found.");
                enabled = false;
                return;
            }
        }

        if (backgroundTilePrefab == null)
        {
            Debug.LogError("Background Tile Prefab is not assigned.");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        // プレイヤーの現在地からグリッド座標を計算
        Vector2Int newPlayerGridPos = new Vector2Int(
            Mathf.RoundToInt(playerTransform.position.x / tileSize.x),
            Mathf.RoundToInt(playerTransform.position.y / tileSize.y)
        );

        // プレイヤーが別のグリッドに移動した場合のみ更新
        if (newPlayerGridPos != playerGridPos)
        {
            playerGridPos = newPlayerGridPos;
            UpdateTiles();
        }
    }

    void UpdateTiles()
    {
        // 不要になったタイルをリストアップ
        List<Vector2Int> tilesToRemove = new List<Vector2Int>();
        foreach (var tilePos in activeTiles.Keys)
        {
            if (Mathf.Abs(tilePos.x - playerGridPos.x) > viewDistance.x ||
                Mathf.Abs(tilePos.y - playerGridPos.y) > viewDistance.y)
            {
                tilesToRemove.Add(tilePos);
            }
        }

        // リストアップしたタイルを削除
        foreach (var tilePos in tilesToRemove)
        {
            Destroy(activeTiles[tilePos]);
            activeTiles.Remove(tilePos);
        }

        // 新しく必要になったタイルを生成
        for (int x = -viewDistance.x; x <= viewDistance.x; x++)
        {
            for (int y = -viewDistance.y; y <= viewDistance.y; y++)
            {
                Vector2Int tilePos = new Vector2Int(playerGridPos.x + x, playerGridPos.y + y);
                if (!activeTiles.ContainsKey(tilePos))
                {
                    CreateTile(tilePos);
                }
            }
        }
    }

    void CreateTile(Vector2Int gridPos)
    {
        // グリッド座標からワールド座標を計算
        Vector3 worldPos = new Vector3(gridPos.x * tileSize.x, gridPos.y * tileSize.y, transform.position.z);
        
        GameObject newTile = Instantiate(backgroundTilePrefab, worldPos, Quaternion.identity, transform);
        newTile.transform.localScale = Vector3.one; // スケールをリセット
        newTile.name = $"BackgroundTile_{gridPos.x}_{gridPos.y}";

        // タイルのY座標に基づいてバイオームとテクスチャを決定
        int height = Mathf.FloorToInt(worldPos.y);
        BiomeData biome = terrainDataManager.GetBiomeForHeight(height);
        
        Texture2D texture = terrainDataManager.defaultBackgroundTexture;
        if (biome != null && biome.backgroundTexture != null)
        {
            texture = biome.backgroundTexture;
        }

        // テクスチャをスプライトとして設定
        if (texture != null)
        {
            SpriteRenderer tileRenderer = newTile.GetComponent<SpriteRenderer>();
            
            // プレハブのスプライトを差し替えるのではなく、テクスチャだけを上書きする
            propertyBlock.SetTexture("_MainTex", texture);
            tileRenderer.SetPropertyBlock(propertyBlock);
        }

        activeTiles.Add(gridPos, newTile);
    }
}
