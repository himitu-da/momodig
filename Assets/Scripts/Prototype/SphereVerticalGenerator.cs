using UnityEngine;

public class SphereVerticalGenerator : BasePlayerGenerator
{
    protected override PlayerController.MoveMode moveMode => PlayerController.MoveMode.Vertical;
}
