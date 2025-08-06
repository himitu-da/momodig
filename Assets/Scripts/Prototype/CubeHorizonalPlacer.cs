using UnityEngine;

public class CubeHorizonalPlacer : MonoBehaviour
{
    [SerializeField]private int k;      // Y座標
    [SerializeField]private int minX;   // X座標の最小値
    [SerializeField]private int maxX;   // X座標の最大値
    [SerializeField]private int minZ;   // Z座標の最小値
    [SerializeField]private int maxZ;   // Z座標の最大値

    void Start()
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.position = new Vector3(x, k, z);
                g.transform.parent = this.transform;
                g.tag = "Block"; // ブロックにタグを設定
                if ((x + z) % 2 == 0)
                {
                    g.GetComponent<Renderer>().material.color = Color.blue;
                } else {
                    g.GetComponent<Renderer>().material.color = Color.red;
                }
            }
        }
    }
}
