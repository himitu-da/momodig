using UnityEngine;

public class CubeVerticalPlacer : MonoBehaviour
{
    [SerializeField]private int k;      // Z座標
    [SerializeField]private int minX;   // X座標の最小値
    [SerializeField]private int maxX;   // X座標の最大値
    [SerializeField]private int minY;   // Y座標の最小値
    [SerializeField]private int maxY;   // Y座標の最大値

    void Start()
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.position = new Vector3(x, y, k);
                g.transform.parent = this.transform;
                g.tag = "Block"; // ブロックにタグを設定
                if ((x + y) % 2 == 0)
                {
                    g.GetComponent<Renderer>().material.color = Color.blue;
                } else {
                    g.GetComponent<Renderer>().material.color = Color.red;
                }
            }
        }
    }
}
