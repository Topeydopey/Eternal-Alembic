// Assets/Scripts/Minigame V2/PhilosophersAlembic/DropSlotAlembic.cs
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DropSlotAlembic : MonoBehaviour, IDropHandler
{
    [Header("Acceptance")]
    [Tooltip("Primary/role tag for this slot. Keep this as 'Runoff' for mouth or 'Result' for take zone.\nUsed by game logic to know which slot was used.")]
    public string acceptsTag = "Runoff";

    [Tooltip("If set, this slot will accept ANY of these tags on drop.\nLeave empty to fall back to only `acceptsTag`.")]
    public string[] acceptsTags = new string[0];

    [Header("Refs")]
    [SerializeField] private PhilosophersAlembicMinigame alembicMinigame; // assign in Inspector (optional)

    private CanvasGroup cachedCg;

    void Awake()
    {
        cachedCg = GetComponent<CanvasGroup>();

        if (!alembicMinigame)
            alembicMinigame = GetComponentInParent<PhilosophersAlembicMinigame>(true);

        if (!alembicMinigame)
            alembicMinigame = FindAnyObjectByType<PhilosophersAlembicMinigame>(FindObjectsInactive.Include);

        if (!alembicMinigame)
            Debug.LogWarning("[DropSlotAlembic] No PhilosophersAlembicMinigame found; will try again on drop.");
    }

    public void Enable(bool on)
    {
        if (cachedCg || TryGetComponent(out cachedCg))
        {
            cachedCg.alpha = on ? 1f : 0.4f;
            cachedCg.interactable = on;
            cachedCg.blocksRaycasts = on;
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

        if (!Accepts(dragged))
        {
            var expect = (acceptsTags != null && acceptsTags.Length > 0)
                ? $"one of [{string.Join(", ", acceptsTags)}]"
                : $"'{acceptsTag}'";
            Debug.Log($"[DropSlotAlembic:{name}] Rejected '{dragged.tag}', expects {expect}.");
            return;
        }

        var mg = alembicMinigame ?? GetComponentInParent<PhilosophersAlembicMinigame>(true);
        if (!mg)
        {
            Debug.LogError("[DropSlotAlembic] No PhilosophersAlembicMinigame found to handle drop.");
            return;
        }

        // Pass through (minigame uses `slot.acceptsTag` to know if it's the mouth or the take zone)
        mg.HandleDropAlembic(this, dragged);
    }

    private bool Accepts(GameObject go)
    {
        // If a multi-tag list is provided, use it.
        if (acceptsTags != null && acceptsTags.Length > 0)
        {
            for (int i = 0; i < acceptsTags.Length; i++)
            {
                var tag = acceptsTags[i];
                if (!string.IsNullOrEmpty(tag) && go.CompareTag(tag))
                    return true;
            }
            return false;
        }

        // Fallback: single role tag
        return go.CompareTag(acceptsTag);
    }
}
