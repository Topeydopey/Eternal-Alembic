// Assets/Scripts/Minigame V2/PhilosophersAlembic/RunoffBeakerSprites.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class RunoffBeakerSprites : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Image beakerImage;     // The main beaker image
    [SerializeField] private Image liquidImage;     // Optional: an inner liquid image you can tint per phase (can be null)

    [Header("Sprites by Phase (index: 0=Black,1=White,2=Gold,3=Red)")]
    [SerializeField] private Sprite[] beakerBase;       // Idle/empty (or same as full if you don’t have empties)
    [SerializeField] private Sprite[] beakerFull;       // “full” look (non-selected)
    [SerializeField] private Sprite[] beakerSelected;   // *_SELECTED variants (ready to drag)

    [Header("Drag + Pour Tilt")]
    [SerializeField] private DraggableItem draggable;   // Your existing script
    [SerializeField] private float tiltAngle = -35f;
    [SerializeField] private float tiltSeconds = 0.25f;

    private PhilosophersAlembicMinigame owner;
    private RectTransform rt;
    private Vector2 armDefaultAnchoredPos;
    private Quaternion defaultRotation;
    private bool ready;      // full and ready to drag (Selected sprite)
    private bool collectible;// red phase: drag to TakeZone

    public void Init(PhilosophersAlembicMinigame owner)
    {
        this.owner = owner;
        rt = (RectTransform)transform;
        defaultRotation = rt.localRotation;
        armDefaultAnchoredPos = rt.anchoredPosition;
        if (!draggable) draggable = GetComponent<DraggableItem>();
        SetDraggable(false);
    }

    public void SetVisualPhase(int phaseIndex, Color tint)
    {
        ready = false; collectible = false;
        if (liquidImage) liquidImage.color = tint;

        Sprite s = (phaseIndex >= 0 && phaseIndex < beakerBase.Length) ? beakerBase[phaseIndex] : null;
        if (beakerImage && s) beakerImage.sprite = s;

        // reset transform
        rt.localRotation = defaultRotation;
        rt.anchoredPosition = armDefaultAnchoredPos;
        SetDraggable(false);
    }

    public void MarkFullPourable(int phaseIndex)
    {
        ready = true; collectible = false;
        SwapSelected(phaseIndex);
        SetDraggable(true);
        gameObject.tag = "Runoff";
    }

    public void MarkFullCollectible(int phaseIndex)
    {
        ready = true; collectible = true;
        SwapSelected(phaseIndex);
        SetDraggable(true);
        gameObject.tag = "Result";
    }

    private void SwapSelected(int phaseIndex)
    {
        Sprite s = (phaseIndex >= 0 && phaseIndex < beakerSelected.Length) ? beakerSelected[phaseIndex] : null;
        if (beakerImage && s) beakerImage.sprite = s;
    }

    public IEnumerator PourTiltRoutine()
    {
        // quick tilt forward then back
        float t = 0f;
        Quaternion from = defaultRotation;
        Quaternion to = Quaternion.Euler(0, 0, tiltAngle);

        while (t < tiltSeconds)
        {
            t += Time.deltaTime;
            rt.localRotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / tiltSeconds));
            yield return null;
        }

        t = 0f;
        while (t < tiltSeconds)
        {
            t += Time.deltaTime;
            rt.localRotation = Quaternion.Slerp(to, from, Mathf.Clamp01(t / tiltSeconds));
            yield return null;
        }
    }

    public void EmptyAndReturnToArm(int nextPhaseIndex, Color nextPhaseTint)
    {
        // Reset visuals for next fill
        SetVisualPhase(nextPhaseIndex, nextPhaseTint);
    }

    private void SetDraggable(bool on)
    {
        if (!draggable) return;
        draggable.enabled = on;
        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        // while dragging, DraggableItem sets blocksRaycasts=false; here we just ensure it’s interactable when ready
        cg.interactable = on;
        cg.blocksRaycasts = true;
    }
}
