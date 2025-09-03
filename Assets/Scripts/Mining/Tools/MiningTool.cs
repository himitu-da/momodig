using UnityEngine;

[CreateAssetMenu(fileName = "MiningTool", menuName = "Scriptable Objects/MiningTool")]
public abstract class MiningTool : ScriptableObject
{
    [Header("ツール設定")]
    [SerializeField] protected string toolName;
    [SerializeField] protected Sprite toolIcon;

    [Header("掘削モジュール")]
    public MiningModule miningModule;

    /// <summary>
    /// ツールを使用します。
    /// </summary>
    /// <param name="user">使用者</param>
    public abstract void Use(GameObject user);
}
