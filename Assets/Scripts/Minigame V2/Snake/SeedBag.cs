using UnityEngine;
using UnityEngine.EventSystems;

public class SeedBag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Prefab: Image + CanvasGroup + DraggableItem; Tag='Seed'")]
    public GameObject seedTokenPrefab;

    [Tooltip("Canvas that contains the minigame UI.")]
    public Canvas canvas;

    // Exposed so drop slots can recover the token if pointerDrag is wrong
    public static GameObject CurrentToken { get; private set; }

    private GameObject spawned;

    void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!seedTokenPrefab || !canvas) return;

        spawned = Instantiate(seedTokenPrefab, canvas.transform);
        spawned.SetActive(true);
        spawned.tag = "Seed"; // make sure

        // place under pointer
        var rt = spawned.GetComponent<RectTransform>();
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            e.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out local
        );
        rt.anchoredPosition = local;

        // seeds should NOT snap back if no drop detected
        var di = spawned.GetComponent<DraggableItem>();
        if (di) di.snapBackIfNoDrop = false;

        // IMPORTANT: redirect this drag to the token
        e.pointerDrag = spawned;
        e.pointerPress = spawned;
        e.rawPointerPress = spawned;

        // store for safety fallback
        CurrentToken = spawned;

        // kick off drag on the spawned token
        ExecuteEvents.Execute<IBeginDragHandler>(spawned, e, ExecuteEvents.beginDragHandler);
    }

    public void OnDrag(PointerEventData e)
    {
        // keep the event pointed at the token while dragging
        if (spawned)
        {
            e.pointerDrag = spawned;
            ExecuteEvents.Execute<IDragHandler>(spawned, e, ExecuteEvents.dragHandler);
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (spawned)
        {
            e.pointerDrag = spawned;
            ExecuteEvents.Execute<IEndDragHandler>(spawned, e, ExecuteEvents.endDragHandler);
        }

        spawned = null;
        CurrentToken = null;
    }
}
