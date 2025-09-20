using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class Digger : MonoBehaviour
{
    public BoxCollider DiggingArea => diggingArea; // 掘削範囲のBoxCollider
    private BoxCollider diggingArea;
    private MiningModule pendingMiningModule; // 実行待機中の掘削モジュール
    private MiningInfo pendingMiningInfo; // 実行待機中の掘削情報
    private bool isDiggingAreaOverridden = false; // 掘削範囲が外部から上書きされたか

    void Awake()
    {
        // 自身のゲームオブジェクトにアタッチされているBoxColliderを取得
        diggingArea = GetComponent<BoxCollider>();
        // もしBoxColliderがなければ、新しく追加する
        if (diggingArea == null)
        {
            diggingArea = gameObject.AddComponent<BoxCollider>();
            diggingArea.isTrigger = true; // OverlapBoxで使用するためトリガーにする
        }
    }

    /// <summary>
    /// 実行待機中の掘削モジュールと掘削情報を設定します。
    /// </summary>
    /// <param name="module">掘削モジュール</param>
    /// <param name="info">掘削情報</param>
    public void SetPendingMining(MiningModule module, MiningInfo info)
    {
        pendingMiningModule = module;
        pendingMiningInfo = info;
    }

    /// <summary>
    /// アニメーションイベントから呼び出され、保留中の掘削を実行します。
    /// </summary>
    public void ExecuteDigFromAnimation()
    {
        if (pendingMiningModule != null)
        {
            // 掘削範囲が外部から上書きされていない場合のみ、モジュールのデフォルト値を使用
            if (!isDiggingAreaOverridden)
            {
                SetDiggingAreaParameters(pendingMiningModule.DiggingCenter, pendingMiningModule.DiggingSize);
            }

            // 掘削を実行
            Dig(pendingMiningModule.DamagePerHit, pendingMiningInfo);

            // 実行後に保留中のモジュールとフラグをクリア
            pendingMiningModule = null;
            isDiggingAreaOverridden = false;
        }
        else
        {
            Debug.LogWarning("Pending mining module is not set. Cannot execute dig.");
        }
    }

    // SphereGeneratorから呼び出される
    public void SetDiggingArea(BoxCollider area)
    {
        diggingArea = area;
    }

    // PlayerControllerから呼び出される
    public void UpdateDiggingAreaTransform(Vector3 position, Quaternion rotation)
    {
        if (diggingArea != null)
        {
            // Player自体が回転するため、Diggerのローカル回転はリセットし、
            // 位置のオフセットのみを設定する
            diggingArea.transform.localPosition = position;
            diggingArea.transform.localRotation = Quaternion.identity;
        }
    }

    // MiningModuleから呼び出される
    public void SetDiggingAreaParameters(Vector3 center, Vector3 size)
    {
        if (diggingArea != null)
        {
            diggingArea.center = center;
            diggingArea.size = size;
            isDiggingAreaOverridden = true; // 範囲が設定されたことをマーク
        }
    }


    void Update()
    {
    }

    public void Dig(int damagePerHit, MiningInfo info)
    {
        DigAsync(damagePerHit, info).Forget();
    }

    private async UniTask DigAsync(int damagePerHit, MiningInfo info)
    {
        if (diggingArea == null)
        {
            Debug.LogError("Digging Area is not set.");
            return;
        }

        // diggingAreaのワールド中心を計算
        Vector3 worldCenter = diggingArea.transform.TransformPoint(diggingArea.center);

        // OverlapBoxで範囲内のすべてのコライダーを取得（中心を正しく使用）
        Collider[] hitColliders = Physics.OverlapBox(
            worldCenter,
            diggingArea.size / 2,
            diggingArea.transform.rotation
        );

        // ユニークなブロックを収集（複数ヒット回避）
        HashSet<Block> hitBlocks = new HashSet<Block>();
        foreach (var hitCollider in hitColliders)
        {
            Block block = hitCollider.GetComponent<Block>();
            if (block != null)
                hitBlocks.Add(block);
        }

        // BoxColliderのワールド空間での8つの頂点を計算し、それらを完全に含むAABB (Axis-Aligned Bounding Box) を作成します。
        // これにより、回転したBoxColliderも正確に表現できます。
        var points = new Vector3[8];
        var center = diggingArea.center;
        var size = diggingArea.size / 2;
        points[0] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, -size.y, -size.z));
        points[1] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, -size.y, -size.z));
        points[2] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, -size.y, size.z));
        points[3] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, -size.y, size.z));
        points[4] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, size.y, -size.z));
        points[5] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, size.y, -size.z));
        points[6] = diggingArea.transform.TransformPoint(center + new Vector3(size.x, size.y, size.z));
        points[7] = diggingArea.transform.TransformPoint(center + new Vector3(-size.x, size.y, size.z));

        List<UniTask> diggingTasks = new List<UniTask>();
        foreach (var block in hitBlocks)
        {
            diggingTasks.Add(block.DigVoxels(diggingArea, damagePerHit));
        }

        // 全ての掘削処理が完了するのを待つ
        await UniTask.WhenAll(diggingTasks);

        // 掘削範囲内のドロップアイテムに力を加える
        if (DroppedItemManager.Instance != null)
        {
            Vector3 expandedSize = diggingArea.size + new Vector3(2, 2, 2);
            DroppedItemManager.Instance.ApplyForceToItemsInRadius(worldCenter, expandedSize, diggingArea.transform.rotation, info);
        }
    }

}
