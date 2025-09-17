using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Canvas that contains this draggable. If null, found at runtime.")]
    [SerializeField] private Canvas canvas;

    [Tooltip("If true, the item returns to its original parent/position when no DropSlot handled the drop.")]
    public bool snapBackIfNoDrop = true;

    private RectTransform rt;
    private CanvasGroup cg;
    private Transform originalParent;
    private Vector2 originalAnchoredPos;
    private bool consumed;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        if (!canvas) canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (consumed || canvas == null) return;

        originalParent = transform.parent;
        originalAnchoredPos = rt.anchoredPosition;

        // bring to top within the same canvas
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        // allow raycasts to pass through while dragging so DropSlots can receive
        cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (consumed || canvas == null) return;

        Vector2 localPoint;
        RectTransform canvasRT = canvas.transform as RectTransform;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, cam, out localPoint))
            rt.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (consumed) return; // already handled by a slot
        cg.blocksRaycasts = true;

        // If no slot reparented us (still under canvas root), optionally snap back
        if (transform.parent == canvas.transform && snapBackIfNoDrop && originalParent)
        {
            transform.SetParent(originalParent, true);
            rt.anchoredPosition = originalAnchoredPos;
        }
    }

    /// <summary>Hide the UI and prevent snap-back logic on this drag end.</summary>
    public void Consume()
    {
        consumed = true;
        cg.blocksRaycasts = true;
        gameObject.SetActive(false);
    }
}
