// UIDropZone.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIDropZone : MonoBehaviour, IDropHandler
{
    public string zoneId;                     // e.g., "Mortar", "OffTable"
    public List<string> acceptsDragIds;       // which dragIds are allowed

    private static readonly List<UIDropZone> zones = new();

    private void OnEnable() { zones.Add(this); }
    private void OnDisable() { zones.Remove(this); }

    public static bool TryNotifyDrop(PointerEventData e, UIDragItem item)
    {
        foreach (var z in zones)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(z.transform as RectTransform, e.position, z.GetComponentInParent<Canvas>()?.worldCamera))
            {
                if (z.acceptsDragIds == null || z.acceptsDragIds.Count == 0 || z.acceptsDragIds.Contains(item.dragId))
                {
                    (z as IDropHandler).OnDrop(new PointerEventData(EventSystem.current) { pointerDrag = item.gameObject });
                    return true;
                }
            }
        }
        return false;
    }

    public virtual void OnDrop(PointerEventData eventData) { }
}
