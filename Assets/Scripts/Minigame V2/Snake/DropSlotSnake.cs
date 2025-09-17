using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DropSlotSnake : MonoBehaviour, IDropHandler
{
    [Tooltip("Tag accepted by this slot (use 'Result').")]
    public string acceptsTag = "Result";

    [SerializeField] private SnakeStationMinigame stationMinigame; // assign in Inspector (optional)

    void Awake()
    {
        if (!stationMinigame)
            stationMinigame = GetComponentInParent<SnakeStationMinigame>(true);

        if (!stationMinigame)
            Debug.LogWarning("[DropSlotSnake] No SnakeStationMinigame found in parents; will try again on drop.");
    }

    /// <summary>Enable/disable the drop zone (and raycast) nicely.</summary>
    public void Enable(bool on)
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.alpha = on ? 1f : 0.4f;
            cg.interactable = on;
            cg.blocksRaycasts = on;
        }
        else
        {
            gameObject.SetActive(on);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag;
        if (!dragged) return;

        if (!dragged.CompareTag(acceptsTag))
        {
            Debug.Log($"[DropSlotSnake:{name}] Rejected '{dragged.tag}', expects '{acceptsTag}'.");
            return;
        }

        var mg = stationMinigame ?? GetComponentInParent<SnakeStationMinigame>(true);
        if (!mg)
        {
            Debug.LogError("[DropSlotSnake] No SnakeStationMinigame found to handle the drop.");
            return;
        }

        mg.HandleResultDrop(this, dragged);
    }
}
