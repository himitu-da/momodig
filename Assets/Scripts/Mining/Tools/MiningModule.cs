using UnityEngine;

[CreateAssetMenu(fileName = "MiningModule", menuName = "Scriptable Objects/MiningModule")]
public abstract class MiningModule : ScriptableObject
{
    [Header("Damage Settings")]
    [SerializeField] protected int baseDamagePerHit = 1;
    public Stat damagePerHit = new Stat();
    
    void OnEnable()
    {
        damagePerHit.BaseValue = baseDamagePerHit;
    }

    /// <summary>
    /// 掘削範囲の中心を取得します。
    /// </summary>
    public abstract Vector3 DiggingCenter { get; }

    /// <summary>
    /// 掘削範囲のサイズを取得します。
    /// </summary>
    public abstract Vector3 DiggingSize { get; }
    
    /// <summary>
    /// 1回の攻撃で与えるダメージ量を取得します。
    /// </summary>
    public virtual int DamagePerHit => damagePerHit.IntValue;

    /// <summary>
    /// 掘削処理を実行します。
    /// </summary>
    /// <param name="user">使用者</param>
    public abstract void Execute(GameObject user);
}
