using UnityEngine;

public abstract class BaseCubePlacer : MonoBehaviour
{
    [SerializeField] protected float chunkSize = 16.0f; // 1チャンクのワールドサイズ
    [SerializeField] protected Texture2D texture1;
    [SerializeField] protected Texture2D texture2;
    [SerializeField] protected int voxelHp = 3;
    [SerializeField] protected int voxelSize = 4;
    
    [Header("Dropped Item Settings")]
    [SerializeField] protected GameObject droppedItemPrefab; // ドロップアイテムのPrefab（オプション）
    [SerializeField] protected bool disableRotation = true; // 回転を無効化するかどうか
    [SerializeField] protected bool autoScale = true; // Prefabのスケールを自動調整するかどうか
    [SerializeField] protected float scaleMultiplier = 0.8f; // スケール倍率（voxelSizeに対する倍率）

    protected virtual void Start()
    {
        Generate();
    }

    protected abstract void Generate();

    protected Vector3Int GetWorldCenter(Vector3Int localCenter)
    {
        // このメソッドは現在使われておらず、意図も不明確なため、
        // chunkSizeを使ったとしても正しい結果を返さない可能性が高い。
        // 将来的に必要になった場合、再設計する。
        // return Vector3Int.RoundToInt(transform.position / chunkSize) + localCenter;
        return localCenter;
    }

    // 派生クラスで使用するチャンク作成ヘルパー
    protected void CreateChunk(Vector3Int chunkPos, bool[,,] pattern)
    {
        GameObject chunkObj = new GameObject("Chunk_" + chunkPos);
        chunkObj.transform.parent = transform;
        // 親オブジェクトからの相対位置として設定する
        chunkObj.transform.localPosition = (Vector3)chunkPos * chunkSize;

        // チャンクの表示サイズがchunkSizeになるようにスケールを調整する
        // VoxelChunkは内部的に自身のChunkSizeの大きさでメッシュを生成するため
        float scale = chunkSize / voxelSize;
        chunkObj.transform.localScale = new Vector3(scale, scale, scale);

        VoxelChunk chunk = chunkObj.AddComponent<VoxelChunk>();
        var renderer = chunkObj.GetComponent<MeshRenderer>();

        // Material設定（URP Transparent）
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_AlphaClip", 1); // Alpha Clipping
        mat.mainTexture = texture1; // デフォルトテクスチャ（拡張でパターン対応）
        renderer.material = mat;

        // VoxelChunkを初期化（テクスチャも一緒に渡す）
        chunk.Initialize(pattern, voxelSize, chunkSize, voxelHp, droppedItemPrefab, disableRotation, autoScale, scaleMultiplier, texture1, texture2);
    }
}
