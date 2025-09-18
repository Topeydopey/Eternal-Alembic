// Assets/Scripts/Minigame V2/PhilosophersAlembic/DropSlotAlembic.cs
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DropSlotAlembic : MonoBehaviour, IDropHandler
{
    [Tooltip("Tag accepted by this slot. Use 'Runoff' for mouth or 'Result' for take zone.")]
    public string acceptsTag = "Runoff";

    [SerializeField] private PhilosophersAlembicMinigame alembicMinigame; // assign in Inspector (optional)

    void Awake()
    {
        if (!alembicMinigame)
            alembicMinigame = GetComponentInParent<PhilosophersAlembicMinigame>(true);

        if (!alembicMinigame)
            alembicMinigame = FindAnyObjectByType<PhilosophersAlembicMinigame>(FindObjectsInactive.Include);

        if (!alembicMinigame)
            Debug.LogWarning("[DropSlotAlembic] No PhilosophersAlembicMinigame found; will try again on drop.");
    }

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
            Debug.Log($"[DropSlotAlembic:{name}] Rejected '{dragged.tag}', expects '{acceptsTag}'.");
            return;
        }

        var mg = alembicMinigame ?? GetComponentInParent<PhilosophersAlembicMinigame>(true);
        if (!mg)
        {
            Debug.LogError("[DropSlotAlembic] No PhilosophersAlembicMinigame found to handle drop.");
            return;
        }

        // Call the uniquely named handler to avoid overload ambiguity
        mg.HandleDropAlembic(this, dragged);
    }
}
