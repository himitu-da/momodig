using UnityEngine;

[CreateAssetMenu(fileName = "MiningModule", menuName = "Scriptable Objects/MiningModule")]
public abstract class MiningModule : ScriptableObject
{
    /// <summary>
    /// 掘削処理を実行します。
    /// </summary>
    /// <param name="user">使用者</param>
    public abstract void Execute(GameObject user);
}
