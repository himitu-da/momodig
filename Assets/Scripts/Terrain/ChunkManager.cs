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
                if (chunkPos.y > 0) continue;
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
                List<Vector3Int> chunkBlocks = new List<Vector3Int>();
                for (int bx = 0; bx < terrainManager.Settings.blocksPerChunk.x; bx++)
                {
                    for (int by = 0; by < terrainManager.Settings.blocksPerChunk.y; by++)
                    {
                        Vector3Int blockPos = new Vector3Int(
                            chunkPos.x * terrainManager.Settings.blocksPerChunk.x + bx,
                            chunkPos.y * terrainManager.Settings.blocksPerChunk.y + by,
                            0
                        );
                        chunkBlocks.Add(blockPos);
                    }
                }
                
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
                if (chunkPos.y > 0) continue;
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
            List<Vector3Int> blocksInChunk = new List<Vector3Int>();
            for (int bx = 0; bx < terrainManager.Settings.blocksPerChunk.x; bx++)
            {
                for (int by = 0; by < terrainManager.Settings.blocksPerChunk.y; by++)
                {
                    Vector3Int blockPos = new Vector3Int(
                        chunkPos.x * terrainManager.Settings.blocksPerChunk.x + bx,
                        chunkPos.y * terrainManager.Settings.blocksPerChunk.y + by,
                        0
                    );
                    blocksInChunk.Add(blockPos);
                }
            }
            
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

        var newBlockInstance = terrainManager.BlockManager.CreateBlock(blockPos, worldPos, pattern, blockTypeData, terrainManager.Settings.blockSize, terrainManager.Settings.voxelsPerBlock, chunk.transform);

        terrainManager.VoxelManager.RegisterVoxelsFromPattern(pattern, blockPos, worldPos, blockTypeData, terrainManager.Settings.blockSize, terrainManager.Settings.voxelsPerBlock);

        if (newBlockInstance != null && newBlockInstance.block != null)
        {
            newBlockInstance.block.GenerateMesh();
        }
    }

    private Chunk GetOrCreateChunk(Vector3Int blockPos)
    {
        Vector3Int chunkPos = new Vector3Int(
            Mathf.FloorToInt((float)blockPos.x / terrainManager.Settings.blocksPerChunk.x),
            Mathf.FloorToInt((float)blockPos.y / terrainManager.Settings.blocksPerChunk.y),
            0
        );

        if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            return chunk;
        }

        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
        chunkObj.transform.parent = transform;
        Chunk newChunk = chunkObj.AddComponent<Chunk>();
        newChunk.Initialize(chunkPos);
        activeChunks.Add(chunkPos, newChunk);
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

    private Vector3Int GetChunkPositionFromWorld(Vector3 worldPosition)
    {
        var settings = terrainManager.Settings;
        
        // ワールド座標を論理ブロック座標に変換
        int blockX = Mathf.RoundToInt(worldPosition.x / settings.blockSize);
        int blockY = Mathf.RoundToInt(worldPosition.y / settings.blockSize);

        // 論理ブロック座標をチャンク座標に変換
        int chunkX = Mathf.FloorToInt((float)blockX / settings.blocksPerChunk.x);
        int chunkY = Mathf.FloorToInt((float)blockY / settings.blocksPerChunk.y);

        return new Vector3Int(chunkX, chunkY, 0);
    }

    private Vector3 GetChunkCenterWorldPosition(Vector3Int chunkPos)
    {
        var settings = terrainManager.Settings;
        
        // チャンクの開始ブロック座標を計算
        float startBlockX = chunkPos.x * settings.blocksPerChunk.x;
        float startBlockY = chunkPos.y * settings.blocksPerChunk.y;

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
}
