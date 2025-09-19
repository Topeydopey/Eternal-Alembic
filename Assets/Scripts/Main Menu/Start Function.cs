using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartFunction : MonoBehaviour
{
    public GameObject head;
    public GameObject replace;
    
    private bool isTriggered = false;
    public bool isStart = false;

    private bool inputLocked = true;
    
    public FadeScript fadeScript; 

    void Start()
    {
        head.SetActive(false);
        replace.SetActive(true);

        StartCoroutine(DelayInput());
    }

    void Update()
    {
        if (Keyboard.current.anyKey.wasPressedThisFrame && !inputLocked) //if(Input.anyKey)
        {
            isStart = true;
            Debug.Log("Pressed");
        }

        if (!isTriggered && isStart)
        {
            isTriggered = true;
            StartCoroutine(StartGame());
        }
    }

    IEnumerator DelayInput()
    {
        yield return new WaitForSeconds(3);
        inputLocked = false;
    }

    IEnumerator StartGame()
    {
        replace.SetActive(false);
        head.SetActive(true);

        fadeScript.FadeIn();

        yield return new WaitForSeconds(3);

        SceneManager.LoadScene("Home");

    }
}
