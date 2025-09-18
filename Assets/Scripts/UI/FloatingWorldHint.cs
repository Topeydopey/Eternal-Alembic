// Assets/Scripts/UI/FloatingWorldHint.cs
// Unity 6.x • TextMeshPro
// World-space TMP text that fades in/out above a target. Safe to call while inactive.

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FloatingWorldHint : MonoBehaviour
{
    // ---------- Static helpers ----------
    public static void Show(
        Transform target,
        string message,
        Vector3 localOffset,
        float fadeInSeconds = 0.12f,
        float holdSeconds = 1.25f,
        float fadeOutSeconds = 0.25f)
    {
        if (!target) return;
        var inst = GetOrCreateOn(target);
        inst.Display(message, localOffset, fadeInSeconds, holdSeconds, fadeOutSeconds);
    }

    public static FloatingWorldHint GetOrCreateOn(Transform target)
    {
        var existing = target.GetComponentInChildren<FloatingWorldHint>(true);
        if (existing) return existing;

        var root = new GameObject("Floating Text Controller", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(target, false);
        rt.localPosition = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        var hint = root.AddComponent<FloatingWorldHint>();
        hint.BuildUI();  // creates Canvas (World Space) + CanvasGroup + TMP if needed
        return hint;
    }

    // ---------- Instance fields ----------
    [Header("Assign")]
    [SerializeField] private TMP_Text tmp;           // TextMeshProUGUI child
    [SerializeField] private Canvas worldCanvas;     // root (World Space)
    [SerializeField] private CanvasGroup canvasGroup;// root

    [Header("Behaviour")]
    [Tooltip("Disable GameObject after fade-out.")]
    [SerializeField] private bool disableWhenHidden = true;
    [Tooltip("Face the camera each LateUpdate.")]
    [SerializeField] private bool billboardToCamera = true;

    [Header("World-Space Canvas")]
    [SerializeField] private float worldScale = 0.01f;
    [SerializeField] private int sortingOrder = 2000;

    [Header("TMP Defaults")]
    [SerializeField] private int defaultFontSize = 36;
    [SerializeField] private bool enableAutoSize = true;
    [SerializeField] private int autoSizeMin = 18;
    [SerializeField] private int autoSizeMax = 60;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;

    private RectTransform _rt;
    private Coroutine _fadeCo;
    private Camera _cam;

    // ---------- Unity ----------
    void Awake()
    {
        _rt = transform as RectTransform;
        if (!tmp || !worldCanvas || !canvasGroup) BuildUI();
        // Start hidden but DO NOT deactivate here
        SetAlpha(0f);
    }

    void OnEnable()
    {
        // If someone re-enabled us, ensure hidden alpha but stay active
        SetAlpha(0f);
    }

    void LateUpdate()
    {
        if (!billboardToCamera) return;
        if (!_cam) _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (_cam)
            transform.rotation = Quaternion.LookRotation(_cam.transform.forward, _cam.transform.up);
    }

    // ---------- API ----------
    public void Display(string message, Vector3 localOffset, float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
    {
        if (!tmp || !canvasGroup) BuildUI();

        // Ensure we are active BEFORE starting coroutines to avoid Unity errors
        if (disableWhenHidden && !gameObject.activeSelf)
            gameObject.SetActive(true);

        _rt.localPosition = localOffset;
        tmp.text = message;

        // Prepare for fade
        transform.SetAsLastSibling();
        SetAlpha(0f);

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(fadeInSeconds, holdSeconds, fadeOutSeconds));
    }

    // ---------- Internals ----------
    private IEnumerator FadeRoutine(float fadeIn, float hold, float fadeOut)
    {
        // Fade in
        float t = 0f, d = Mathf.Max(0.0001f, fadeIn);
        while (t < d) { t += Time.unscaledDeltaTime; SetAlpha(t / d); yield return null; }
        SetAlpha(1f);

        // Hold
        t = 0f; d = Mathf.Max(0f, hold);
        while (t < d) { t += Time.unscaledDeltaTime; yield return null; }

        // Fade out
        t = 0f; d = Mathf.Max(0.0001f, fadeOut);
        while (t < d) { t += Time.unscaledDeltaTime; SetAlpha(1f - (t / d)); yield return null; }
        SetAlpha(0f);

        if (disableWhenHidden) gameObject.SetActive(false);
        _fadeCo = null;
    }

    private void SetAlpha(float a)
    {
        if (canvasGroup) { canvasGroup.alpha = a; return; }
        if (tmp) tmp.alpha = a; // fallback
    }

    private void BuildUI()
    {
        // Root Canvas (World Space)
        worldCanvas = GetComponent<Canvas>();
        if (!worldCanvas)
        {
            worldCanvas = gameObject.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
        }
        worldCanvas.sortingOrder = sortingOrder;

        var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        transform.localScale = Vector3.one * worldScale;

        // CanvasGroup on root
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        // TMP child
        if (!tmp)
        {
            var tgo = new GameObject("HintTMP", typeof(RectTransform));
            var tr = tgo.GetComponent<RectTransform>();
            tr.SetParent(transform, false);
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(600f, 200f);
            tmp = tgo.AddComponent<TextMeshProUGUI>();
        }

        // Configure TMP
        tmp.text = "";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = enableAutoSize;
        tmp.fontSize = defaultFontSize;
        if (enableAutoSize) { tmp.fontSizeMin = autoSizeMin; tmp.fontSizeMax = autoSizeMax; }
        tmp.outlineWidth = outlineWidth;
        tmp.outlineColor = outlineColor;
    }
}
