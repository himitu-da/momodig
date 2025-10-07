using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuyManager : MonoBehaviour
{
    private List<RequestMaterial> requestmaterials;

    public void setmaterials(List<RequestMaterial> materialrequest)
    {
        requestmaterials = materialrequest;
    }
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
