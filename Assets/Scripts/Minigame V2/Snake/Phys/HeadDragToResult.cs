// HeadDragToResult.cs
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class HeadDragToResult : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Enable/State")]
    public bool canTake = false; // set true when snake completes

    [Header("UI Result Token")]
    public GameObject resultTokenPrefab;  // Image + CanvasGroup + DraggableItem; Tag="Result"
    public Canvas canvas;                 // minigame canvas to spawn into

    private GameObject spawned;

    void Awake()
    {
        if (!canvas) canvas = FindFirstObjectByType<Canvas>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!canTake || !resultTokenPrefab || !canvas) return;

        spawned = Instantiate(resultTokenPrefab, canvas.transform);
        spawned.SetActive(true);
        spawned.tag = "Result";

        // place under pointer
        var rt = spawned.GetComponent<RectTransform>();
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, e.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out local);
        rt.anchoredPosition = local;

        // redirect the drag to the token so user is now dragging UI
        e.pointerDrag = spawned;
        e.pointerPress = spawned;
        e.rawPointerPress = spawned;

        // kick off drag on the token
        ExecuteEvents.Execute<IBeginDragHandler>(spawned, e, ExecuteEvents.beginDragHandler);
    }

    public void OnDrag(PointerEventData e)
    {
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
            spawned = null;
        }
    }
}
