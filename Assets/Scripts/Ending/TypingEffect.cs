using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TypingEffect : MonoBehaviour
{
    public Text targetText; // Assign your UI Text component in the Inspector
    public float timePerChar = 0.05f; // Delay between characters

    private string fullText;

    public AudioSource typeSFX;

    void Start()
    {
        fullText = targetText.text; // Get the initial text
        targetText.text = ""; // Clear the text
        StartCoroutine(TypeText());
    }

    IEnumerator TypeText()
    {
        foreach (char c in fullText)
        {
            targetText.text += c;
            typeSFX.PlayOneShot(typeSFX.clip);
            yield return new WaitForSeconds(timePerChar);
        }
    }
}
