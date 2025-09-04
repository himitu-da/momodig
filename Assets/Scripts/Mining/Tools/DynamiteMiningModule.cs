using UnityEngine;

[CreateAssetMenu(fileName = "DynamiteMiningModule", menuName = "MomoDig/Mining/Modules/Dynamite")]
public class DynamiteMiningModule : MiningModule
{
    [Header("Explosion Area (for future)")]
    [Tooltip("爆風の中心オフセット（将来使用）")]
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    [Tooltip("爆風のサイズ（将来使用）")]
    [SerializeField] private Vector3 size = new Vector3(4f, 4f, 4f);

    public override Vector3 DiggingCenter => centerOffset;
    public override Vector3 DiggingSize => size;

    // まだ投擲物側で処理するため空実装
    public override void Execute(GameObject user) { }
}
