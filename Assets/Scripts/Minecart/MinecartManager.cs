using UnityEngine;
using System.Collections.Generic;

// トロッコ管理クラス
public class MinecartManager : MonoBehaviour
{
    [Header("トロッコ設定")]
    public GameObject minecartPrefab; // トロッコのプレハブ
    public int CartCapacity = 500;
    public int cartunit = 2;
    public List<Minecart> minecarts = new List<Minecart>();

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

    // minecartnum番目のトロッコの指定資源をvalueだけ追加
    public void updatevalue(int minecartnum, ResourceType type, int value)
    {
        minecarts[minecartnum].resources[type] += value;
    }
    // minecartnum番目のトロッコをcartcooltime間送信する、cartcooltime>0fならばトロッコは使用しているものとみなし、利用不可
    public void settime(int minecartnum, float cartcooltime)
    {
        minecarts[minecartnum].time += cartcooltime;
    }

    // トロッコの位置を更新する
    public void UpdateMinecartPositions(Vector3 playerPosition, Vector3 playerLastMoveDirection, float offset)
    {
        for (int i = 0; i < minecarts.Count; i++)
        {
            Minecart cart = minecarts[i];
            if (cart.gameObject != null && cart.movement != null)
            {
                // プレイヤーの後ろに追従するようなオフセットを計算
                Vector3 targetPosition = playerPosition - playerLastMoveDirection * (offset * (i + 1));
                cart.movement.targetPosition = targetPosition;
            }
        }
    }

    void Update()
    {
        // 必要に応じて処理
        //time>0fのトロッコはDeltaTime(Time.DeltaTimeと同値、timemanager的なものを作るまでは待機)ずつ減らす
        foreach (Minecart cart in minecarts)
        {
            if (0f < cart.time)
            {
                cart.time -= Time.deltaTime;
            }
        }
        //利用中のトロッコはusingcart番目のトロッコ、minecarts[usingcart].time <= 0fならば、このカートは積載可能とみなす
        if (minecarts[usingcart].time <= 0f)
        {
            if (!digable)   //もし、digable=falseなのにminecarts[usingcart].time <= 0fであるならばtrueにする
            {
                digable = !digable;
            }
            //ここに資材を追加する適当なプログラムを入力する
        }
        else    //timeが設定された=利用不可である時を考える
        {
            digable = false;    //トロッコは使えないものとする
            for (int i = 0; i < minecarts.Count; i++)
            {
                if (minecarts[i].time <= 0f)    //0からtime<=0fのトロッコを探す
                {
                    usingcart = i;          //存在するならば、利用するトロッコを更新する
                    digable = true;         //使えるトロッコが存在するため、掘ることができる
                }
            }
        }
    }
}
