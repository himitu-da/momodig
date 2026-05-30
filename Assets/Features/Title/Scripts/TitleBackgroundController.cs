using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// タイトルシーンの背景を管理するコントローラー。
/// ブロックのスプライトをタイル状に配置し、動的な動きをつける。
/// </summary>
public class TitleBackgroundController : MonoBehaviour
{
    [Header("Tile Settings")]
    [Tooltip("背景に使用するブロックデータのリスト")]
    public List<BlockData> blockDataList;

    [Tooltip("タイルとして使用するプレハブ (SpriteRendererを持つこと)")]
    public GameObject tilePrefab;

    [Tooltip("タイトル背景の表示範囲を決めるカメラ")]
    [SerializeField] private Camera titleCamera;

    [Tooltip("タイルのサイズ")]
    public Vector2 tileSize = Vector2.one;

    [Header("Animation Settings")]
    [Tooltip("タイルの上昇速度")]
    public float horizontalScrollSpeed = 0.5f;

    [Tooltip("左右の揺れの振幅")]
    public float verticalAmplitude = 0.2f;

    [Tooltip("左右の揺れの速さ")]
    public float verticalBobSpeed = 1.0f;

    // 各タイルのアニメーション用情報を保持するクラス
    private class TileInfo
    {
        public Transform transform;
        public Vector3 initialPosition;
    }

    private List<TileInfo> tiles = new List<TileInfo>();
    private float rightBoundary;
    private float wrapWidth;

    void Start()
    {
        if (titleCamera == null)
        {
            Debug.LogError("TitleBackgroundController: titleCamera is not configured.", this);
            enabled = false;
            return;
        }

        if (tilePrefab == null)
        {
            Debug.LogError("Tile Prefab is not assigned.");
            enabled = false;
            return;
        }

        if (blockDataList == null || blockDataList.Count == 0)
        {
            Debug.LogError("Block Data List is not assigned or empty.");
            enabled = false;
            return;
        }

        GenerateTiles();

        // ラッピング（画面外に出たタイルを反対側に移動させる）のための境界を計算
        float cameraHeight = titleCamera.orthographicSize * 2;
        // 画面を覆っているタイル全体の幅を計算
        float screenAspect = (float)Screen.width / Screen.height;
        Vector2 cameraSize = new Vector2(cameraHeight * screenAspect, cameraHeight);
        rightBoundary = titleCamera.transform.position.x + cameraSize.x * 0.5f + tileSize.x;
        int tilesX = Mathf.CeilToInt(cameraSize.x / tileSize.x) + 2;
        wrapWidth = tilesX * tileSize.x;
    }

    void Update()
    {
        AnimateTiles();
    }

    /// <summary>
    /// 画面を埋めるようにタイルを生成する
    /// </summary>
    void GenerateTiles()
    {
        // カメラのビューポートからワールド座標での表示範囲を取得
        float screenAspect = (float)Screen.width / Screen.height;
        float cameraHeight = titleCamera.orthographicSize * 2;
        Vector2 cameraSize = new Vector2(cameraHeight * screenAspect, cameraHeight);

        // 画面を覆うのに必要なタイルの数を計算 (余裕を持たせる)
        int tilesX = Mathf.CeilToInt(cameraSize.x / tileSize.x) + 2;
        int tilesY = Mathf.CeilToInt(cameraSize.y / tileSize.y) + 2;

        for (int y = -tilesY / 2; y <= tilesY / 2; y++)
        {
            for (int x = -tilesX / 2; x <= tilesX / 2; x++)
            {
                CreateTile(x, y);
            }
        }
    }

    /// <summary>
    /// 指定されたグリッド座標にタイルを1つ作成する
    /// </summary>
    void CreateTile(int x, int y)
    {
        Vector3 position = new Vector3(x * tileSize.x, y * tileSize.y, transform.position.z);
        GameObject newTileObj = Instantiate(tilePrefab, position, Quaternion.identity, transform);
        newTileObj.transform.localScale = Vector3.one;
        newTileObj.name = $"Tile_{x}_{y}";

        SpriteRenderer renderer = newTileObj.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            // ランダムなブロックデータを取得
            BlockData randomBlockData = blockDataList[Random.Range(0, blockDataList.Count)];
            if (randomBlockData.textures != null && randomBlockData.textures.Count > 0)
            {
                // テクスチャからスプライトを作成して設定
                Texture2D texture = randomBlockData.textures[Random.Range(0, randomBlockData.textures.Count)];
                Rect rect = new Rect(0, 0, texture.width, texture.height);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                // Pixels Per Unitをテクスチャの幅に設定し、スプライトが1x1ユニットになるようにする
                renderer.sprite = Sprite.Create(texture, rect, pivot, texture.width);
            }
        }

        // アニメーション用に情報を保存
        tiles.Add(new TileInfo
        {
            transform = newTileObj.transform,
            initialPosition = position
        });
    }

    /// <summary>
    /// タイルをアニメーションさせる
    /// </summary>
    void AnimateTiles()
    {
        foreach (var tile in tiles)
        {
            // 1. 新しいX座標を計算（右方向）
            float newX = tile.transform.position.x + horizontalScrollSpeed * Time.deltaTime;

            // 2. 新しいY座標を計算（上下の揺れ）
            float verticalOffset = Mathf.Sin(Time.time * verticalBobSpeed) * verticalAmplitude;
            float newY = tile.initialPosition.y + verticalOffset;

            // 3. 新しい座標を適用
            tile.transform.position = new Vector3(newX, newY, tile.initialPosition.z);

            // 4. 画面の右端を超えたら、左端に移動させる（ラッピング）
            if (tile.transform.position.x > rightBoundary)
            {
                tile.transform.position -= new Vector3(wrapWidth, 0, 0);
            }
        }
    }
}
