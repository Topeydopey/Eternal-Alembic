// SeedDropSlot.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SeedDropSlot : MonoBehaviour, IDropHandler
{
    [Tooltip("Tag this slot accepts (your seed token).")]
    public string acceptsTag = "Seed";

    [Header("Targets")]
    [SerializeField] private SnakeStationMinigame minigame;   // auto-found if null
    [SerializeField] private RectTransform targetPlayArea;    // usually minigame.playArea
    [SerializeField] private Canvas canvas;                   // auto-found if null

    [Header("Visual (optional)")]
    public GameObject seedMarkerPrefab;  // small Image to show a dropped seed

    [Header("Debug")]
    public bool verbose = false;

    void Awake()
    {
        if (!minigame) minigame = GetComponentInParent<SnakeStationMinigame>(true);
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!targetPlayArea && minigame) targetPlayArea = minigame.playArea;

        // Make sure this UI can receive drops.
        var img = GetComponent<Image>();
        if (!img)
        {
            img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // invisible, but raycastable
        }
        img.raycastTarget = true;
    }

    public void OnDrop(PointerEventData e)
    {
        var dragged = e.pointerDrag;
        if (!dragged)
        {
            if (verbose) Debug.Log("[SeedDropSlot] OnDrop but dragged=null");
            return;
        }

        if (!dragged.CompareTag(acceptsTag))
        {
            if (verbose) Debug.Log($"[SeedDropSlot] Rejected '{dragged.tag}', expects '{acceptsTag}'");
            return;
        }

        if (!minigame)
        {
            minigame = GetComponentInParent<SnakeStationMinigame>(true);
            if (!minigame) { Debug.LogError("[SeedDropSlot] No SnakeStationMinigame found."); return; }
        }

        if (!targetPlayArea) targetPlayArea = minigame.playArea;
        if (!targetPlayArea) { Debug.LogError("[SeedDropSlot] targetPlayArea not set."); return; }

        // Convert screen -> playArea local (anchored) position
        var cam = minigame.CanvasCamera; // correct camera for ScreenSpace-Camera/World
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetPlayArea, e.position, cam, out local);

        if (verbose) Debug.Log($"[SeedDropSlot] Drop local={local}");

        // Consume the dragged seed token so it doesn't snap back
        var di = dragged.GetComponent<DraggableItem>();
        if (di) di.Consume(); else dragged.SetActive(false);

        // Optional marker at drop location (as child of playArea)
        GameObject marker = null;
        if (seedMarkerPrefab)
        {
            marker = Instantiate(seedMarkerPrefab, targetPlayArea);
            var mrt = marker.GetComponent<RectTransform>() ?? marker.AddComponent<RectTransform>();
            mrt.anchoredPosition = local;
            marker.SetActive(true);
        }

        // Tell the snake to eat it
        minigame.EnqueueSeed(local, marker);
    }
}
