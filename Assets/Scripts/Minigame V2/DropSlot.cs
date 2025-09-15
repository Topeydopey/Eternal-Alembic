using UnityEngine;
using UnityEngine.EventSystems;

public class DropSlot : MonoBehaviour, IDropHandler
{
    [Tooltip("What tag this slot accepts, e.g. 'Potion' or 'Plant'")]
    public string acceptsTag;

    [SerializeField] private MortarPestleMinigame minigame;

    void Awake()
    {
        if (!minigame) minigame = GetComponentInParent<MortarPestleMinigame>();
        if (!minigame) minigame = FindAnyObjectByType<MortarPestleMinigame>(FindObjectsInactive.Include);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag;
        if (!dragged) return;

        if (dragged.CompareTag(acceptsTag))
        {
            minigame?.HandleDrop(this, dragged);
        }
        else
        {
            Debug.Log($"[DropSlot:{name}] Rejected '{dragged.tag}', expects '{acceptsTag}'.");
        }
    }
}
