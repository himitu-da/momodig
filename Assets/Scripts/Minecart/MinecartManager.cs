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
    private int usingcart = 0;
    public float cartcooltime;
    public float DeltaTime;
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
            if (newMinecartObject.GetComponent<MinecartMovement>() == null)
            {
                newMinecartObject.AddComponent<MinecartMovement>();
            }
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
    // minecartnum番目のトロッコをcartcooltime間送信する、cartcooltime>0fならばトロッコは使用しているものとみなし、利用不可
    public void settime(int minecartnum, float cartcooltime)
    {
        minecarts[minecartnum].time += cartcooltime;
    }

    // トロッコを地上(0,0,0)に送り、キューを進める
    private void SendCartToHome(int minecartnum)
    {
        Minecart cart = minecarts[minecartnum];
        if (cart.gameObject != null && minecartnum == 0) // 先頭のトロッコのみ処理
        {
            cart.isGoingToGround = true; // 地上に行く途中で追従を停止
            cart.movement.targetPosition = Vector3.zero; // 地上に移動
            cart.time = cartcooltime; // 送出時間を設定

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
            if (cart.gameObject != null && cart.movement != null && !cart.isGoingToGround)
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

        // トロッコの位置を更新
        UpdateMinecartPositions();

        // 必要に応じて処理
        //time>0fのトロッコはDeltaTime(Time.DeltaTimeと同値、timemanager的なものを作るまでは待機)ずつ減らす
        foreach (Minecart cart in minecarts)
        {
            if (0f < cart.time)
            {
                cart.time -= Time.deltaTime;
            }
        }
        // 地上到着チェック
        for (int i = 0; i < minecarts.Count; i++)
        {
            Minecart cart = minecarts[i];
            if (cart.isGoingToGround && Vector3.Distance(cart.gameObject.transform.position, Vector3.zero) < 1f)
            {
                // 地上に到着したら中身を空にして追従に戻す
                cart.ClearResources();
                cart.isGoingToGround = false; // 追従可能に
            }
        }

        //利用中のトロッコはキューの先頭（index 0）、minecarts[0].time <= 0fならば、このカートは積載可能とみなす
        usingcart = 0; // 常に先頭のトロッコを使用
        if (minecarts.Count > 0 && minecarts[usingcart].time <= 0f)
        {
            if (!digable)   //もし、digable=falseなのにminecarts[usingcart].time <= 0fであるならばtrueにする
            {
                digable = !digable;
            }
            //ここに資材を追加する適当なプログラムを入力する
        }
        else    // キューが空の場合や利用不可である時を考える
        {
            digable = false;    //トロッコは使えないものとする
        }
    }
}
