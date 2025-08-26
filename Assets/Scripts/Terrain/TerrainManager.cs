using UnityEngine;

/// <summary>
/// 地形生成タイプ列挙型
/// </summary>
public enum TerrainGenerationType
{
    SideScroller,    // XY平面（現在のCubeSideScrollerPlacerと同等）
    TopDown,         // XZ平面（現在のCubeTopDownPlacerと同等）
    Custom          // カスタム
}

/// <summary>
/// 地形設定データ構造
/// </summary>
[System.Serializable]
public class TerrainSettings
{
    [Header("Basic Settings")]
    public Vector3Int center = Vector3Int.zero;
    public Vector2Int chunkCount = new Vector2Int(10, 5);
    public float chunkSize = 1.0f;
    public int voxelSize = 4;
    public int voxelHp = 2;
    
    [Header("Texture Settings")]
    public Texture2D texture1;
    public Texture2D texture2;
    
    [Header("Dropped Item Settings")]
    public GameObject droppedItemPrefab;
    public bool disableRotation = true;
    public bool autoScale = true;
    public float scaleMultiplier = 1.0f;
    
    [Header("Generation Type")]
    public TerrainGenerationType generationType = TerrainGenerationType.SideScroller;
}

/// <summary>
/// 地形全体を管理するマネージャー
/// WorldGeneratorオブジェクトにアタッチして使用
/// </summary>
public class TerrainManager : MonoBehaviour
{
    [Header("Terrain Configuration")]
    [SerializeField] private TerrainSettings settings = new TerrainSettings();
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    /// <summary>
    /// 地形設定の取得
    /// </summary>
    public TerrainSettings Settings => settings;

    void Start()
    {
        GenerateTerrain();
    }

    /// <summary>
    /// 地形を生成
    /// </summary>
    public void GenerateTerrain()
    {
        if (showDebugInfo)
        {
            Debug.Log($"TerrainManager: Generating terrain with type {settings.generationType}");
        }

        switch (settings.generationType)
        {
            case TerrainGenerationType.SideScroller:
                GenerateSideScrollerTerrain();
                break;
            case TerrainGenerationType.TopDown:
                GenerateTopDownTerrain();
                break;
            case TerrainGenerationType.Custom:
                GenerateCustomTerrain();
                break;
        }
    }

    /// <summary>
    /// サイドスクローラータイプの地形生成（XY平面）
    /// </summary>
    private void GenerateSideScrollerTerrain()
    {
        // チャンク全体のワールドサイズを計算
        float totalWorldSizeX = settings.chunkCount.x * settings.chunkSize;
        float totalWorldSizeY = settings.chunkCount.y * settings.chunkSize;

        // チャンク群の左下奥にあるチャンクの中心座標を計算
        Vector3 startPosition = new Vector3(
            settings.center.x - totalWorldSizeX / 2.0f + settings.chunkSize / 2.0f,
            settings.center.y - totalWorldSizeY / 2.0f + settings.chunkSize / 2.0f,
            settings.center.z
        );

        transform.position = startPosition;

        // chunkCountに基づいてチャンクを生成
        for (int x = 0; x < settings.chunkCount.x; x++)
        {
            for (int y = 0; y < settings.chunkCount.y; y++)
            {
                Vector3Int chunkPos = new Vector3Int(x, y, 0);
                bool[,,] pattern = GenerateSideScrollerPattern();
                CreateChunk(chunkPos, pattern);
            }
        }
    }

    /// <summary>
    /// トップダウンタイプの地形生成（XZ平面）
    /// </summary>
    private void GenerateTopDownTerrain()
    {
        // チャンク全体のワールドサイズを計算
        float totalWorldSizeX = settings.chunkCount.x * settings.chunkSize;
        float totalWorldSizeZ = settings.chunkCount.y * settings.chunkSize;

        // チャンク群の左下にあるチャンクの中心座標を計算
        Vector3 startPosition = new Vector3(
            settings.center.x - totalWorldSizeX / 2.0f + settings.chunkSize / 2.0f,
            settings.center.y,
            settings.center.z - totalWorldSizeZ / 2.0f + settings.chunkSize / 2.0f
        );

        transform.position = startPosition;

        // chunkCountに基づいてチャンクを生成
        for (int x = 0; x < settings.chunkCount.x; x++)
        {
            for (int z = 0; z < settings.chunkCount.y; z++)
            {
                Vector3Int chunkPos = new Vector3Int(x, 0, z);
                bool[,,] pattern = GenerateTopDownPattern();
                CreateChunk(chunkPos, pattern);
            }
        }
    }

