using UnityEngine;
using System.Collections.Generic;

// トロッコクラス
public class Minecart
{
    public GameObject gameObject; // トロッコのゲームオブジェクト
    public MinecartMovement movement; // トロッコの移動コンポーネント
    public Dictionary<ResourceType, int> resources; // 資源と量
    public float time;

    // コンストラクタで初期化
    public Minecart(GameObject obj)
    {
        gameObject = obj;
        movement = obj.GetComponent<MinecartMovement>();
        if (movement == null)
        {
            movement = obj.AddComponent<MinecartMovement>();
        }
        resources = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
        time = 0f;
        isGoingToGround = false; // 地上に行く途中で、プレイヤーを追従しないフラグ
    }

    // 現在の積載量を計算
    public int CurrentLoad
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<ResourceType, int> resource in resources)
            {
                total += resource.Value;
            }
            return total;
        }
    }

    // 積載物を空にする
    public void ClearResources()
    {
        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
    }

    public bool isGoingToGround; // 地上に行く途中で、プレイヤーを追従しないフラグ
}
