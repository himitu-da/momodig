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
    }
}
