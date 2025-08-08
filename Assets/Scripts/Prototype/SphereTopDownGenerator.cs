using UnityEngine;

public class SphereTopDownGenerator : BasePlayerGenerator
{
    protected override PlayerController.MoveMode moveMode => PlayerController.MoveMode.TopDown;
}
