// Assets/Scripts/UI/UIHoverHighlighter.cs
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class UIHoverHighlighter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Sprites")]
    [Tooltip("Your normal sprite. If left null, uses the Image's current sprite.")]
    public Sprite baseSprite;
    [Tooltip("The highlighted (outlined) sprite variant.")]
    public Sprite highlightSprite;

    [Header("Fade")]
    [Range(0f, 1f)] public float maxHighlightAlpha = 1f;
    [Tooltip("Seconds to fade in/out the highlight overlay.")]
    public float fadeDuration = 0.15f;
    [Tooltip("Optional slight scale bump on hover.")]
    public float hoverPopScale = 1.05f;

    [Header("Options")]
    [Tooltip("If true, the base Image's sprite is kept as-is; we add a child overlay that fades in.")]
    public bool useOverlay = true;

    private Image _baseImage;
    private RectTransform _rt;
    private Image _overlayImage;
    private CanvasGroup _overlayCg;
    private Coroutine _fadeCo;
    private bool _hovering;
    private Vector3 _baseScale;

    void Awake()
    {
        _baseImage = GetComponent<Image>();
        _rt = transform as RectTransform;
        _baseScale = _rt.localScale;

        if (!baseSprite) baseSprite = _baseImage ? _baseImage.sprite : null;

        if (useOverlay)
        {
            if (!_overlayImage)
            {
                var go = new GameObject("HighlightOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                var child = go.transform as RectTransform;
                child.SetParent(transform, false);
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
                child.pivot = _rt.pivot;
                child.localScale = Vector3.one;

                _overlayImage = go.GetComponent<Image>();
                _overlayImage.raycastTarget = false; // don’t block pointer
                _overlayImage.preserveAspect = _baseImage ? _baseImage.preserveAspect : true;

                _overlayCg = go.GetComponent<CanvasGroup>();
                _overlayCg.alpha = 0f;
                _overlayCg.blocksRaycasts = false;
                _overlayCg.interactable = false;
            }
            _overlayImage.sprite = highlightSprite ? highlightSprite : baseSprite;
        }
        else
        {
            // Fallback to swapping the base image color alpha if no overlay
            if (!_baseImage) Debug.LogWarning("[UIHoverHighlighter] No Image found.");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        StartFade(entering: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        StartFade(entering: false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // While dragging, kill highlight so it doesn't look odd
        _hovering = false;
        StartFade(entering: false, instant: true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Restore hover if pointer still over us
        if (eventData != null && eventData.pointerEnter == gameObject)
        {
            _hovering = true;
            StartFade(entering: true);
        }
    }

    private void StartFade(bool entering, bool instant = false)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(entering, instant));
    }

    private IEnumerator FadeRoutine(bool entering, bool instant)
    {
        float dur = instant ? 0f : Mathf.Max(0.0001f, fadeDuration);

        // Optional pop scale
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
            // Color-luminance fallback (tint more visible on hover)
            Color cFrom = _baseImage.color;
            Color cTo = entering ? new Color(cFrom.r, cFrom.g, cFrom.b, 1f) : new Color(cFrom.r, cFrom.g, cFrom.b, 0.9f);

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
    // Runtime setter so gameplay can swap the highlight overlay (e.g., empty->filled->result)
    public void SetHighlightSprite(Sprite sprite, bool resetFade = false)
    {
        highlightSprite = sprite;

        if (useOverlay)
        {
            // Make sure overlay exists (Awake builds it)
            if (_overlayImage == null) Awake();
            if (_overlayImage) _overlayImage.sprite = sprite ? sprite : _overlayImage.sprite;
            if (resetFade && _overlayCg) _overlayCg.alpha = 0f;
        }
    }
}
