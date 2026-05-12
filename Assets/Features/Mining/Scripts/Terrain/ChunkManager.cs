using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

public class ChunkManager : MonoBehaviour
{
    [Header("Dynamic Generation")]
    [SerializeField] private Transform playerTransform; // プレイヤーのTransform
    [SerializeField] private int renderDistanceInChunks = 5; // チャンクの描画距離
    private float chunkUpdateInterval = 0.3f; // チャンクの更新間隔
    private float timeSinceLastChunkUpdate = 0f;
    private Vector3Int currentPlayerChunk;
    private Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    private readonly List<Vector3Int> _blockGenerationList = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> _processedBlocks = new HashSet<Vector3Int>(); // 生成済み or キュー済みのブロック

    private TerrainManager terrainManager;
    private CancellationTokenSource cancellationTokenSource;

    public void Initialize(TerrainManager manager)
    {
        terrainManager = manager;
        playerTransform = manager.playerTransform; // TerrainManagerから取得
    }

    void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        ProcessBlockGenerationQueue(cancellationTokenSource.Token).Forget();
        GenerateTerrain();
    }

    void OnDestroy()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }

    void Update()
    {
        if (playerTransform == null) return;

        timeSinceLastChunkUpdate += Time.deltaTime;
        if (timeSinceLastChunkUpdate >= chunkUpdateInterval)
        {
            UpdateChunks();
            timeSinceLastChunkUpdate = 0f;
        }
    }

    public void GenerateTerrain()
    {
        terrainManager.BlockGenerator.ResetRandom(terrainManager.Settings.seed);
        if (terrainManager.showDebugInfo)
        {
            Debug.Log($"ChunkManager: Generating initial terrain with type {terrainManager.Settings.generationType}");
        }
        
        ClearChunks();
        
        Vector3Int playerChunkPos = GetChunkPositionFromWorld(playerTransform.position);
        currentPlayerChunk = playerChunkPos;
        Vector3 playerWorldPos = playerTransform.position;

        List<Vector3Int> sortedChunks = new List<Vector3Int>();
        
        for (int x = -renderDistanceInChunks; x <= renderDistanceInChunks; x++)
        {
            for (int y = -renderDistanceInChunks; y <= renderDistanceInChunks; y++)
            {
                Vector3Int chunkPos = new Vector3Int(currentPlayerChunk.x + x, currentPlayerChunk.y + y, 0);
                sortedChunks.Add(chunkPos);
            }
        }
        
        sortedChunks.Sort((a, b) => 
        {
            Vector3 aCenterWorld = GetChunkCenterWorldPosition(a);
            Vector3 bCenterWorld = GetChunkCenterWorldPosition(b);
            float distA = Vector3.Distance(playerWorldPos, aCenterWorld);
            float distB = Vector3.Distance(playerWorldPos, bCenterWorld);
            return distA.CompareTo(distB);
        });

        foreach (var chunkPos in sortedChunks)
        {
            if (!activeChunks.ContainsKey(chunkPos))
            {
                GetOrCreateChunkFromPosition(chunkPos);
                List<Vector3Int> chunkBlocks = GetBlockPositionsInChunk(chunkPos);
                
                chunkBlocks.Sort((a, b) => 
                {
                    Vector3 aWorldPos = GetBlockWorldPosition(a);
                    Vector3 bWorldPos = GetBlockWorldPosition(b);
                    float distA = Vector3.Distance(playerWorldPos, aWorldPos);
                    float distB = Vector3.Distance(playerWorldPos, bWorldPos);
                    return distA.CompareTo(distB);
                });
                
                foreach (var blockPos in chunkBlocks)
                {
                    if (_processedBlocks.Add(blockPos))
                    {
                        _blockGenerationList.Add(blockPos);
                    }
                }
            }
        }
    }

    private void UpdateChunks()
    {
        Vector3Int newPlayerChunkPos = GetChunkPositionFromWorld(playerTransform.position);
        if (newPlayerChunkPos == currentPlayerChunk && activeChunks.Count > 0) return;
        
        currentPlayerChunk = newPlayerChunkPos;
        Vector3 playerWorldPos = playerTransform.position;

        List<Vector3Int> chunksToGenerate = new List<Vector3Int>();
        for (int x = -renderDistanceInChunks; x <= renderDistanceInChunks; x++)
        {
            for (int y = -renderDistanceInChunks; y <= renderDistanceInChunks; y++)
            {
                Vector3Int chunkPos = new Vector3Int(currentPlayerChunk.x + x, currentPlayerChunk.y + y, 0);
                if (activeChunks.ContainsKey(chunkPos)) continue;
                chunksToGenerate.Add(chunkPos);
            }
        }
        
        chunksToGenerate.Sort((a, b) => 
        {
            Vector3 aCenterWorld = GetChunkCenterWorldPosition(a);
            Vector3 bCenterWorld = GetChunkCenterWorldPosition(b);
            float distA = Vector3.Distance(playerWorldPos, aCenterWorld);
            float distB = Vector3.Distance(playerWorldPos, bCenterWorld);
            return distA.CompareTo(distB);
        });
        
        foreach (var chunkPos in chunksToGenerate)
        {
            GetOrCreateChunkFromPosition(chunkPos);
            List<Vector3Int> blocksInChunk = GetBlockPositionsInChunk(chunkPos);
            
            blocksInChunk.Sort((a, b) => 
            {
                Vector3 aWorldPos = GetBlockWorldPosition(a);
                Vector3 bWorldPos = GetBlockWorldPosition(b);
                float distA = Vector3.Distance(playerWorldPos, aWorldPos);
                float distB = Vector3.Distance(playerWorldPos, bWorldPos);
                return distA.CompareTo(distB);
            });
            
            foreach (var blockPos in blocksInChunk)
            {
                if (_processedBlocks.Add(blockPos))
                {
                    _blockGenerationList.Add(blockPos);
                }
            }
        }
    }

    private async UniTask ProcessBlockGenerationQueue(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int blocksToProcess = Mathf.Min(_blockGenerationList.Count, terrainManager.Settings.blocksPerFrame);
            for (int i = 0; i < blocksToProcess; i++)
            {
                if (_blockGenerationList.Count > 0)
                {
                    Vector3Int blockPos = _blockGenerationList[0];
                    _blockGenerationList.RemoveAt(0);
                    GenerateSingleBlock(blockPos);
                }
            }
            await UniTask.Yield(cancellationToken);
        }
    }

    private void GenerateSingleBlock(Vector3Int blockPos)
    {
        Chunk chunk = GetOrCreateChunk(blockPos);
        Vector3 worldPos = GetBlockWorldPosition(blockPos);

        // 論理座標に基づいて、生成すべきブロックのデータを取得
        BlockData blockTypeData = terrainManager.BlockGenerator.GetBlockDataForPosition(blockPos);

        // 生成すべきブロックがない場合は、ここで処理を終了
        if (blockTypeData == null)
        {
            return;
        }

        var generationData = new BlockGenerator.BlockGenerationData(
            terrainManager.Settings.generationType,
            terrainManager.Settings.voxelsPerBlock,
            terrainManager.Settings.blockSize,
            blockPos
        );
        bool[,,] pattern = terrainManager.BlockGenerator.GenerateBlockPattern(generationData);

        var newBlockInstance = terrainManager.BlockManager.CreateBlock(blockPos, worldPos, terrainManager.Settings.blockSize, terrainManager.Settings.voxelsPerBlock, chunk.transform);

        terrainManager.VoxelManager.RegisterVoxelsFromPattern(pattern, blockPos, worldPos, blockTypeData, terrainManager.Settings.blockSize, terrainManager.Settings.voxelsPerBlock);

        if (newBlockInstance != null && newBlockInstance.block != null)
        {
            newBlockInstance.block.GenerateMesh();
        }
    }

    public Transform GetOrCreateChunkTransform(Vector3Int blockPos)
    {
        Chunk chunk = GetOrCreateChunk(blockPos);
        return chunk != null ? chunk.transform : null;
    }

    private Chunk GetOrCreateChunk(Vector3Int blockPos)
    {
        Vector3Int chunkPos = GetChunkPositionFromBlock(blockPos);
        return GetOrCreateChunkFromPosition(chunkPos);
    }

    private Chunk GetOrCreateChunkFromPosition(Vector3Int chunkPos)
    {
        if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            return chunk;
        }

        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
        chunkObj.transform.parent = transform;
        Chunk newChunk = chunkObj.AddComponent<Chunk>();
        newChunk.Initialize(chunkPos);
        activeChunks.Add(chunkPos, newChunk);

        // このチャンクに対応するドロップアイテムを遅延ロード
        LoadItemsWithDelay(chunkPos).Forget();

        return newChunk;
    }

    public void ClearChunks()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
        ProcessBlockGenerationQueue(cancellationTokenSource.Token).Forget();
        
        foreach (var chunk in activeChunks.Values)
        {
            if (chunk != null) Destroy(chunk.gameObject);
        }
        
        activeChunks.Clear();
        _blockGenerationList.Clear();
        _processedBlocks.Clear();
    }

    public Vector3Int GetChunkPositionFromWorld(Vector3 worldPosition)
    {
        var settings = terrainManager.Settings;
        
        // ワールド座標を論理ブロック座標に変換
        int blockX = WorldToBlockCoordinate(worldPosition.x, settings.blockSize);
        int blockY = WorldToBlockCoordinate(worldPosition.y, settings.blockSize);

        // 論理ブロック座標をチャンク座標に変換
        return GetChunkPositionFromBlock(new Vector3Int(blockX, blockY, 0));
    }

    public Vector3Int GetChunkPositionFromBlock(Vector3Int blockPosition)
    {
        var settings = terrainManager.Settings;
        int chunkX = Mathf.FloorToInt((float)(blockPosition.x + GetHorizontalChunkOriginOffset()) / settings.blocksPerChunk.x);
        int chunkY = Mathf.FloorToInt((float)blockPosition.y / settings.blocksPerChunk.y);

        return new Vector3Int(chunkX, chunkY, blockPosition.z);
    }

    public bool IsBlockGenerationSkipped(Vector3Int blockPosition)
    {
        return terrainManager.TerrainDataManager != null &&
               terrainManager.TerrainDataManager.IsBlockGenerationExcluded(blockPosition);
    }

    private List<Vector3Int> GetBlockPositionsInChunk(Vector3Int chunkPos)
    {
        var settings = terrainManager.Settings;
        Vector3Int startBlock = GetChunkStartBlockPosition(chunkPos);
        List<Vector3Int> blockPositions = new List<Vector3Int>(settings.blocksPerChunk.x * settings.blocksPerChunk.y);

        for (int bx = 0; bx < settings.blocksPerChunk.x; bx++)
        {
            for (int by = 0; by < settings.blocksPerChunk.y; by++)
            {
                Vector3Int blockPosition = new Vector3Int(startBlock.x + bx, startBlock.y + by, chunkPos.z);
                if (!IsBlockGenerationSkipped(blockPosition))
                {
                    blockPositions.Add(blockPosition);
                }
            }
        }

        return blockPositions;
    }

    private Vector3Int GetChunkStartBlockPosition(Vector3Int chunkPos)
    {
        var settings = terrainManager.Settings;
        return new Vector3Int(
            chunkPos.x * settings.blocksPerChunk.x - GetHorizontalChunkOriginOffset(),
            chunkPos.y * settings.blocksPerChunk.y,
            chunkPos.z
        );
    }

    private int GetHorizontalChunkOriginOffset()
    {
        return terrainManager.Settings.blocksPerChunk.x / 2;
    }

    private int WorldToBlockCoordinate(float worldAxis, float blockSize)
    {
        return Mathf.FloorToInt(worldAxis / blockSize + 0.5f);
    }

    private Vector3 GetChunkCenterWorldPosition(Vector3Int chunkPos)
    {
        var settings = terrainManager.Settings;
        
        // チャンクの開始ブロック座標を計算
        Vector3Int startBlock = GetChunkStartBlockPosition(chunkPos);
        float startBlockX = startBlock.x;
        float startBlockY = startBlock.y;

        // チャンクの中心のブロック座標を計算
        float centerBlockX = startBlockX + (settings.blocksPerChunk.x - 1) / 2f;
        float centerBlockY = startBlockY + (settings.blocksPerChunk.y - 1) / 2f;

        // ワールド座標に変換
        float worldX = centerBlockX * settings.blockSize;
        float worldY = centerBlockY * settings.blockSize;

        return new Vector3(worldX, worldY, settings.center.z);
    }

    private Vector3 GetBlockWorldPosition(Vector3Int blockPos)
    {
        var settings = terrainManager.Settings;
        
        // 論理座標にブロックサイズを掛けてワールド座標の基点を計算
        // （ブロックの中心ではなく、左下の角を原点とする）
        float worldX = blockPos.x * settings.blockSize;
        float worldY = blockPos.y * settings.blockSize;

        return new Vector3(worldX, worldY, settings.center.z);
    }

    private async UniTask LoadItemsWithDelay(Vector3Int chunkPos)
    {
        if (terrainManager == null) return;
        
        // 指定された時間だけ待機
        await UniTask.Delay(System.TimeSpan.FromSeconds(terrainManager.Settings.itemLoadDelay), cancellationToken: cancellationTokenSource.Token);

        if (cancellationTokenSource.IsCancellationRequested) return;

        // ドロップアイテムをロード
        if (DroppedItemManager.Instance != null)
        {
            DroppedItemManager.Instance.LoadItemsInChunk(chunkPos);
        }
    }
}
