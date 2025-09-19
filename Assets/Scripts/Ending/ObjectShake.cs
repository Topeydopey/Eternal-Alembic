using UnityEngine;
using System.Collections;

public class ObjectShake : MonoBehaviour
{
    public float duration = 0.5f;   // how long to shake
    public float magnitude = 10f;   // how strong the shake

    public AudioSource rumble;

    private void OnEnable()
    {

        StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        Vector3 originalPos = transform.localPosition;
        rumble.PlayOneShot(rumble.clip);
        
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;

        // disable script after finishing
        this.enabled = false;
    }
}
