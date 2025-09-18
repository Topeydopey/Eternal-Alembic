// Assets/Scripts/Minigame V2/PhilosophersAlembic/RunoffBeakerSprites.cs
// Unity 6.2 • Universal 2D • Input System
// Beaker visuals + drag only. No position resets here (minigame controls placement).
// Now updates UIHoverHighlighter per phase for hover-outline variants.

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class RunoffBeakerSprites : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image beakerImage;
    [SerializeField] private Image liquidImage; // optional tint overlay

    [Header("Sprites by Phase (0..N-1)")]
    [SerializeField] private Sprite[] beakerBase;     // idle/empty
    [SerializeField] private Sprite[] beakerFull;     // optional
    [SerializeField] private Sprite[] beakerSelected; // ready-to-drag look

    [Header("Drag")]
    [SerializeField] private DraggableItem draggable;

    [Header("Optional: 'pop' when full")]
    [SerializeField] private Animator beakerAnimator;
    [SerializeField] private string showFullTrigger = "ShowFull";
    [SerializeField] private string phaseIntParam = "Phase";

    [Header("Hover Highlight (optional)")]
    [Tooltip("If present, this will receive the current phase index so it can swap highlight sprites.")]
    [SerializeField] private UIHoverHighlighter hoverHighlighter;
    [Tooltip("Reset highlight fade when the phase changes.")]
    [SerializeField] private bool resetHighlightFadeOnPhaseChange = true;

    private PhilosophersAlembicMinigame owner;
    private RectTransform rt;
    private bool ready;
    private bool collectible;

    // --- Init ----------------------------------------------------------------

    public void Init(PhilosophersAlembicMinigame owner)
    {
        this.owner = owner;
        rt = (RectTransform)transform;

        if (!beakerImage) beakerImage = GetComponent<Image>();
        if (!draggable) draggable = GetComponent<DraggableItem>();
        if (!hoverHighlighter) hoverHighlighter = GetComponent<UIHoverHighlighter>();
        if (!TryGetComponent(out CanvasGroup _)) gameObject.AddComponent<CanvasGroup>();

        SetDraggable(false);
    }

    // --- Public API -----------------------------------------------------------

    public void SetVisualPhase(int phaseIndex, Color tint)
    {
        ready = false; collectible = false;
        if (liquidImage) liquidImage.color = tint;

        var s = GetSafeSprite(beakerBase, phaseIndex);
        if (beakerImage && s) beakerImage.sprite = s;

        SetAnimatorPhase(phaseIndex);
        UpdateHighlighterPhase(phaseIndex);          // <-- keep hover outline in sync with phase

        SetDraggable(false);
        gameObject.tag = "Untagged";
        // IMPORTANT: no anchoredPosition changes here (minigame handles placement)
    }

    public void MarkFullPourable(int phaseIndex)
    {
        ready = true; collectible = false;
        SwapSelected(phaseIndex);
        SetDraggable(true);
        gameObject.tag = "Runoff";

        SetAnimatorPhase(phaseIndex);
        UpdateHighlighterPhase(phaseIndex);          // <-- ensure hover outline matches this phase

        if (beakerAnimator && !string.IsNullOrEmpty(showFullTrigger))
            beakerAnimator.SetTrigger(showFullTrigger);
    }

    public void MarkFullCollectible(int phaseIndex)
    {
        ready = true; collectible = true;
        SwapSelected(phaseIndex);
        SetDraggable(true);
        gameObject.tag = "Result";

        SetAnimatorPhase(phaseIndex);
        UpdateHighlighterPhase(phaseIndex);          // <-- ensure hover outline matches this phase

        if (beakerAnimator && !string.IsNullOrEmpty(showFullTrigger))
            beakerAnimator.SetTrigger(showFullTrigger);
    }

    public void EmptyAndReturnToArm(int nextPhaseIndex, Color nextPhaseTint)
    {
        SetVisualPhase(nextPhaseIndex, nextPhaseTint);
    }

    // --- Internals ------------------------------------------------------------

    private void SwapSelected(int phaseIndex)
    {
        Sprite s = GetSafeSprite(beakerSelected, phaseIndex);
        if (!s) s = GetSafeSprite(beakerFull, phaseIndex);
        if (beakerImage && s) beakerImage.sprite = s;
    }

    private Sprite GetSafeSprite(Sprite[] arr, int i)
    {
        if (arr == null || arr.Length == 0) return null;
        i = Mathf.Clamp(i, 0, arr.Length - 1);
        return arr[i];
    }

    private void SetAnimatorPhase(int index)
    {
        if (beakerAnimator && !string.IsNullOrEmpty(phaseIntParam))
            beakerAnimator.SetInteger(phaseIntParam, index);
    }

    private void SetDraggable(bool on)
    {
        if (draggable) draggable.enabled = on;
        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.interactable = on;
        cg.blocksRaycasts = true; // DraggableItem toggles during drag
    }

    private void UpdateHighlighterPhase(int phaseIndex)
    {
        if (!hoverHighlighter) return;
        hoverHighlighter.SetPhaseIndex(phaseIndex, resetHighlightFadeOnPhaseChange);
    }
}
