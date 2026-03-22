using UnityEngine;
using System.Collections.Generic;
using TMPro;

// トロッコクラス

public enum MinecartState
{
    Following,      // プレイヤーを追従中
    GoingToGround,  // 地上へ移動中
    Unloading       // 地上で荷降ろし中
}

public class Minecart
{
    public GameObject gameObject; // トロッコのゲームオブジェクト
    public MinecartMovement movement; // トロッコの移動コンポーネント
    public Dictionary<ResourceType, int> resources; // 資源と量
    public float time;
    public MinecartState state; // トロッコの現在の状態
    public TextMeshProUGUI capacityText; // UIテキストへの参照

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
        state = MinecartState.Following; // 初期状態は追従
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

}
