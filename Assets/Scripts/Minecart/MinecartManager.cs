using UnityEngine;
using System.Collections.Generic;

// トロッコ管理クラス
public class MinecartManager : MonoBehaviour
{
    [Header("プレイヤー設定")]
    public Transform playerTransform; // プレイヤーのTransform

    [Header("トロッコ設定")]
    public GameObject minecartPrefab; // トロッコのプレハブ
    public int CartCapacity = 500;
    [Header("トロッコ移動設定")]
    public Vector3 groundStationPosition = Vector3.zero; // 地上の停留点
    public float followMoveSpeed = 5f; // プレイヤー追従時の速度
    public float groundMoveSpeed = 10f; // 地上へ向かう際の速度
    public float unloadTime = 2.0f; // 地上での荷降ろし時間
    public int cartunit = 2;
    public List<Minecart> minecarts = new List<Minecart>();

    [Header("軌跡追従設定")]
    [Tooltip("軌跡を記録する最小移動距離（格子点の間隔）")]
    public float pathRecordInterval = 1.0f;
    [Tooltip("トロッコ間の距離（軌跡点の数）")]
    public int cartDistanceInPoints = 5;
    [Tooltip("記録する軌跡の最大数")]
    public int maxPathPoints = 100;
    private List<Vector3> pathPoints = new List<Vector3>();

    [Header("トロッコ状態")]
    public bool digable = true;

    void Start()
    {
        // トロッコを必要数まで生成
        while (minecarts.Count < cartunit)
        {
            addnewcart();
        }
        updatevalue(0, ResourceType.Stone, 10);
        // 内容を確認
        foreach (Minecart cart in minecarts)
        {
            foreach (KeyValuePair<ResourceType, int> element in cart.resources)
            {
                Debug.Log($"Key={element.Key}, Amount={element.Value}");
            }
        }

        // 軌跡記録の初期化
        if (playerTransform != null)
        {
            pathPoints.Add(playerTransform.position);
        }
    }

    // 新しいトロッコを追加
    public void addnewcart()
    {
        if (minecartPrefab != null)
        {
            GameObject newMinecartObject = Instantiate(minecartPrefab, Vector3.zero, Quaternion.identity);
            // MinecartMovementコンポーネントがなければ追加する
            MinecartMovement movement = newMinecartObject.GetComponent<MinecartMovement>();
            if (movement == null)
            {
                movement = newMinecartObject.AddComponent<MinecartMovement>();
            }
            movement.moveSpeed.BaseValue = followMoveSpeed; // 初期速度を設定
            minecarts.Add(new Minecart(newMinecartObject));
        }
        else
        {
            Debug.LogError("minecartPrefabが設定されていません！");
        }
    }

    // キューの先頭トロッコの指定資源をvalueだけ追加
    public void updatevalue(int minecartnum, ResourceType type, int value)
    {
        // 常に現在の利用トロッコ（キューの先頭）を対象とする
        int currentCart = 0; // 先頭のトロッコを使用
        minecarts[currentCart].resources[type] += value;
        // 容量チェック
        if (minecarts[currentCart].CurrentLoad >= CartCapacity)
        {
            SendCartToHome(currentCart);
        }
    }

    // トロッコを地上(0,0,0)に送り、キューを進める
    private void SendCartToHome(int minecartnum)
    {
        Minecart cart = minecarts[minecartnum];
        if (cart.gameObject != null && minecartnum == 0) // 先頭のトロッコのみ処理
        {
            cart.state = MinecartState.GoingToGround; // 状態を地上へ移動中に変更
            cart.movement.targetPosition = groundStationPosition; // 地上の停留点に移動
            cart.movement.moveSpeed.BaseValue = groundMoveSpeed; // 地上への移動速度を設定

            // 使用済みのトロッコをリストの末尾に移動（キューの末尾へ）
            Minecart movedCart = minecarts[0];
            minecarts.RemoveAt(0);
            minecarts.Add(movedCart);

            // 次のトロッコ（新しい先頭）がすぐに利用可能
            digable = true;
        }
    }