    /// <summary>
    /// カスタムタイプの地形生成
    /// </summary>
    private void GenerateCustomTerrain()
    {
        // 将来の拡張用
        Debug.LogWarning("Custom terrain generation is not implemented yet.");
    }

    /// <summary>
    /// サイドスクローラー用のパターンを生成
    /// </summary>
    private bool[,,] GenerateSideScrollerPattern()
    {
        bool[,,] pattern = new bool[settings.voxelSize, settings.voxelSize, settings.voxelSize];
        float cubeSize = settings.chunkSize / settings.voxelSize;

        for (int lx = 0; lx < settings.voxelSize; lx++)
        {
            for (int ly = 0; ly < settings.voxelSize; ly++)
            {
                for (int lz = 0; lz < settings.voxelSize; lz++)
                {
                    // Z軸の絶対値が0.5以下のボクセルのみ生成
                    float zPos = (lz - (settings.voxelSize - 1) / 2.0f) * cubeSize;
                    pattern[lx, ly, lz] = Mathf.Abs(zPos) <= 0.5f;
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// トップダウン用のパターンを生成
    /// </summary>
    private bool[,,] GenerateTopDownPattern()
    {
        bool[,,] pattern = new bool[settings.voxelSize, settings.voxelSize, settings.voxelSize];
        float cubeSize = settings.chunkSize / settings.voxelSize;

        for (int lx = 0; lx < settings.voxelSize; lx++)
        {
            for (int ly = 0; ly < settings.voxelSize; ly++)
            {
                for (int lz = 0; lz < settings.voxelSize; lz++)
                {
                    // Y軸の絶対値が0.5以下のボクセルのみ生成
                    float yPos = (ly - (settings.voxelSize - 1) / 2.0f) * cubeSize;
                    pattern[lx, ly, lz] = Mathf.Abs(yPos) <= 0.5f;
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// チャンクを作成
    /// </summary>
    private void CreateChunk(Vector3Int chunkPos, bool[,,] pattern)
    {
        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}_{chunkPos.z}");
        chunkObj.transform.parent = transform;
        chunkObj.transform.localPosition = (Vector3)chunkPos * settings.chunkSize;

        // チャンクの表示サイズがchunkSizeになるようにスケールを調整
        float scale = settings.chunkSize / settings.voxelSize;
        chunkObj.transform.localScale = new Vector3(scale, scale, scale);

        VoxelChunk chunk = chunkObj.AddComponent<VoxelChunk>();
        var renderer = chunkObj.GetComponent<MeshRenderer>();

        // Material設定（URP Transparent）
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_AlphaClip", 1); // Alpha Clipping
        mat.mainTexture = settings.texture1;
        renderer.material = mat;

        // VoxelChunkを初期化
        chunk.Initialize(
            pattern, 
            settings.voxelSize, 
            settings.chunkSize, 
            settings.voxelHp, 
            settings.droppedItemPrefab, 
            settings.disableRotation, 
            settings.autoScale, 
            settings.scaleMultiplier, 
            settings.texture1, 
            settings.texture2
        );

        if (showDebugInfo)
        {
            Debug.Log($"Created chunk at position {chunkPos} with {CountActiveVoxels(pattern)} active voxels");
        }
    }

    /// <summary>
    /// アクティブなボクセル数をカウント（デバッグ用）
    /// </summary>
    private int CountActiveVoxels(bool[,,] pattern)
    {
        int count = 0;
        for (int x = 0; x < pattern.GetLength(0); x++)
        {
            for (int y = 0; y < pattern.GetLength(1); y++)
            {
                for (int z = 0; z < pattern.GetLength(2); z++)
                {
                    if (pattern[x, y, z]) count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// 既存の全チャンクを削除
    /// </summary>
    public void ClearTerrain()
    {
        // 子オブジェクトを全て削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }

    /// <summary>
    /// 地形を再生成
    /// </summary>
    [ContextMenu("Regenerate Terrain")]
    public void RegenerateTerrain()
    {
        ClearTerrain();
        GenerateTerrain();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // エディタでの値変更時に設定を検証
        settings.chunkCount.x = Mathf.Max(1, settings.chunkCount.x);
        settings.chunkCount.y = Mathf.Max(1, settings.chunkCount.y);
        settings.chunkSize = Mathf.Max(0.1f, settings.chunkSize);
        settings.voxelSize = Mathf.Max(1, settings.voxelSize);
        settings.voxelHp = Mathf.Max(1, settings.voxelHp);
        settings.scaleMultiplier = Mathf.Max(0.1f, settings.scaleMultiplier);
    }
#endif
}
