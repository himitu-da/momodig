public enum RenderQueueLayer
{
    Background,
    Scenery,
    Geometry,
    Minecart,
    Player,
    PlayerTool,
    WorldSpaceUI,
    ScreenSpaceUI
}

public static class RenderQueue
{
    public const int Background = 4000;
    public const int Scenery = 4001;
    public const int Geometry = 4100;
    public const int Minecart = 4101;
    public const int Player = 4200;
    public const int PlayerTool = 4201;
    public const int WorldSpaceUI = 4300;
    public const int ScreenSpaceUI = 5000;

    public static int Resolve(RenderQueueLayer layer)
    {
        switch (layer)
        {
            case RenderQueueLayer.Background:
                return Background;
            case RenderQueueLayer.Scenery:
                return Scenery;
            case RenderQueueLayer.Geometry:
                return Geometry;
            case RenderQueueLayer.Minecart:
                return Minecart;
            case RenderQueueLayer.Player:
                return Player;
            case RenderQueueLayer.PlayerTool:
                return PlayerTool;
            case RenderQueueLayer.WorldSpaceUI:
                return WorldSpaceUI;
            case RenderQueueLayer.ScreenSpaceUI:
                return ScreenSpaceUI;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(layer), layer, "Unsupported render queue layer.");
        }
    }
}
