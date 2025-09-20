// Assets/Scripts/Utility/ScreenFader.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
    [SerializeField] private Camera uiCamera;  // if ScreenSpaceCamera
    [SerializeField] private int sortingOrder = 50000;

    [Header("Look")]
    [SerializeField] private Color color = Color.black;
    [SerializeField] private float defaultDuration = 0.6f;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Safety")]
    [Tooltip("When true, the fader resets to transparent whenever a new scene loads, unless CarryNextSceneFade is true.")]
    [SerializeField] private bool autoClearOnSceneLoaded = true;

    public static bool CarryNextSceneFade = false;   // opt-out for one scene load (set by code doing a deliberate cross-scene fade)

    private Canvas canvas;
    private CanvasGroup cg;
    private Image image;
    private Coroutine fadeCo;

    public float CurrentAlpha => cg ? cg.alpha : 0f;
    public bool IsFading => fadeCo != null;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = renderMode;
        canvas.sortingOrder = sortingOrder;
        canvas.worldCamera = (renderMode == RenderMode.ScreenSpaceCamera) ? uiCamera : null;

        if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();

        cg = GetComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.alpha = 0f;

        // Fullscreen Image
        if (!image)
        {
            var go = new GameObject("Fader", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            image = go.GetComponent<Image>();
        }
        image.color = color;
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset alpha unless a cross-scene fade is explicitly being carried
        if (!autoClearOnSceneLoaded) return;

        if (CarryNextSceneFade)
        {
            // Use the carried black once, then clear the flag so subsequent loads auto-clear
            CarryNextSceneFade = false;
            return;
        }

        // Safety clear
        SetAlpha(0f);
    }

    public static ScreenFader CreateDefault()
    {
        if (Instance) return Instance;
        var go = new GameObject("ScreenFader", typeof(Canvas), typeof(CanvasGroup), typeof(ScreenFader));
        return go.GetComponent<ScreenFader>();
    }

    public void SetAlpha(float a)
    {
        if (!cg) cg = GetComponent<CanvasGroup>();
        cg.alpha = a;
        cg.blocksRaycasts = a > 0.001f;
    }

    public Coroutine FadeIn(float duration = -1f) => Fade(1f, 0f, duration < 0 ? defaultDuration : duration);
    public Coroutine FadeOut(float duration = -1f) => Fade(0f, 1f, duration < 0 ? defaultDuration : duration);

    private Coroutine Fade(float from, float to, float dur)
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeRoutine(from, to, Mathf.Max(0.0001f, dur)));
        return fadeCo;
    }

    private IEnumerator FadeRoutine(float from, float to, float dur)
    {
        SetAlpha(from);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // unaffected by timescale
            SetAlpha(Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        SetAlpha(to);
        fadeCo = null;
    }
}
