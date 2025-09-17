using UnityEngine;
using System.Collections;

public class OpenAnimation : MonoBehaviour
{

    public GameObject left;
    public GameObject right;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(StopNow());
    }

    IEnumerator StopNow()
    {
        yield return new WaitForSeconds(2.5f);
        Destroy(this);
        left.SetActive(false);
        right.SetActive(false);

    }
}
