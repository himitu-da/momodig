public static class RenderQueue
{
    // --- Unity標準の描画キュー (リファレンス用) ---
    // これらはUnityが内部的に使用する基準値です。
    // カスタムキューの値を決める際の比較対象として記載しています。

    /// <summary>一番最初に描画される。背景やスカイボックス用。</summary>
    public const int Background = 1000;

    /// <summary>デフォルト。ほとんどの不透明なオブジェクトが使用。</summary>
    public const int Geometry = 2000;

    /// <summary>透明部分を切り抜くオブジェクト用。Geometryの後、Transparentの前に描画。</summary>
    public const int AlphaTest = 2450;

    /// <summary>半透明オブジェクト用。オブジェクトの奥から手前に向かって描画される。</summary>
    public const int Transparent = 3000;

    // --- このプロジェクト固有のカスタムキュー ---
    // UIなど、常に最前面に表示したいオブジェクト群。
    // Transparentより大きい値を設定する。

    /// <summary>カスタムキューの基準値。UnityのUI（2D）よりは手前に来るようにOverlay(4000)を基準とする。</summary>
    public const int OverlayBase = 4000;

    /// <summary>ワールド空間に表示されるUIなど、プレイヤーよりは奥だが常に表示したいオブジェクト用</summary>
    public const int WorldSpaceUI = OverlayBase + 0;

    /// <summary>追従してくるコンパニオンなど</summary>
    public const int Companion = OverlayBase + 1;

    /// <summary>プレイヤーキャラクター</summary>
    public const int Player = OverlayBase + 2;

    /// <summary>プレイヤーが持つツールなど、最前面に表示したいオブジェクト</summary>
    public const int Tool = OverlayBase + 3;
    
    /// <summary>スクリーン空間のUIなど、最も手前に表示したいオブジェクト用</summary>
    public const int UI = 5000;
}
