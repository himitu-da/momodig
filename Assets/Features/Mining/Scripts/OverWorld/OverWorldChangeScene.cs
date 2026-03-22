using UnityEngine;
using UnityEngine.SceneManagement;
public class OverWorldChangeScene : MonoBehaviour
{
    public ChangeScene changescene;
    public void SelectMineplace()
    {
        changescene.OnClickToChangeScene("MiningScene");
    }
    public void SelectWorkshop()
    {
        ShopStateManager.Instance.startMode = ShopStateManager.ShopMode.Workshop;
        changescene.OnClickToChangeScene("ShopScene");
    }
    public void SelectGarage()
    {
        ShopStateManager.Instance.startMode = ShopStateManager.ShopMode.Garage;
        changescene.OnClickToChangeScene("ShopScene");
    }
}
