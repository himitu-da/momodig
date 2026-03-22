public static class RenderQueue
{
    // --- このプロジェクト固有のカスタムキュー ---
    // 数値が大きいほど手前に描画される

    /// <summary>ゲーム背景</summary>
    public const int Background = 4000;

    /// <summary>出口など、当たり判定のない固定オブジェクト</summary>
    public const int Scenery = 4001;

    /// <summary>ブロックやドロップアイテムなど、奥行きを持つオブジェクトの基準値</summary>
    public const int Geometry = 4100;

    /// <summary>輸送道具（トロッコ）</summary>
    public const int Minecart = 4101;

    /// <summary>プレイヤー</summary>
    public const int Player = 4200;

    /// <summary>プレイヤーに付随する採掘道具</summary>
    public const int PlayerTool = 4201;

    /// <summary>オブジェクトに付属するUI（トロッコUI等）</summary>
    public const int WorldSpaceUI = 4300;

    /// <summary>固定UI</summary>
    public const int ScreenSpaceUI = 5000;
}
