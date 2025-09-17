using UnityEngine;
using System.Collections;

public class OuroborosRotate : MonoBehaviour
{
    public GameObject ouroboros;
    public StartFunction startScript;

    public float rotateSpeed;

    public bool isSnapped = false;


    void Update()
    {
            ouroboros.transform.Rotate(0, 0, -rotateSpeed);
        

        if(startScript.isStart && !isSnapped)
        {
            ouroboros.transform.rotation = Quaternion.Euler(0, 0, 95);
            
            isSnapped = true;

            rotateSpeed = 0.1f;
        }
    }
}
