using UnityEngine;

[CreateAssetMenu(fileName = "Pickaxe", menuName = "MomoDig/Mining/Tools/Pickaxe")]
public class Pickaxe : MiningTool
{
    /// <summary>
    /// 旧SOベースの経路は廃止。Behaviour駆動（MiningToolBehaviour.Use）をご利用ください。
    /// </summary>
    [System.Obsolete("Deprecated: Use behaviour-driven tools. Do not call Pickaxe.Use() at runtime.")]
    public override void Use(GameObject user)
    {
        Debug.LogError("[Deprecated] Pickaxe.Use() was called. Behaviour-driven architecture is in use. Assign toolPrefab and use MiningToolsController -> Behaviour.Use().");
    }
}