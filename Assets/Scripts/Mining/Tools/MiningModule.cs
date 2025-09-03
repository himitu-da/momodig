using UnityEngine;

[CreateAssetMenu(fileName = "MiningModule", menuName = "Scriptable Objects/MiningModule")]
public abstract class MiningModule : ScriptableObject
{
    /// <summary>
    /// 掘削範囲の中心を取得します。
    /// </summary>
    public abstract Vector3 DiggingCenter { get; }

    /// <summary>
    /// 掘削範囲のサイズを取得します。
    /// </summary>
    public abstract Vector3 DiggingSize { get; }

    /// <summary>
    /// 掘削処理を実行します。
    /// </summary>
    /// <param name="user">使用者</param>
    public abstract void Execute(GameObject user);
}
