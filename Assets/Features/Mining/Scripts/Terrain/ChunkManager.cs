using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;
using Cysharp.Threading.Tasks;
using System.Threading;

public class ChunkManager : MonoBehaviour
{
    private static readonly ProfilerMarker RegisterChunkGenerationMarker =
        new ProfilerMarker("ChunkManager.RegisterChunkGeneration");
    private static readonly ProfilerMarker MarkBlockGenerationProcessedMarker =
        new ProfilerMarker("ChunkManager.MarkBlockGenerationProcessed");

    [Header("Dynamic Generation")]
    [SerializeField] private Transform playerTransform; // プレイヤーのTransform
    [SerializeField] private int renderDistanceInChunks = 5; // チャンクの描画距離

    [Header("Persistence Restoration")]
    [SerializeField] private MiningSceneRestoreCoordinator restoreCoordinator;
    [SerializeField, Min(0)] private int initialRestoreRadiusInChunks = 1;

    private float chunkUpdateInterval = 0.3f; // チャンクの更新間隔
    private float timeSinceLastChunkUpdate = 0f;
    private Vector3Int currentPlayerChunk;
    private Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    private readonly List<Vector3Int> _blockGenerationList = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> _processedBlocks = new HashSet<Vector3Int>(); // 生成済み or キュー済みのブロック
    private readonly Dictionary<Vector3Int, int> pendingBlockCountsByChunk = new Dictionary<Vector3Int, int>();
    private readonly HashSet<Vector3Int> restoredChunks = new HashSet<Vector3Int>();

    private TerrainManager terrainManager;
    private CancellationTokenSource cancellationTokenSource;
    private bool blockGenerationSettingsErrorLogged;
    private bool restoreCoordinatorMissingErrorLogged;

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
        if (!ValidateRestoreCoordinator())
        {
            return;
        }

        terrainManager.BlockGenerator.ResetRandom(terrainManager.Settings.seed);
        if (terrainManager.showDebugInfo)
        {
            Debug.Log($"ChunkManager: Generating initial terrain with type {terrainManager.Settings.generationType}");
        }
        
        ClearChunks();
        
        Vector3Int playerChunkPos = GetChunkPositionFromWorld(playerTransform.position);
        currentPlayerChunk = playerChunkPos;
        Vector3 playerWorldPos = playerTransform.position;

        List<Vector3Int> sortedChunks = BuildChunkPositionsAround(currentPlayerChunk, renderDistanceInChunks);
        
        sortedChunks.Sort((a, b) => 
        {
            Vector3 aCenterWorld = GetChunkCenterWorldPosition(a);
            Vector3 bCenterWorld = GetChunkCenterWorldPosition(b);
            float distA = Vector3.Distance(playerWorldPos, aCenterWorld);
            float distB = Vector3.Distance(playerWorldPos, bCenterWorld);
            return distA.CompareTo(distB);
        });

        List<Vector3Int> initialRestoreChunks = BuildChunkPositionsAround(playerChunkPos, initialRestoreRadiusInChunks);
        restoreCoordinator.BeginInitialChunkRestore(playerChunkPos, initialRestoreChunks);

