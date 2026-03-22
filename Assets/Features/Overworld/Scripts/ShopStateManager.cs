using UnityEngine;

public class ShopStateManager : MonoBehaviour
{
    public static ShopStateManager Instance { get; private set; }

    public enum ShopMode { Garage, Workshop }
    public ShopMode startMode = ShopMode.Garage;
    void Awake()
    {
        // Singletonパターン（重複して作られたら破棄）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
