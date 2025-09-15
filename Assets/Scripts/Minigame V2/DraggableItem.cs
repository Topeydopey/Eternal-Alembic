using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Canvas canvas;

    private RectTransform rt;
    private CanvasGroup cg;
    private Transform originalParent;
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
        if (consumed) return;
        originalParent = transform.parent;
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();
        cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (consumed) return;

        Vector2 localPoint;
        RectTransform canvasRT = canvas.transform as RectTransform;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, cam, out localPoint))
            rt.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (consumed) return; // Already eaten by a slot
        cg.blocksRaycasts = true;

        // If nothing handled the drop, snap back
        if (transform.parent == canvas.transform)
        {
            transform.SetParent(originalParent, true);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    public void Consume()
    {
        consumed = true;
        cg.blocksRaycasts = true;
        gameObject.SetActive(false); // hide sprite immediately
    }
}
