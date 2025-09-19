using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PoemController : MonoBehaviour
{
    public GameObject poem1;
    public GameObject poem2;
    public GameObject poem3;
    public GameObject poem4;
    public GameObject poem5;
    public GameObject poem6;

    public GameObject credits;

    public GameObject ouroboros1;
    public GameObject ouroboros2;
    public GameObject ouroboros3;

    private bool isPlayed = false;
    private bool text2Done = false;
    private bool text4Done = false;

    void Start()
    {
        poem1.SetActive(false);
        poem2.SetActive(false);
        poem3.SetActive(false);
        poem4.SetActive(false);
        poem5.SetActive(false);
        ouroboros1.SetActive(false);
        ouroboros2.SetActive(false);
        ouroboros3.SetActive(false);
        credits.SetActive(false);

        if (!isPlayed)
        {
            StartCoroutine(DelayEnding());
            isPlayed = true;
        }
    }

    void Update()
    {
        if(text2Done) //Swallow effect
        {
            ouroboros1.SetActive(true);
            ouroboros1.transform.Rotate(0, 0, -0.1f);

        }

        if(text4Done) //Bite effect
        {
            StartCoroutine(StartBiting());
        }
    }

    IEnumerator StartBiting()
    {
        ouroboros2.SetActive(true); // animation set played as entry, so enable object = run animation

        yield return new WaitForSeconds(1f);
        ouroboros2.SetActive(false);
        ouroboros3.SetActive(true);
    }

    IEnumerator DelayEnding()
    {
        yield return new WaitForSeconds(1);
        poem1.SetActive(true);

        yield return new WaitForSeconds(8); // reading text 1 
        poem1.SetActive(false);

        yield return new WaitForSeconds(1); // delay before text 2
        poem2.SetActive(true);

        yield return new WaitForSeconds(7); 
        poem2.GetComponent<ObjectShake>().enabled = true;
        text2Done = true; // ouroboros swallow text effect

        yield return new WaitForSeconds(1); 
        poem2.SetActive(false);

        yield return new WaitForSeconds(3); // continue text 3
        ouroboros1.SetActive(false);
        text2Done = false;
        poem3.SetActive(true);

        yield return new WaitForSeconds(10);
        poem3.SetActive(false);

        yield return new WaitForSeconds(1); // delay and text 4
        poem4.SetActive(true);

        yield return new WaitForSeconds(10); 
        poem4.GetComponent<ObjectShake>().enabled = true;
        text4Done = true; // bite effect
        
        yield return new WaitForSeconds(1); // delay before text 5
        poem4.SetActive(false);
        text4Done = false;
        ouroboros3.SetActive(false);

        yield return new WaitForSeconds(1.5f);
        poem5.SetActive(true);

        yield return new WaitForSeconds(9); 
        poem5.SetActive(false); // text 5 disapear -> run credit/change scene

        yield return new WaitForSeconds(1.5f);
        poem6.SetActive(true);

        yield return new WaitForSeconds(2f);
        poem6.SetActive(false);

        yield return new WaitForSeconds(3f); 
        credits.SetActive(true); // use animation to scroll
    }
}
