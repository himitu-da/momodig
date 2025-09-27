using UnityEngine;
using UnityEngine.UI;
public class FacillityToggle : MonoBehaviour
{
    public Toggle garageToggle;
    public Toggle workshopToggle;
    void Start()
    {
        if (ShopStateManager.Instance == null) return;

        if (ShopStateManager.Instance.startMode == ShopStateManager.ShopMode.Garage)
        {
            garageToggle.isOn = true;
        }
        else if (ShopStateManager.Instance.startMode == ShopStateManager.ShopMode.Workshop)
        {
            workshopToggle.isOn = true;
        }
    }
}
