// Assets/Scripts/Utility/SceneIntro.cs
using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SceneIntro : MonoBehaviour
{
    public static event Action OnLevelIntro; // listeners fire when the intro starts (configurable below)

    [Header("Fade")]
    [SerializeField] private bool fadeInOnStart = true;
    [SerializeField] private float holdBlack = 0.0f;   // seconds to hold black before fade
    [SerializeField] private float fadeInDuration = 0.6f;

    [Header("When to trigger listeners")]
    [Tooltip("Fire OnLevelIntro BEFORE the fade begins (screen still black). If false, fires after fade completes.")]
    [SerializeField] private bool triggerBeforeFade = true;

    private IEnumerator Start()
    {
        if (!ScreenFader.Instance) ScreenFader.CreateDefault();

        if (fadeInOnStart)
        {
            ScreenFader.Instance.SetAlpha(1f);
            if (holdBlack > 0f) yield return new WaitForSeconds(holdBlack);

            if (triggerBeforeFade) OnLevelIntro?.Invoke();

            yield return ScreenFader.Instance.FadeIn(fadeInDuration);
            if (!triggerBeforeFade) OnLevelIntro?.Invoke();
        }
        else
        {
            OnLevelIntro?.Invoke();
        }
    }
}
