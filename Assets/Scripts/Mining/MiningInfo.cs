using UnityEngine;

/// <summary>
/// 掘削の種類を定義します。
/// </summary>
public enum MiningType
{
    /// <summary>
    /// ピッケルのような方向性のある掘削
    /// </summary>
    Directional,

    /// <summary>
    /// ピッケルの振り向きに合わせた円弧状の掘削
    /// </summary>
    ArcSwing,
    
    /// <summary>
    /// ダイナマイトのような爆発による掘削
    /// </summary>
    Explosive
}

/// <summary>
/// 掘削に関する情報を格納する構造体です。
/// </summary>
public struct MiningInfo
{
    public MiningType Type;
    public Vector3 SourcePoint; // 掘削の起点（プレイヤー位置や爆心地）
    public Vector3 Direction;   // 掘削方向（Directionalの場合）
    public float Force;         // 初速の強さ
    public bool IsFacingRight;  // 右を向いているか（ArcSwingの場合）

    /// <summary>
    /// 方向性のある掘削用のMiningInfoを作成します。
    /// </summary>
    public static MiningInfo Directional(Vector3 sourcePoint, Vector3 direction, float force)
    {
        return new MiningInfo
        {
            Type = MiningType.Directional,
            SourcePoint = sourcePoint,
            Direction = direction,
            Force = force
        };
    }

    /// <summary>
    /// 爆発による掘削用のMiningInfoを作成します。
    /// </summary>
    public static MiningInfo Explosive(Vector3 sourcePoint, float force)
    {
        return new MiningInfo
        {
            Type = MiningType.Explosive,
            SourcePoint = sourcePoint,
            Direction = Vector3.zero, // Explosiveでは使用しない
            Force = force,
            IsFacingRight = false
        };
    }

    /// <summary>
    /// 円弧状の掘削用のMiningInfoを作成します。
    /// </summary>
    public static MiningInfo ArcSwing(Vector3 sourcePoint, bool isFacingRight, float force)
    {
        return new MiningInfo
        {
            Type = MiningType.ArcSwing,
            SourcePoint = sourcePoint,
            Direction = Vector3.zero, // ArcSwingでは使用しない
            Force = force,
            IsFacingRight = isFacingRight
        };
    }
}
