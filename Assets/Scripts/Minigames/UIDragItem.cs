// UIDragItem.cs
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UIDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string dragId; // e.g., "Pestle"
    public Canvas canvas;

    private RectTransform rt;
    private Vector2 startPos;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (!canvas) canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        startPos = rt.anchoredPosition;
    }

    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, e.position, canvas.worldCamera, out var local);
        rt.anchoredPosition = local;
    }

    public void OnEndDrag(PointerEventData e)
    {
        // Let drop zones handle it; if nothing consumed, snap back
        if (!UIDropZone.TryNotifyDrop(e, this))
            rt.anchoredPosition = startPos;
    }

    public void SnapTo(Vector2 anchoredPos) => rt.anchoredPosition = anchoredPos;
    public Vector2 StartPos => startPos;
    public RectTransform Rect => rt;
}
