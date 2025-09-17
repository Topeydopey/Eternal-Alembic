using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class DropSlotResult : MonoBehaviour, IDropHandler
{
    [Tooltip("Tag the token must have to be accepted.")]
    public string acceptsTag = "Result";

    [Tooltip("Controller that will consume the token and close the UI.")]
    public SnakeWorldMinigame minigame; // set via inspector or auto

    [Header("Debug")]
    public bool verbose = false;

    void Awake()
    {
        if (!minigame) minigame = GetComponentInParent<SnakeWorldMinigame>(true);

        // Ensure there's a raycastable Image so OnDrop can fire on transparent UI.
        var img = GetComponent<Image>();
        if (!img) img = gameObject.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0); // invisible but hit-testable
        img.raycastTarget = true;
    }

    public void OnDrop(PointerEventData e)
    {
        if (minigame == null)
        {
            if (verbose) Debug.LogWarning("[DropSlotResult] No minigame assigned/found.");
            return;
        }

        var dragged = e.pointerDrag;
        if (!dragged)
        {
            if (verbose) Debug.Log("[DropSlotResult] OnDrop with null pointerDrag.");
            return;
        }

        // Climb to the token root in case the child (e.g., Image) is being dragged.
        var di = dragged.GetComponentInParent<DraggableItem>();
        var tokenRoot = di ? di.gameObject : dragged;

        if (!tokenRoot.CompareTag(acceptsTag))
        {
            if (verbose) Debug.Log($"[DropSlotResult] Rejected '{tokenRoot.tag}', expects '{acceptsTag}'.");
            return;
        }

        if (verbose) Debug.Log($"[DropSlotResult] Accepted token '{tokenRoot.name}'.");
        minigame.HandleResultDrop(tokenRoot);
    }
}
