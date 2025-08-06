using UnityEngine;

public class SphereHorizonalGenerator : BasePlayerGenerator
{
    protected override PlayerController.MoveMode moveMode => PlayerController.MoveMode.Horizonal;
}