        foreach (var chunkPos in sortedChunks)
        {
            if (!activeChunks.ContainsKey(chunkPos))
            {
                GetOrCreateChunkFromPosition(chunkPos);
                List<Vector3Int> chunkBlocks = GetBlockPositionsInChunk(chunkPos);
                RegisterChunkGeneration(chunkPos, chunkBlocks.Count);
                
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
        if (!ValidateRestoreCoordinator())
        {
            return;
        }

        Vector3Int newPlayerChunkPos = GetChunkPositionFromWorld(playerTransform.position);
        if (newPlayerChunkPos == currentPlayerChunk && activeChunks.Count > 0) return;
        
        currentPlayerChunk = newPlayerChunkPos;
        Vector3 playerWorldPos = playerTransform.position;

        List<Vector3Int> chunksToGenerate = BuildChunkPositionsAround(currentPlayerChunk, renderDistanceInChunks);
        for (int i = chunksToGenerate.Count - 1; i >= 0; i--)
        {
            if (activeChunks.ContainsKey(chunksToGenerate[i]))
            {
                chunksToGenerate.RemoveAt(i);
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
            RegisterChunkGeneration(chunkPos, blocksInChunk.Count);
            
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
            if (_blockGenerationList.Count > 0 && ValidateBlockGenerationSettings())
            {
                TerrainSettings settings = terrainManager.Settings;
                int blocksToProcess = Mathf.Min(_blockGenerationList.Count, settings.blocksPerFrame);
                int processedCount = 0;
                double startedAt = Time.realtimeSinceStartupAsDouble;
                double budgetSeconds = settings.blockGenerationBudgetMilliseconds / 1000.0;

                while (_blockGenerationList.Count > 0 && processedCount < blocksToProcess)
                {
                    if (processedCount > 0 && Time.realtimeSinceStartupAsDouble - startedAt >= budgetSeconds)
                    {
                        break;
                    }

                    Vector3Int blockPos = _blockGenerationList[0];
                    _blockGenerationList.RemoveAt(0);
                    GenerateSingleBlock(blockPos);
                    MarkBlockGenerationProcessed(blockPos);
                    processedCount++;
                }
            }
            await UniTask.Yield(cancellationToken);
        }
    }

    private bool ValidateBlockGenerationSettings()
    {
        TerrainSettings settings = terrainManager.Settings;
        if (settings.blocksPerFrame <= 0 ||
            settings.blockGenerationBudgetMilliseconds <= 0f)
        {
            if (!blockGenerationSettingsErrorLogged)
            {
                blockGenerationSettingsErrorLogged = true;
                Debug.LogError(
                    $"ChunkManager: invalid block generation settings. blocksPerFrame={settings.blocksPerFrame}, blockGenerationBudgetMilliseconds={settings.blockGenerationBudgetMilliseconds}",
                    this);
            }

            return false;
        }

        blockGenerationSettingsErrorLogged = false;
        return true;
    }

    private void GenerateSingleBlock(Vector3Int blockPos)
    {
        Chunk chunk = GetOrCreateChunk(blockPos);
        if (chunk == null)
        {
            return;
        }

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
        if (!ValidateRestoreCoordinator())
        {
            return null;
        }

        if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
        {
            return chunk;
        }

        GameObject chunkObj = new GameObject($"Chunk_{chunkPos.x}_{chunkPos.y}");
        chunkObj.transform.parent = transform;
        Chunk newChunk = chunkObj.AddComponent<Chunk>();
        newChunk.Initialize(chunkPos);
        activeChunks.Add(chunkPos, newChunk);
        restoreCoordinator.NotifyChunkGenerated(chunkPos);

        return newChunk;
    }

    public void ClearChunks()
    {
        if (!ValidateRestoreCoordinator())
        {
            return;
        }

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
        pendingBlockCountsByChunk.Clear();
        restoredChunks.Clear();
        restoreCoordinator.ResetChunkRestoreTracking();
    }

    private void RegisterChunkGeneration(Vector3Int chunkPos, int blockCount)
    {
        using (RegisterChunkGenerationMarker.Auto())
        {
            restoredChunks.Remove(chunkPos);
            pendingBlockCountsByChunk[chunkPos] = blockCount;
            if (blockCount == 0)
            {
                MarkChunkRestored(chunkPos);
            }
        }
    }

    private void MarkBlockGenerationProcessed(Vector3Int blockPos)
    {
        using (MarkBlockGenerationProcessedMarker.Auto())
        {
            Vector3Int chunkPos = GetChunkPositionFromBlock(blockPos);
            if (!pendingBlockCountsByChunk.TryGetValue(chunkPos, out int remainingBlocks))
            {
                return;
            }

            remainingBlocks--;
            if (remainingBlocks > 0)
            {
                pendingBlockCountsByChunk[chunkPos] = remainingBlocks;
                return;
            }

            pendingBlockCountsByChunk.Remove(chunkPos);
            MarkChunkRestored(chunkPos);
        }
    }

    private void MarkChunkRestored(Vector3Int chunkPos)
    {
        if (!restoredChunks.Add(chunkPos))
        {
            return;
        }

        restoreCoordinator.NotifyChunkRestored(chunkPos);
    }

    private bool ValidateRestoreCoordinator()
    {
        if (restoreCoordinator != null)
        {
            restoreCoordinatorMissingErrorLogged = false;
            return true;
        }

        if (!restoreCoordinatorMissingErrorLogged)
        {
            restoreCoordinatorMissingErrorLogged = true;
            Debug.LogError("ChunkManager: MiningSceneRestoreCoordinator is not assigned.", this);
        }

        return false;
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

    private List<Vector3Int> BuildChunkPositionsAround(Vector3Int centerChunk, int radiusInChunks)
    {
        int radius = Mathf.Max(0, radiusInChunks);
        int sideLength = radius * 2 + 1;
        List<Vector3Int> chunkPositions = new List<Vector3Int>(sideLength * sideLength);
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                chunkPositions.Add(new Vector3Int(centerChunk.x + x, centerChunk.y + y, centerChunk.z));
            }
        }

        return chunkPositions;
    }

    public List<Vector3Int> GetBlockPositionsInChunk(Vector3Int chunkPos)
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

    public Bounds GetChunkWorldBounds(Vector3Int chunkPos)
    {
        var settings = terrainManager.Settings;
        Vector3Int startBlock = GetChunkStartBlockPosition(chunkPos);
        float blockSize = settings.blockSize;
        Vector3 size = new Vector3(
            settings.blocksPerChunk.x * blockSize,
            settings.blocksPerChunk.y * blockSize,
            blockSize);
        Vector3 min = new Vector3(
            (startBlock.x - 0.5f) * blockSize,
            (startBlock.y - 0.5f) * blockSize,
            settings.center.z + (chunkPos.z - 0.5f) * blockSize);

        return new Bounds(min + size * 0.5f, size);
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
}
