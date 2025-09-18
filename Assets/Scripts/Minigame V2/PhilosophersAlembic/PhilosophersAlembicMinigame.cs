// Assets/Scripts/Minigame V2/PhilosophersAlembic/PhilosophersAlembicMinigame.cs
// Unity 6.2 • Universal 2D • Input System
// Single Animator on the Alembic drives BOTH boil (IsBoiling) and pour (Pour) by Phase.
// 5 phases supported (configurable collect phase). Beaker spawns via optional RectTransform anchors.
// Now includes "once only" persistence + success events.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PhilosophersAlembicMinigame : MonoBehaviour
{
    // Add 5th phase. Rename "Purple" to whatever matches your art (e.g., "Green" or "Quintessence").
    public enum Phase { Black = 0, White = 1, Gold = 2, Red = 3, Purple = 4 }
    private enum State { Idle, Filling, FullAwaitingPour, Pouring, Completed }

    [Header("Owners / Canvas")]
    public Canvas owningCanvas;
    public GameObject owningRoot;

    [Header("Once Only")]
    [Tooltip("Unique ID for this station (e.g., 'alembic_station_A'). Required if Once Only is on.")]
    [SerializeField] private string stationId = "alembic_station";
    [SerializeField] private bool onceOnly = false;
    [Tooltip("If already used and onceOnly is on, immediately close when opened.")]
    [SerializeField] private bool autoCloseIfAlreadyUsed = true;

    [Header("Drop Slots")]
    [SerializeField] private DropSlotAlembic mouthDropSlot;    // acceptsTag = "Runoff"
    [SerializeField] private DropSlotAlembic takeZoneDropSlot; // acceptsTag = "Result"

    [Header("Alembic Visuals")]
    [SerializeField] private Image alembicLiquidImage;

    [Header("Alembic Animator (combined)")]
    [Tooltip("Animator on the main alembic art — controls boil & pour.")]
    [SerializeField] private Animator alembicAnimator;
    [SerializeField] private string alembicPhaseParam = "Phase";        // int (0..N-1)
    [SerializeField] private string alembicBoilBoolParam = "IsBoiling"; // bool
    [SerializeField] private string alembicPourTrigger = "Pour";        // trigger
    [SerializeField, Min(0.05f)] private float alembicPourFallbackSeconds = 0.6f;

    [Header("Beaker (Runoff Glass Container)")]
    [Tooltip("Prefab with: RectTransform + CanvasGroup + Image + DraggableItem + RunoffBeakerSprites")]
    [SerializeField] private RunoffBeakerSprites beakerPrefab;
    [SerializeField] private Transform beakerSpawnParent; // default: owningCanvas

    [Tooltip("OPTIONAL: anchor refs for precise spawn/dock. Place them under the SAME parent as the beaker (beakerSpawnParent).")]
    [SerializeField] private RectTransform beakerSpawnAnchorRef; // overrides spawn position if assigned
    [SerializeField] private RectTransform beakerDockAnchorRef;  // overrides dock position if assigned

    [SerializeField] private Vector2 beakerDockAnchoredPos = new Vector2(140, -40);  // used if no anchorRef
    [SerializeField] private Vector2 beakerSpawnAnchoredPos = new Vector2(140, -40); // used if no anchorRef
    [SerializeField] private bool hideBeakerUntilFull = true;

    [Header("Pour Timing")]
    [SerializeField] private bool useHardPourTimer = true;     // set true to ignore animation event
    [SerializeField] private bool triggerPourAnimation = true; // still fire the animator trigger for visuals
    [SerializeField, Min(0.05f)] private float defaultPourSeconds = 0.6f;
    [SerializeField]
    private float[] pourSecondsPerPhase =      // size 5 if you’re using 5 phases
        new float[] { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };

    [Header("Phase Colors (size should match number of enum entries)")]
    [SerializeField] private Color blackCol = new Color(0.10f, 0.10f, 0.10f);
    [SerializeField] private Color whiteCol = new Color(0.93f, 0.93f, 0.93f);
    [SerializeField] private Color goldCol = new Color(0.95f, 0.76f, 0.31f);
    [SerializeField] private Color redCol = new Color(0.89f, 0.27f, 0.23f);
    [SerializeField] private Color purpleCol = new Color(0.58f, 0.33f, 0.74f); // NEW phase color

    [Header("Per-Phase Fill Time (sec) — size should be 5 now")]
    [SerializeField] private float[] phaseFillTimes = new float[] { 2.5f, 2.0f, 1.6f, 1.6f, 1.6f };

    [Header("Collection Settings")]
    [Tooltip("Which phase produces the final collectible beaker for the take zone?")]
    [SerializeField] private Phase collectOnPhase = Phase.Red; // change to Phase.Purple if you want the 5th to be final

    [Header("Result (final turn-in)")]
    [SerializeField] private ItemSO resultItem;

    [Header("Events")]
    public UnityEvent onClosed;
    [Tooltip("Invoked only on SUCCESS (when result is actually awarded).")]
    public UnityEvent onSuccessUnity;
    public event Action onSuccess;

    // --- Runtime ---
    public Phase CurrentPhase { get; private set; } = Phase.Black;
    private State state = State.Idle;
    private Coroutine fillingCo;
    private RunoffBeakerSprites beakerInstance;
    public bool BurnerOn { get; private set; }
    private bool alembicPourDoneFlag;

    void Awake()
    {
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;
        if (!beakerSpawnParent && owningCanvas) beakerSpawnParent = owningCanvas.transform;

        if (!mouthDropSlot)
            mouthDropSlot = GetComponentInChildren<DropSlotAlembic>(true);
        if (!takeZoneDropSlot)
        {
            foreach (var s in GetComponentsInChildren<DropSlotAlembic>(true))
                if (s && s.acceptsTag == "Result") { takeZoneDropSlot = s; break; }
        }

        SetPhase(Phase.Black);
        ApplyPhaseTargets();
    }

    public void BeginSession()
    {
        // 🔒 Guard once-only right when station opens
        if (onceOnly && WorkstationOnce.IsUsed(stationId))
        {
            if (autoCloseIfAlreadyUsed)
            {
                owningRoot?.SetActive(false);
                onClosed?.Invoke();
                return;
            }
        }

        StopAllCoroutines();
        DestroyBeakerIfAny();

        SetPhase(Phase.Black);
        state = State.Idle;
        BurnerOn = false;
        SetAlembicBoil(false);
        ApplyPhaseTargets();
    }

    // Hook to furnace click (same alembic object)
    public void OnBurnerClicked()
    {
        if (state != State.Idle)
        {
            Debug.Log($"[Alembic] Ignoring burner click during {state}.");
            return;
        }

        // If already at collect phase + beaker is ready, don't reboil
        if (CurrentPhase.Equals(collectOnPhase) && beakerInstance && beakerInstance.gameObject.activeSelf && state == State.FullAwaitingPour)
        {
            Debug.Log("[Alembic] Collect-phase beaker ready; pour/turn-in instead of boiling.");
            return;
        }

        StartFillCycle();
    }

    private void StartFillCycle()
    {
        if (fillingCo != null) StopCoroutine(fillingCo);
        fillingCo = StartCoroutine(FillRoutineOnce());
    }

    private IEnumerator FillRoutineOnce()
    {
        state = State.Filling;
        BurnerOn = true;
        SetAlembicBoil(true);

        // Prepare beaker (hidden until full if requested)
        PrepareBeakerForPhase(spawnIfMissing: true, visibleNow: !hideBeakerUntilFull);

        float fillTime = GetPhaseFillTime(CurrentPhase);
        float t = 0f;
        while (t < fillTime)
        {
            if (!gameObject.activeInHierarchy) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        // Full: tag + visuals
        if (CurrentPhase.Equals(collectOnPhase))
        {
            beakerInstance.MarkFullCollectible((int)CurrentPhase);
            TagBeaker("Result");
        }
        else
        {
            beakerInstance.MarkFullPourable((int)CurrentPhase);
            TagBeaker("Runoff");
        }

        // Place and **show** it now
        PlaceBeakerAtSpawn();
        ShowBeakerForPlayer();

        BurnerOn = false;
        SetAlembicBoil(false);

        ApplyPhaseTargets();
        state = State.FullAwaitingPour;
        fillingCo = null;
    }

    // ---------- ALEMBIC-SPECIFIC DROP HANDLER ----------
    public void HandleDropAlembic(DropSlotAlembic slot, GameObject droppedGO)
    {
        if (!slot || !droppedGO) return;

        // Final collection
        if (slot.acceptsTag == "Result" && droppedGO.CompareTag("Result") && CurrentPhase.Equals(collectOnPhase))
        {
            var drag = droppedGO.GetComponent<DraggableItem>();
            if (drag) drag.Consume();

            if (EquipmentInventory.Instance && resultItem &&
                EquipmentInventory.Instance.TryEquipToFirstAvailable(resultItem))
            {
                // ✅ SUCCESS: mark once-only + raise events
                if (onceOnly) WorkstationOnce.MarkUsed(stationId);
                onSuccess?.Invoke();
                onSuccessUnity?.Invoke();

                state = State.Completed;
                owningRoot?.SetActive(false);
                onClosed?.Invoke();
            }
            else
            {
                Debug.Log("[Alembic] Inventory full; could not equip result.");
            }
            return;
        }

        // Pour back in (pre-collect phases)
        if (slot.acceptsTag == "Runoff" && droppedGO.CompareTag("Runoff") && !CurrentPhase.Equals(collectOnPhase))
        {
            var rb = droppedGO.GetComponent<RunoffBeakerSprites>();
            if (rb && state == State.FullAwaitingPour) StartCoroutine(PourAndAdvance(rb));
            return;
        }

        Debug.Log($"[Alembic] Drop ignored. Slot:{slot.acceptsTag}, Item:{droppedGO.tag}, Phase:{CurrentPhase}, State:{state}");
    }

    private IEnumerator PourAndAdvance(RunoffBeakerSprites rb)
    {
        state = State.Pouring;
        BurnerOn = false;
        SetAlembicBoil(false);
        if (fillingCo != null) { StopCoroutine(fillingCo); fillingCo = null; }

        // Hide UI beaker while the pixel-art pour plays on the Alembic animator
        if (rb) rb.gameObject.SetActive(false);

        yield return PlayAlembicPourAndWait();

        // Return beaker to dock (and hide until next boil if configured)
        ReturnBeakerToDockAndHide();

        // Advance color phase
        SetPhase(NextPhase(CurrentPhase));

        ApplyPhaseTargets();
        state = State.Idle;
    }

    // -------------------- Helpers --------------------

    private void PrepareBeakerForPhase(bool spawnIfMissing, bool visibleNow)
    {
        if (!beakerInstance && spawnIfMissing)
        {
            if (!beakerPrefab)
            {
                Debug.LogError("[Alembic] Beaker prefab not assigned.");
                return;
            }

            var parent = beakerSpawnParent ? beakerSpawnParent : owningCanvas.transform;
            var go = Instantiate(beakerPrefab.gameObject, parent, false);
            go.name = beakerPrefab.name + " (Runtime)";
            beakerInstance = go.GetComponent<RunoffBeakerSprites>() ?? go.AddComponent<RunoffBeakerSprites>();

            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true;

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.preserveAspect = true; img.raycastTarget = true;

            _ = go.GetComponent<DraggableItem>() ?? go.AddComponent<DraggableItem>();

            beakerInstance.Init(this);
        }

        if (!beakerInstance) return;

        // Set visuals for current phase (does NOT change position or active state)
        beakerInstance.SetVisualPhase((int)CurrentPhase, GetPhaseColor(CurrentPhase));

        // Initial placement (spawn or dock), but **do not force-active** here.
        if (visibleNow) PlaceBeakerAtSpawn();
        else PlaceBeakerAtDock();

        // Respect the "hide until full" flag here:
        beakerInstance.gameObject.SetActive(visibleNow);

        // Pre-tag (updated again when full)
        TagBeaker(CurrentPhase.Equals(collectOnPhase) ? "Result" : "Runoff");
    }

    private void PlaceBeakerAtSpawn()
    {
        var rt = beakerInstance ? beakerInstance.GetComponent<RectTransform>() : null;
        if (!rt) return;

        var parentRT = beakerSpawnParent as RectTransform;
        if (parentRT)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        rt.anchoredPosition = ResolveAnchoredPos(beakerSpawnAnchorRef, beakerSpawnAnchoredPos);
    }

    private void PlaceBeakerAtDock()
    {
        var rt = beakerInstance ? beakerInstance.GetComponent<RectTransform>() : null;
        if (!rt) return;

        var parentRT = beakerSpawnParent as RectTransform;
        if (parentRT)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        rt.anchoredPosition = ResolveAnchoredPos(beakerDockAnchorRef, beakerDockAnchoredPos);
    }

    private Vector2 ResolveAnchoredPos(RectTransform refRt, Vector2 fallback)
    {
        // For simplicity/robustness: assume refRt (if set) shares the SAME parent as the beaker.
        // If not, we'll just use the fallback.
        if (!refRt) return fallback;

        var parent = beakerSpawnParent as RectTransform;
        if (parent && refRt.parent == parent) return refRt.anchoredPosition;

        // Different hierarchy? Use fallback to avoid mismatched spaces.
        return fallback;
    }

    private void ShowBeakerForPlayer()
    {
        if (!beakerInstance) return;

        // turn it on
        if (!beakerInstance.gameObject.activeSelf)
            beakerInstance.gameObject.SetActive(true);

        // make sure it’s clickable / on top
        var cg = beakerInstance.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }

        beakerInstance.transform.SetAsLastSibling();
    }

    private void ReturnBeakerToDockAndHide()
    {
        if (!beakerInstance) return;
        PlaceBeakerAtDock();
        beakerInstance.gameObject.SetActive(!hideBeakerUntilFull); // hides when the box is checked
    }

    private void DestroyBeakerIfAny()
    {
        if (beakerInstance)
        {
            Destroy(beakerInstance.gameObject);
            beakerInstance = null;
        }
    }

    private void TagBeaker(string tagName)
    {
        if (!beakerInstance) return;
        beakerInstance.gameObject.tag = tagName;
    }

    private void ApplyPhaseTargets()
    {
        bool atCollect = CurrentPhase.Equals(collectOnPhase);
        if (mouthDropSlot) mouthDropSlot.Enable(!atCollect);
        if (takeZoneDropSlot) takeZoneDropSlot.Enable(atCollect);
    }

    private void SetPhase(Phase p)
    {
        CurrentPhase = p;
        UpdateAlembicColor();
        if (beakerInstance) beakerInstance.SetVisualPhase((int)p, GetPhaseColor(p));
        UpdateAlembicAnimatorPhase(); // push phase int
    }

    private void UpdateAlembicAnimatorPhase()
    {
        if (alembicAnimator && !string.IsNullOrEmpty(alembicPhaseParam))
            alembicAnimator.SetInteger(alembicPhaseParam, (int)CurrentPhase);
    }

    private void UpdateAlembicColor()
    {
        if (!alembicLiquidImage) return;
        alembicLiquidImage.color = GetPhaseColor(CurrentPhase);
    }

    public Color GetPhaseColor(Phase p) =>
        p switch
        {
            Phase.Black => blackCol,
            Phase.White => whiteCol,
            Phase.Gold => goldCol,
            Phase.Red => redCol,
            Phase.Purple => purpleCol,
            _ => blackCol
        };

    private float GetPhaseFillTime(Phase p)
    {
        int i = (int)p;
        if (phaseFillTimes != null && i < phaseFillTimes.Length) return phaseFillTimes[i];
        return 2f;
    }

    private float GetPourTime(Phase p)
    {
        int i = (int)p;
        if (pourSecondsPerPhase != null && i < pourSecondsPerPhase.Length && pourSecondsPerPhase[i] > 0f)
            return pourSecondsPerPhase[i];
        return Mathf.Max(0.05f, defaultPourSeconds);
    }

    private Phase NextPhase(Phase p)
    {
        int idx = (int)p;
        int max = System.Enum.GetValues(typeof(Phase)).Length - 1;
        idx = Mathf.Clamp(idx + 1, 0, max);
        return (Phase)idx;
    }

    private void SetAlembicBoil(bool on)
    {
        if (!alembicAnimator || string.IsNullOrEmpty(alembicBoilBoolParam)) return;
        UpdateAlembicAnimatorPhase(); // choose correct Boil_* by current phase
        alembicAnimator.SetBool(alembicBoilBoolParam, on);
    }

    // --- Pour animation on the SAME alembic animator ---
    private IEnumerator PlayAlembicPourAndWait()
    {
        // Optionally still play the visual animation
        if (alembicAnimator && triggerPourAnimation && !string.IsNullOrEmpty(alembicPourTrigger))
        {
            UpdateAlembicAnimatorPhase();               // make sure Phase is set
            alembicAnimator.ResetTrigger(alembicPourTrigger);
            alembicAnimator.SetTrigger(alembicPourTrigger);
        }

        if (useHardPourTimer)
        {
            // Pure timer path: ignore animation events
            float wait = GetPourTime(CurrentPhase);
            float t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Event-or-fallback path (original behavior)
            alembicPourDoneFlag = false;
            float t = 0f, maxWait = Mathf.Max(0.05f, alembicPourFallbackSeconds);
            while (!alembicPourDoneFlag && t < maxWait)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    /// <summary>Animation Event: call this at the END of each Pour_* clip on the Alembic animator.</summary>
    public void AlembicOnPourAnimComplete() => alembicPourDoneFlag = true;

    // Bridge (generic signature)
    public void HandleDrop(DropSlot slot, GameObject droppedGO)
    {
        if (slot == null || droppedGO == null) return;
        if (slot.TryGetComponent<DropSlotAlembic>(out var alembicSlot) && alembicSlot != null)
            HandleDropAlembic(alembicSlot, droppedGO);
        else
            Debug.LogWarning("[Alembic] HandleDrop called with a non-Alembic DropSlot.");
    }
}