    // トロッコの位置を更新する
    public void UpdateMinecartPositions()
    {
        if (pathPoints.Count == 0) return;

        for (int i = 0; i < minecarts.Count; i++)
        {
            Minecart cart = minecarts[i];
            // 追従状態のトロッコのみ位置を更新
            if (cart.state == MinecartState.Following && cart.gameObject != null && cart.movement != null)
            {
                // 各トロッコの目標となる軌跡リスト上のインデックスを計算
                int targetIndex = pathPoints.Count - 1 - (cartDistanceInPoints * (i + 1));

                // インデックスが範囲外にならないように調整
                if (targetIndex < 0)
                {
                    targetIndex = 0;
                }

                Vector3 targetPosition = pathPoints[targetIndex];
                cart.movement.targetPosition = targetPosition;
            }
        }
    }

    // 互換用オーバーロード（旧呼び出しに対応）
    // 旧API: プレイヤー位置・最後の移動方向・オフセットを受け取る
    // 実装: プレイヤー位置を軌跡に取り込み、軌跡ベースの追従ロジックへ委譲
    public void UpdateMinecartPositions(Vector3 playerPosition, Vector3 playerLastMoveDirection, float offset)
    {
        // 軌跡初期化または追記
        if (pathPoints.Count == 0)
        {
            pathPoints.Add(playerPosition);
        }
        else
        {
            if (Vector3.Distance(playerPosition, pathPoints[pathPoints.Count - 1]) > pathRecordInterval)
            {
                pathPoints.Add(playerPosition);
                if (pathPoints.Count > maxPathPoints)
                {
                    pathPoints.RemoveAt(0);
                }
            }
        }

        // 軌跡に基づいて各トロッコの目標位置を更新
        UpdateMinecartPositions();
    }

    void Update()
    {
        // プレイヤーの軌跡を記録
        if (playerTransform != null)
        {
            if (pathPoints.Count > 0)
            {
                float distance = Vector3.Distance(playerTransform.position, pathPoints[pathPoints.Count - 1]);
                if (distance > pathRecordInterval)
                {
                    pathPoints.Add(playerTransform.position);
                    // 軌跡リストが最大数を超えたら古いものから削除
                    if (pathPoints.Count > maxPathPoints)
                    {
                        pathPoints.RemoveAt(0);
                    }
                }
            }
            else
            {
                pathPoints.Add(playerTransform.position);
            }
        }

        // トロッコの位置を更新
        UpdateMinecartPositions();

        // トロッコの状態を更新
        foreach (Minecart cart in minecarts)
        {
            switch (cart.state)
            {
                case MinecartState.GoingToGround:
                    // 地上到着チェック
                    if (Vector3.Distance(cart.gameObject.transform.position, groundStationPosition) < 0.1f)
                    {
                        cart.state = MinecartState.Unloading; // 状態を荷降ろし中に変更
                        cart.time = unloadTime; // 荷降ろしタイマーを設定

                        // 中身をStorageManagerに移す
                        if (StorageManager.Instance != null)
                        {
                            StorageManager.Instance.AddResources(cart.resources);
                        }
                        cart.ClearResources();
                    }
                    break;

                case MinecartState.Unloading:
                    // 荷降ろしタイマーを減らす
                    if (cart.time > 0)
                    {
                        cart.time -= Time.deltaTime;
                    }
                    else
                    {
                        cart.state = MinecartState.Following; // 状態を追従中に変更
                        cart.movement.moveSpeed.BaseValue = followMoveSpeed; // 追従速度に戻す
                    }
                    break;

                case MinecartState.Following:
                    // 追従中の処理はUpdateMinecartPositionsで行う
                    break;
            }
        }

        // 採掘可能かどうかの判定
        if (minecarts.Count > 0 && minecarts[0].state == MinecartState.Following)
        {
            digable = true;
        }
        else
        {
            digable = false;
        }
    }
}
