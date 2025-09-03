using UnityEngine;

[CreateAssetMenu(fileName = "Pickaxe", menuName = "MomoDig/Mining/Tools/Pickaxe")]
public class Pickaxe : MiningTool
{
    public override void Use(GameObject user)
    {
        if (miningModule != null)
        {
            miningModule.Execute(user);
        }
        else
        {
            Debug.LogWarning($"MiningModule is not set for {toolName}.");
        }
    }
}