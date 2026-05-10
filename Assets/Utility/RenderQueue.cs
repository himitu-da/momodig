public static class RenderQueue
{
    // Larger queues are drawn later. Screen-space Overlay UI is ordered by Canvas hierarchy, not this table.
    public const int Background = 4000;
    public const int Scenery = 4001;
    public const int Geometry = 4100;
    public const int Minecart = 4101;
    public const int Player = 4200;
    public const int PlayerTool = 4201;
    public const int Foreground = 4250;
    public const int WorldSpaceUI = 4300;
    public const int ScreenSpaceUI = 5000;

    public static int Resolve(RenderQueueLayer layer)
    {
        if (!System.Enum.IsDefined(typeof(RenderQueueLayer), layer))
        {
            throw new System.ArgumentOutOfRangeException(nameof(layer), layer, "Unsupported render queue layer.");
        }

        return (int)layer;
    }
}

public enum RenderQueueLayer
{
    Background = RenderQueue.Background,
    Scenery = RenderQueue.Scenery,
    Geometry = RenderQueue.Geometry,
    Minecart = RenderQueue.Minecart,
    Player = RenderQueue.Player,
    PlayerTool = RenderQueue.PlayerTool,
    Foreground = RenderQueue.Foreground,
    WorldSpaceUI = RenderQueue.WorldSpaceUI,
    ScreenSpaceUI = RenderQueue.ScreenSpaceUI
}
