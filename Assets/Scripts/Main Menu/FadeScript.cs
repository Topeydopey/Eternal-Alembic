using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadeScript : MonoBehaviour
{
    public CanvasGroup fadeCanvasGroup;

    public float fadeDuration = 4.5f;

    public GameObject blackScreen;


    public void Start()
    {
        fadeCanvasGroup.alpha = 0f;
        blackScreen.SetActive(false);
    }


    public void FadeIn()
    {
        StartCoroutine(FadeBlack());
    }

    public IEnumerator FadeBlack()
    {
        blackScreen.SetActive(true);
        fadeCanvasGroup.alpha = 0f;

        // Increase the alpha of the black screen img
        fadeCanvasGroup.blocksRaycasts = true;
        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            fadeCanvasGroup.alpha = time / fadeDuration;
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
        yield return new WaitForSeconds(0.2f);
    }
}

