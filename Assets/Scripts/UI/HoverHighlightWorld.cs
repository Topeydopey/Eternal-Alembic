// Assets/Scripts/World/WorldSpriteHighlighter2D.cs
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class WorldSpriteHighlighter2D : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Base sprite; if null, uses the SpriteRenderer's current sprite.")]
    [SerializeField] private Sprite normalSprite;
    [Tooltip("Outlined/selected sprite to fade in on hover. If null, reuses normal.")]
    [SerializeField] private Sprite highlightSprite;

    [Header("Fade")]
    [Range(0f, 1f)] public float maxHighlightAlpha = 1f;
    [Tooltip("Alpha change per second.")]
    public float fadeSpeed = 10f;

    [Header("Raycast")]
    [Tooltip("Camera used to convert mouse to world. If null, uses Camera.main.")]
    public Camera worldCamera;
    [Tooltip("If true, do not highlight when the pointer is over UI.")]
    public bool blockWhenPointerOverUI = true;
    [Tooltip("Optional filter for which layers should count as hover. Leave empty = no filter.")]
    public LayerMask hoverMask = ~0; // all layers by default

    [Header("Sorting")]
    [Tooltip("Add to base sorting order for overlay so it renders on top.")]
    public int overlayOrderOffset = 1;

    [Header("Collider Sync (optional)")]
    [Tooltip("If true and there is a BoxCollider2D, auto-size it to match the SpriteRenderer (supports Tiled/Sliced).")]
    public bool autoSyncBoxCollider = true;

    private SpriteRenderer _baseSr;
    private SpriteRenderer _overlaySr;
    private Collider2D _col;
    private float _targetAlpha;
    private float _currentAlpha;

    // cache for change detection
    private Vector2 _lastSrSize;
    private SpriteDrawMode _lastDrawMode;
    private SpriteTileMode _lastTileMode;
    private bool _lastFlipX, _lastFlipY;

    void Awake()
    {
        _baseSr = GetComponent<SpriteRenderer>();
        _col = GetComponent<Collider2D>();
        if (!worldCamera) worldCamera = Camera.main;

        if (!normalSprite) normalSprite = _baseSr.sprite;
        if (!highlightSprite) highlightSprite = normalSprite;

        // Build overlay child SR that mirrors the base SR
        var go = new GameObject("HighlightOverlay_SR");
        go.transform.SetParent(transform, false);
        _overlaySr = go.AddComponent<SpriteRenderer>();
        _overlaySr.sprite = highlightSprite ? highlightSprite : normalSprite;
        MirrorSpriteRendererSettings();

        _overlaySr.color = new Color(1, 1, 1, 0f); // start hidden

        _currentAlpha = 0f;
        _targetAlpha = 0f;

        TrySyncBoxCollider();
    }

    void Update()
    {
        if (!worldCamera) worldCamera = Camera.main;
        if (!_col || !_overlaySr) return;

        // Keep overlay in sync in case someone edits SR at runtime (size/flip/tile)
        if (BaseRendererChanged())
        {
            MirrorSpriteRendererSettings();
            TrySyncBoxCollider();
        }

        // 1) Mouse world point
        Vector3 screen;
#if ENABLE_INPUT_SYSTEM
        screen = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : (Vector3)Input.mousePosition;
#else
        screen = Input.mousePosition;
#endif

        if (worldCamera == null) return;
        var wp2 = (Vector2)worldCamera.ScreenToWorldPoint(screen);

        // 2) UI block?
        if (blockWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            _targetAlpha = 0f;
        }
        else
        {
            // 3) Hover test against OUR collider
            bool hitUs = _col.OverlapPoint(wp2);

            // Optional layer filter
            if (hitUs && hoverMask != (hoverMask | (1 << gameObject.layer)))
                hitUs = false;

            _targetAlpha = hitUs ? maxHighlightAlpha : 0f;
        }

        // 4) Smooth fade
        if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            var c = _overlaySr.color; c.a = _currentAlpha; _overlaySr.color = c;
        }
    }

    void OnDisable()
    {
        if (_overlaySr)
        {
            var c = _overlaySr.color; c.a = 0f; _overlaySr.color = c;
        }
        _currentAlpha = 0f;
        _targetAlpha = 0f;
    }

    /// <summary>Call this if you change the base/outline sprites at runtime.</summary>
    public void SetSprites(Sprite baseSprite, Sprite highlight)
    {
        if (baseSprite) { normalSprite = baseSprite; if (_baseSr) _baseSr.sprite = baseSprite; }
        if (highlight) { highlightSprite = highlight; if (_overlaySr) _overlaySr.sprite = highlight; }
        MirrorSpriteRendererSettings();
        TrySyncBoxCollider();
    }

    // ---------- internals ----------

    private void MirrorSpriteRendererSettings()
    {
        // Sorting & material
        _overlaySr.sortingLayerID = _baseSr.sortingLayerID;
        _overlaySr.sortingOrder = _baseSr.sortingOrder + overlayOrderOffset;
        _overlaySr.sharedMaterial = _baseSr.sharedMaterial;

        // Draw mode / size (handles Tiled/Sliced)
        _overlaySr.drawMode = _baseSr.drawMode;
        if (_baseSr.drawMode != SpriteDrawMode.Simple)
        {
            _overlaySr.size = _baseSr.size;
            _overlaySr.tileMode = _baseSr.tileMode;
        }

        // Flips / mask interaction / etc.
        _overlaySr.flipX = _baseSr.flipX;
        _overlaySr.flipY = _baseSr.flipY;
        _overlaySr.maskInteraction = _baseSr.maskInteraction;

        // Cache for change detection
        _lastSrSize = _baseSr.drawMode == SpriteDrawMode.Simple ? Vector2.zero : _baseSr.size;
        _lastDrawMode = _baseSr.drawMode;
        _lastTileMode = _baseSr.tileMode;
        _lastFlipX = _baseSr.flipX;
        _lastFlipY = _baseSr.flipY;
    }

    private bool BaseRendererChanged()
    {
        if (_lastDrawMode != _baseSr.drawMode) return true;
        if (_lastTileMode != _baseSr.tileMode) return true;
        if (_lastFlipX != _baseSr.flipX) return true;
        if (_lastFlipY != _baseSr.flipY) return true;
        if (_baseSr.drawMode != SpriteDrawMode.Simple && _lastSrSize != _baseSr.size) return true;
        return false;
    }

    private void TrySyncBoxCollider()
    {
        if (!autoSyncBoxCollider) return;

        var box = GetComponent<BoxCollider2D>();
        if (!box) return;

        if (_baseSr.drawMode != SpriteDrawMode.Simple)
        {
            // Tiled / Sliced: size is explicit in renderer space
            box.size = _baseSr.size;
            box.offset = Vector2.zero; // SR pivots are in the mesh; table likely centered
        }
        else if (_baseSr.sprite)
        {
            // Simple: use sprite bounds in local space
            var b = _baseSr.sprite.bounds; // local units
            box.size = b.size;
            box.offset = b.center;
        }
    }
}
