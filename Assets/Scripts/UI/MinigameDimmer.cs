// Assets/Scripts/UI/MinigameDimmer.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))]
public class MinigameDimmer : MonoBehaviour
{
    [Range(0f, 1f)] public float targetAlpha = 0.6f;
    public float fadeTime = 0.25f;

    private CanvasGroup _cg;
    private Image _img;
    private Coroutine _fadeCo;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        _img = GetComponent<Image>();
        // Make sure it blocks input when visible
        _img.raycastTarget = true;
        _cg.alpha = 0f;
        _cg.blocksRaycasts = false;
        _cg.interactable = false;
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        StartFade(to: targetAlpha, blockRaycasts: true);
    }

    public void Hide()
    {
        StartFade(to: 0f, blockRaycasts: false, deactivateOnEnd: true);
    }

    private void StartFade(float to, bool blockRaycasts, bool deactivateOnEnd = false)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(to, blockRaycasts, deactivateOnEnd));
    }

    private IEnumerator FadeRoutine(float to, bool blockRaycasts, bool deactivateOnEnd)
    {
        float from = _cg.alpha;
        float t = 0f;
        _cg.blocksRaycasts = blockRaycasts;
        _cg.interactable = blockRaycasts;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeTime));
            yield return null;
        }
        _cg.alpha = to;
        if (!blockRaycasts) { _cg.blocksRaycasts = false; _cg.interactable = false; }
        if (deactivateOnEnd) gameObject.SetActive(false);
        _fadeCo = null;
    }
}
