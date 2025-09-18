// Assets/Scripts/UI/UIHoverHighlighter.cs
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class UIHoverHighlighter : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Sprites")]
    public Sprite baseSprite;
    public Sprite highlightSprite;

    [Header("Per-Phase Highlight (optional)")]
    [Tooltip("Order matches phases: 0..N-1")]
    public Sprite[] highlightByPhase;
    public bool usePerPhaseHighlight = true;

    [Header("Fade")]
    [Range(0f, 1f)] public float maxHighlightAlpha = 1f;
    public float fadeDuration = 0.15f;
    public float hoverPopScale = 1.05f;

    [Header("Options")]
    public bool useOverlay = true;

    [Header("Robustness")]
    [Tooltip("If true, we re-sample the current localScale as the baseline each time hover/drag begins.")]
    public bool recalcBaseScaleOnPointerEnter = true;

    private Image _baseImage;
    private RectTransform _rt;
    private Image _overlayImage;
    private CanvasGroup _overlayCg;
    private Coroutine _fadeCo;
    private Vector3 _baseScale;
    private int _phaseIndex;

    void Awake()
    {
        _baseImage = GetComponent<Image>();
        _rt = (RectTransform)transform;
        _baseScale = _rt.localScale;
        if (!baseSprite && _baseImage) baseSprite = _baseImage.sprite;

        if (useOverlay) { EnsureOverlay(); SetOverlaySpriteForPhase(); }
        else if (!_baseImage) Debug.LogWarning("[UIHoverHighlighter] No Image found.");
    }

    void OnEnable()
    {
        // If parent scale changed while disabled, make sure baseline is sane.
        _baseScale = _rt ? _rt.localScale : Vector3.one;
    }

    // ---------- Public API ----------
    public void SetPhaseIndex(int phaseIndex, bool resetFade = true)
    {
        _phaseIndex = Mathf.Max(0, phaseIndex);
        if (useOverlay)
        {
            EnsureOverlay();
            SetOverlaySpriteForPhase();
            if (resetFade && _overlayCg) _overlayCg.alpha = 0f;
        }
    }

    public void SetHighlightSprite(Sprite sprite, bool resetFade = false)
    {
        highlightSprite = sprite;
        if (useOverlay)
        {
            EnsureOverlay();
            if (_overlayImage) _overlayImage.sprite = sprite ? sprite : _overlayImage.sprite;
            if (resetFade && _overlayCg) _overlayCg.alpha = 0f;
        }
    }

    /// Call this AFTER you finish sizing/scaling at spawn.
    public void SetBaseScaleToCurrent()
    {
        if (!_rt) _rt = (RectTransform)transform;
        _baseScale = _rt.localScale;
    }

    // ---------- Pointer hooks ----------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (recalcBaseScaleOnPointerEnter) _baseScale = _rt.localScale;
        StartFade(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StartFade(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (recalcBaseScaleOnPointerEnter) _baseScale = _rt.localScale;
        StartFade(false, instant: true); // hide highlight while dragging
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // If pointer is still over this object, resum ehighlight from the *current* baseline.
        if (eventData != null && eventData.pointerEnter == gameObject)
        {
            if (recalcBaseScaleOnPointerEnter) _baseScale = _rt.localScale;
            StartFade(true);
        }
    }

    // ---------- Internals ----------
    private void StartFade(bool entering, bool instant = false)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(entering, instant));
    }

    private IEnumerator FadeRoutine(bool entering, bool instant)
    {
        float dur = instant ? 0f : Mathf.Max(0.0001f, fadeDuration);

        var fromScale = _rt.localScale;
        var toScale = entering ? (_baseScale * hoverPopScale) : _baseScale;

        if (useOverlay && _overlayCg)
        {
            float from = _overlayCg.alpha;
            float to = entering ? maxHighlightAlpha : 0f;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                _overlayCg.alpha = Mathf.Lerp(from, to, k);
                _rt.localScale = Vector3.Lerp(fromScale, toScale, k);
                yield return null;
            }
            _overlayCg.alpha = to;
        }
        else if (_baseImage)
        {
            Color cFrom = _baseImage.color;
            Color cTo = entering ? new Color(cFrom.r, cFrom.g, cFrom.b, 1f)
                                 : new Color(cFrom.r, cFrom.g, cFrom.b, 0.9f);

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                _baseImage.color = Color.Lerp(cFrom, cTo, k);
                _rt.localScale = Vector3.Lerp(fromScale, toScale, k);
                yield return null;
            }
            _baseImage.color = cTo;
        }

        _rt.localScale = toScale;
        _fadeCo = null;
    }

    private void EnsureOverlay()
    {
        if (_overlayImage) return;

        var go = new GameObject("HighlightOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var child = (RectTransform)go.transform;
        child.SetParent(transform, false);
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.one;
        child.offsetMin = Vector2.zero;
        child.offsetMax = Vector2.zero;
        child.pivot = _rt.pivot;
        child.localScale = Vector3.one;

        _overlayImage = go.GetComponent<Image>();
        _overlayImage.raycastTarget = false;
        _overlayImage.preserveAspect = _baseImage ? _baseImage.preserveAspect : true;

        _overlayCg = go.GetComponent<CanvasGroup>();
        _overlayCg.alpha = 0f;
        _overlayCg.blocksRaycasts = false;
        _overlayCg.interactable = false;
    }

    private void SetOverlaySpriteForPhase()
    {
        if (!_overlayImage) return;
        Sprite s = (usePerPhaseHighlight && highlightByPhase != null && _phaseIndex < highlightByPhase.Length)
                     ? highlightByPhase[Mathf.Clamp(_phaseIndex, 0, highlightByPhase.Length - 1)]
                     : null;
        _overlayImage.sprite = s ? s : (highlightSprite ? highlightSprite : baseSprite);
    }
}
