using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ブロック管理クラス
/// 地形のブロックレベルでの管理を担当
/// </summary>
public class BlockManager : MonoBehaviour, ISaveable
{
    [Header("Block Configuration")]
    [SerializeField] private List<BlockInstanceData> blocks = new List<BlockInstanceData>();
    [SerializeField] private bool showBlockDebugInfo = false;
    
    /// <summary>
    /// ブロックのインスタンスデータ構造
    /// </summary>
    [System.Serializable]
    public class BlockInstanceData
    {
        public Vector3Int position;          // ブロックの論理座標
        public Vector3 worldPosition;       // ワールド座標
        public Block block;                 // 実際のBlockへの参照
        public Rigidbody rigidbody;         // Rigidbodyへの参照
        public bool isActive = true;        // ブロックがアクティブかどうか
        public int voxelCount;              // このブロック内のボクセル数
        public ResourceType resourceType;   // ブロックのリソースタイプ

        public BlockInstanceData(Vector3Int pos, Vector3 worldPos, ResourceType type)
        {
            position = pos;
            worldPosition = worldPos;
            resourceType = type;
        }
    }
    
    /// <summary>
    /// TerrainManagerからの参照
    /// </summary>
    private TerrainManager terrainManager;
    
    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Initialized with TerrainManager");
        }
    }
    
    /// <summary>
    /// ブロックを作成
    /// </summary>
    public BlockInstanceData CreateBlock(Vector3Int blockPos, Vector3 worldPos, bool[,,] pattern, ResourceType resourceType, global::BlockData data, float blockSize, int voxelSize, Transform parent)
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Creating block at {blockPos} with type {resourceType}");
        }
        
        // ブロックデータを作成
        BlockInstanceData blockInstance = new BlockInstanceData(blockPos, worldPos, resourceType);
        
        // GameObjectとBlockを作成
        GameObject blockObj = new GameObject($"Block_{resourceType}_{blockPos.x}_{blockPos.y}");
        blockObj.transform.parent = parent;
        blockObj.transform.position = worldPos;
        
        // スケール調整
        float scale = blockSize / voxelSize;
        blockObj.transform.localScale = new Vector3(scale, scale, scale);
        
        // Blockコンポーネントを追加
        Block block = blockObj.AddComponent<Block>();
        blockInstance.block = block;

        // Rigidbodyを追加して初期設定
        Rigidbody rb = blockObj.AddComponent<Rigidbody>();
        rb.isKinematic = true; // デフォルトはスリープ状態
        blockInstance.rigidbody = rb;
        
        // マテリアル設定
        CreateBlockMaterial(blockObj, data);
        
        // Blockを初期化
        block.Initialize(
            terrainManager.VoxelManager,
            blockPos,
            pattern,
            voxelSize,
            blockSize,
            data
        );
        
        // ボクセル数を計算
        blockInstance.voxelCount = CalculateVoxelCount(pattern);
        
        // リストに追加
        blocks.Add(blockInstance);
        
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Created block with {blockInstance.voxelCount} voxels");
        }
        
        return blockInstance;
    }
    
    /// <summary>
    /// ブロック用マテリアルを作成
    /// </summary>
    private void CreateBlockMaterial(GameObject blockObj, global::BlockData data)
    {
        var renderer = blockObj.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = blockObj.AddComponent<MeshRenderer>();
        }
        
        // URP Transparentマテリアルを作成
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_AlphaClip", 1); // Alpha Clipping
        
        // BlockDataにテクスチャが設定されていれば使用
        if (data.textures != null && data.textures.Count > 0)
        {
            mat.mainTexture = data.textures[0]; // 最初のテクスチャをデフォルトとして使用
        }
        
        renderer.material = mat;
    }
    
    /// <summary>
    /// パターンからボクセル数を計算
    /// </summary>
    private int CalculateVoxelCount(bool[,,] pattern)
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
    /// 指定座標のブロックを取得
    /// </summary>
    public BlockInstanceData GetBlockAt(Vector3Int position)
    {
        foreach (var block in blocks)
        {
            if (block.position == position)
            {
                return block;
            }
        }
        return null;
    }
    
    /// <summary>
    /// すべてのブロックを取得
    /// </summary>
    public List<BlockInstanceData> GetAllBlocks()
    {
        return new List<BlockInstanceData>(blocks);
    }
    
    /// <summary>
    /// アクティブなブロックの数を取得
    /// </summary>
    public int GetActiveBlockCount()
    {
        int count = 0;
        foreach (var block in blocks)
        {
            if (block.isActive) count++;
        }
        return count;
    }
    
    /// <summary>
    /// ブロックのアクティブ状態を設定
    /// </summary>
    public void SetBlockActive(Vector3Int position, bool active)
    {
        BlockInstanceData block = GetBlockAt(position);
        if (block != null && block.block != null)
        {
            block.isActive = active;
            block.block.gameObject.SetActive(active);
            
            if (showBlockDebugInfo)
            {
                Debug.Log($"BlockManager: Set block {position} active: {active}");
            }
        }
    }
    
    /// <summary>
    /// すべてのブロックを削除
    /// </summary>
    public void ClearAllBlocks()
    {
        if (showBlockDebugInfo)
        {
            Debug.Log($"BlockManager: Clearing {blocks.Count} blocks");
        }
        
        foreach (var block in blocks)
        {
            if (block.block != null)
            {
                DestroyImmediate(block.block.gameObject);
            }
        }
        
        blocks.Clear();
    }
    
    /// <summary>
    /// デバッグ情報を取得
    /// </summary>
    /// <summary>
    /// プレイヤーとの距離に応じてブロックのisKinematic状態を更新
    /// </summary>
    public void UpdateBlocksKinematicState(Vector3 playerPosition, float activationDistance)
    {
        float sqrActivationDistance = activationDistance * activationDistance;
        foreach (var blockData in blocks)
        {
            if (blockData.block == null || blockData.rigidbody == null) continue;

            float sqrDistance = (blockData.worldPosition - playerPosition).sqrMagnitude;
            bool shouldBeAwake = sqrDistance <= sqrActivationDistance;
            
            // 状態が異なる場合のみ更新
            if (blockData.rigidbody.isKinematic == shouldBeAwake)
            {
                blockData.rigidbody.isKinematic = !shouldBeAwake;
            }
        }
    }
    
    /// <summary>
    /// デバッグ情報を取得
    /// </summary>
    public string GetDebugInfo()
    {
        int totalVoxels = 0;
        foreach (var block in blocks)
        {
            totalVoxels += block.voxelCount;
        }
        
        return $"BlockManager - Total: {blocks.Count}, Active: {GetActiveBlockCount()}, Voxels: {totalVoxels}";
    }
    
    void OnDestroy()
    {
        ClearAllBlocks();
    }

    #region SaveSystem
    public string SaveFileName => "world";

    public object CaptureState()
    {
        var saveData = new WorldSaveData();
        foreach (var blockInstance in blocks)
        {
            if (blockInstance.block == null) continue;

            var pattern3D = blockInstance.block.GetVoxelPattern();
            var voxelSize = blockInstance.block.ChunkSize;
            
            var blockData = new BlockSaveData
            {
                position = new SerializableVector3Int(blockInstance.position),
                resourceTypeId = (int)blockInstance.resourceType,
                voxelSize = voxelSize,
                voxelPattern = FlattenVoxelPattern(pattern3D, voxelSize)
            };
            saveData.modifiedBlocks.Add(blockData);
        }
        return saveData;
    }

    public void RestoreState(object state)
    {
        var saveData = state as WorldSaveData;
        if (saveData == null) return;

        // 既存のワールドをクリア
        terrainManager.ClearTerrain();

        // チャンクを保持するための仮の親オブジェクト
        var chunkParents = new Dictionary<Vector3Int, Transform>();

        foreach (var blockData in saveData.modifiedBlocks)
        {
            Vector3Int blockPos = blockData.position.ToVector3Int();
            ResourceType resourceType = (ResourceType)blockData.resourceTypeId;
            var blockTypeData = terrainManager.BlockDataManager.GetBlockData(resourceType);
            if (blockTypeData == null)
            {
                Debug.LogWarning($"Could not find BlockData for ResourceType ID {blockData.resourceTypeId}. Skipping block at {blockPos}");
                continue;
            }

            // チャンクの親を取得または作成
            Vector3Int chunkPos = new Vector3Int(
                Mathf.FloorToInt((float)blockPos.x / terrainManager.Settings.chunkSizeInBlocks.x),
                Mathf.FloorToInt((float)blockPos.y / terrainManager.Settings.chunkSizeInBlocks.y),
                0
            );
            if (!chunkParents.TryGetValue(chunkPos, out var parent))
            {
                GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
                chunkObj.transform.parent = terrainManager.transform;
                parent = chunkObj.transform;
                chunkParents.Add(chunkPos, parent);
            }

            // ワールド座標を再計算する必要がある
            // この計算はTerrainManagerが詳しいので、将来的にはそちらにメソッドを移譲すべき
            float t = terrainManager.Settings.blockSize;
            float u_x = terrainManager.Settings.chunkSizeInBlocks.x;
            float u_y = terrainManager.Settings.chunkSizeInBlocks.y;
            int bx = blockPos.x - chunkPos.x * (int)u_x;
            int by = blockPos.y - chunkPos.y * (int)u_y;
            float chunkCenterX = chunkPos.x * u_x * t;
            float relativeBlockX = (bx - (u_x - 1) / 2f) * t;
            float worldX = chunkCenterX + relativeBlockX;
            float chunkCenterY = t * u_y * (2 * chunkPos.y - 1) / 2f;
            float relativeBlockY = (by - (u_y - 1) / 2f) * t;
            float worldY = chunkCenterY + relativeBlockY;
            Vector3 worldPos = new Vector3(worldX, worldY, terrainManager.Settings.center.z);

            // 3Dパターンに復元
            var pattern3D = UnflattenVoxelPattern(blockData.voxelPattern, blockData.voxelSize);

            // ブロックを再生成
            var newBlockInstance = CreateBlock(blockPos, worldPos, pattern3D, resourceType, blockTypeData, terrainManager.Settings.blockSize, blockData.voxelSize, parent);
            
            // VoxelManagerにボクセルデータを再登録
            terrainManager.VoxelManager.RegisterVoxelsFromPattern(pattern3D, blockPos, worldPos, blockTypeData, terrainManager.Settings.blockSize, blockData.voxelSize);

            // メッシュを生成
            if (newBlockInstance != null && newBlockInstance.block != null)
            {
                newBlockInstance.block.GenerateMesh();
            }
        }
    }

    private bool[] FlattenVoxelPattern(bool[,,] pattern3D, int size)
    {
        bool[] pattern1D = new bool[size * size * size];
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    pattern1D[x * size * size + y * size + z] = pattern3D[x, y, z];
                }
            }
        }
        return pattern1D;
    }

    private bool[,,] UnflattenVoxelPattern(bool[] pattern1D, int size)
    {
        if (pattern1D == null || pattern1D.Length != size * size * size)
        {
            // データが不正な場合は、すべてがtrueのパターンを返す（完全に埋まったブロック）
            var defaultPattern = new bool[size, size, size];
            for(int i=0; i<size; i++) for(int j=0; j<size; j++) for(int k=0; k<size; k++) defaultPattern[i,j,k] = true;
            return defaultPattern;
        }

        bool[,,] pattern3D = new bool[size, size, size];
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    pattern3D[x, y, z] = pattern1D[x * size * size + y * size + z];
                }
            }
        }
        return pattern3D;
    }
    #endregion
}
