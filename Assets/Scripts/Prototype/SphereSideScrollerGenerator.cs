using UnityEngine;

public class SphereSideScrollerGenerator : BasePlayerGenerator
{
    protected override PlayerController.MoveMode moveMode => PlayerController.MoveMode.SideScroller;
}
