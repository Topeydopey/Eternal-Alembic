// Assets/Scripts/Minigame V2/PhilosophersAlembic/PhilosophersAlembicMinigame.cs
// Unity 6.2 • Universal 2D • Input System
// Single Animator on the Alembic drives BOTH boil (IsBoiling) and pour (Pour) by Phase.
// 5 phases supported (configurable collect phase). Beaker spawns via optional RectTransform anchors.
// Includes "once only" persistence + success events.
// UPDATED: allow taking the beaker at ANY phase (same result item), and keep the Take Zone always visible.
// UPDATED: adds audio for furnace-click, boiling loop, pour one-shot, and take one-shot.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PhilosophersAlembicMinigame : MonoBehaviour
{
    public enum Phase { Black = 0, White = 1, Gold = 2, Red = 3, Purple = 4 }
    private enum State { Idle, Filling, FullAwaitingPour, Pouring, Completed }

    [Header("Owners / Canvas")]
    public Canvas owningCanvas;
    public GameObject owningRoot;

    [Header("Once Only")]
    [SerializeField] private string stationId = "alembic_station";
    [SerializeField] private bool onceOnly = false;
    [SerializeField] private bool autoCloseIfAlreadyUsed = true;

    [Header("Drop Slots")]
    [SerializeField] private DropSlotAlembic mouthDropSlot;    // acceptsTag = "Runoff"
    [SerializeField] private DropSlotAlembic takeZoneDropSlot; // acceptsTag = "Result"

    [Header("Alembic Visuals")]
    [SerializeField] private Image alembicLiquidImage;

    [Header("Alembic Animator (combined)")]
    [SerializeField] private Animator alembicAnimator;
    [SerializeField] private string alembicPhaseParam = "Phase";        // int (0..N-1)
    [SerializeField] private string alembicBoilBoolParam = "IsBoiling"; // bool
    [SerializeField] private string alembicPourTrigger = "Pour";        // trigger
    [SerializeField, Min(0.05f)] private float alembicPourFallbackSeconds = 0.6f;

    [Header("Beaker (Runoff Glass Container)")]
    [SerializeField] private RunoffBeakerSprites beakerPrefab;
    [SerializeField] private Transform beakerSpawnParent; // default: owningCanvas
    [SerializeField] private RectTransform beakerSpawnAnchorRef;
    [SerializeField] private RectTransform beakerDockAnchorRef;
    [SerializeField] private Vector2 beakerDockAnchoredPos = new Vector2(140, -40);
    [SerializeField] private Vector2 beakerSpawnAnchoredPos = new Vector2(140, -40);
    [SerializeField] private bool hideBeakerUntilFull = true;

    [Header("Pour Timing")]
    [SerializeField] private bool useHardPourTimer = true;
    [SerializeField] private bool triggerPourAnimation = true;
    [SerializeField, Min(0.05f)] private float defaultPourSeconds = 0.6f;
    [SerializeField] private float[] pourSecondsPerPhase = new float[] { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };

    [Header("Phase Colors (size should match number of enum entries)")]
    [SerializeField] private Color blackCol = new Color(0.10f, 0.10f, 0.10f);
    [SerializeField] private Color whiteCol = new Color(0.93f, 0.93f, 0.93f);
    [SerializeField] private Color goldCol = new Color(0.95f, 0.76f, 0.31f);
    [SerializeField] private Color redCol = new Color(0.89f, 0.27f, 0.23f);
    [SerializeField] private Color purpleCol = new Color(0.58f, 0.33f, 0.74f);

    [Header("Per-Phase Fill Time (sec) — size should be 5 now")]
    [SerializeField] private float[] phaseFillTimes = new float[] { 2.5f, 2.0f, 1.6f, 1.6f, 1.6f };

    [Header("Collection Settings")]
    [SerializeField] private Phase collectOnPhase = Phase.Red;

    [Header("Result (final turn-in)")]
    [SerializeField] private ItemSO resultItem;

    // Early-take feature
    [Header("Early Take Settings")]
    [SerializeField] private bool allowAnyPhaseTake = true;
    [SerializeField] private bool alwaysShowTakeZone = true;

    // ---------- AUDIO ----------
    [Header("Audio Sources")]
    [Tooltip("2D world SFX for the alembic (click/pour). If null, created automatically.")]
    [SerializeField] private AudioSource worldAudio;
    [Tooltip("Separate looping source for boiling. If null, created automatically.")]
    [SerializeField] private AudioSource boilLoopAudio;
    [Tooltip("UI/Take SFX. Assign to a 'safe' object that doesn't get disabled on close. If null, a temp one is created.")]
    [SerializeField] private AudioSource uiAudio;

    [Header("SFX Clips")]
    [Tooltip("Plays once when the furnace is clicked and a fill actually begins.")]
    [SerializeField] private AudioClip furnaceClickSfx;
    [Tooltip("Loop clip while filling/boiling (should be loopable).")]
    [SerializeField] private AudioClip boilingLoopSfx;
    [Tooltip("Plays once when pouring back into the alembic.")]
    [SerializeField] private AudioClip pourSfx;
    [Tooltip("Plays once when turning in the beaker (any phase).")]
    [SerializeField] private AudioClip takeSfx;

    [Header("SFX Levels")]
    [Range(0f, 1f)] public float furnaceClickVolume = 1f;
    [Range(0f, 1f)] public float boilingVolume = 0.9f;
    [Range(0f, 1f)] public float pourVolume = 1f;
    [Range(0f, 1f)] public float takeVolume = 1f;

    [Header("Events")]
    public UnityEvent onClosed;
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

        // Ensure audio sources exist
        worldAudio = EnsureAudio(worldAudio, "AlembicWorldAudio2D", loop: false);
        boilLoopAudio = EnsureAudio(boilLoopAudio, "AlembicBoilLoop2D", loop: true);
        uiAudio = uiAudio ? uiAudio : null; // optional: user may assign a safe, persistent source

        SetPhase(Phase.Black);
        ApplyPhaseTargets();
    }

    private AudioSource EnsureAudio(AudioSource src, string childName, bool loop)
    {
        if (src) return src;
        var t = transform.Find(childName);
        if (t && t.TryGetComponent(out AudioSource found)) { found.loop = loop; found.playOnAwake = false; found.spatialBlend = 0f; return found; }
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = loop;
        a.spatialBlend = 0f; // 2D
        return a;
    }

    public void BeginSession()
    {
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
        StopBoilLoop();
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

        // If already at collect phase + beaker ready, don't reboil
        if (CurrentPhase.Equals(collectOnPhase) && beakerInstance && beakerInstance.gameObject.activeSelf && state == State.FullAwaitingPour)
        {
            Debug.Log("[Alembic] Collect-phase beaker ready; pour/turn-in instead of boiling.");
            return;
        }

        // SFX: click (only when a fill actually starts)
        PlayOneShotSafe(worldAudio, furnaceClickSfx, furnaceClickVolume);

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

        // SFX: start boiling loop
        StartBoilLoop();

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

        // Place and show
        PlaceBeakerAtSpawn();
        ShowBeakerForPlayer();

        BurnerOn = false;
        SetAlembicBoil(false);

        // SFX: stop boiling loop
        StopBoilLoop();

        ApplyPhaseTargets();
        state = State.FullAwaitingPour;
        fillingCo = null;
    }

    // ---------- ALEMBIC-SPECIFIC DROP HANDLER ----------
    public void HandleDropAlembic(DropSlotAlembic slot, GameObject droppedGO)
    {
        if (!slot || !droppedGO) return;

        // ------- Final collection (allowed at ANY phase if allowAnyPhaseTake) -------
        if (slot.acceptsTag == "Result")
        {
            bool isResultTagged = droppedGO.CompareTag("Result");
            bool isRunoffTagged = droppedGO.CompareTag("Runoff");
            bool canAcceptAtThisPhase = isResultTagged || (allowAnyPhaseTake && isRunoffTagged);

            if (canAcceptAtThisPhase)
            {
                // SFX: take (play via safe UI source if available)
                PlayTakeSfx();

                var drag = droppedGO.GetComponent<DraggableItem>();
                if (drag) drag.Consume();

                if (EquipmentInventory.Instance && resultItem &&
                    EquipmentInventory.Instance.TryEquipToFirstAvailable(resultItem))
                {
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

        // Ensure boil loop is off during pour
        StopBoilLoop();

        if (fillingCo != null) { StopCoroutine(fillingCo); fillingCo = null; }

        // Hide UI beaker while alembic pour anim plays
        if (rb) rb.gameObject.SetActive(false);

        // SFX: pour one-shot
        PlayOneShotSafe(worldAudio, pourSfx, pourVolume);

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

        beakerInstance.SetVisualPhase((int)CurrentPhase, GetPhaseColor(CurrentPhase));

        if (visibleNow) PlaceBeakerAtSpawn();
        else PlaceBeakerAtDock();

        beakerInstance.gameObject.SetActive(visibleNow);

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
        if (!refRt) return fallback;
        var parent = beakerSpawnParent as RectTransform;
        if (parent && refRt.parent == parent) return refRt.anchoredPosition;
        return fallback;
    }

    private void ShowBeakerForPlayer()
    {
        if (!beakerInstance) return;

        if (!beakerInstance.gameObject.activeSelf)
            beakerInstance.gameObject.SetActive(true);

        var cg = beakerInstance.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }

        beakerInstance.transform.SetAsLastSibling();
    }

    private void ReturnBeakerToDockAndHide()
    {
        if (!beakerInstance) return;
        PlaceBeakerAtDock();
        beakerInstance.gameObject.SetActive(!hideBeakerUntilFull);
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
        if (mouthDropSlot) mouthDropSlot.Enable(true);

        if (takeZoneDropSlot)
        {
            if (alwaysShowTakeZone) takeZoneDropSlot.Enable(true);
            else takeZoneDropSlot.Enable(CurrentPhase.Equals(collectOnPhase));
        }
    }

    private void SetPhase(Phase p)
    {
        CurrentPhase = p;
        UpdateAlembicColor();
        if (beakerInstance) beakerInstance.SetVisualPhase((int)p, GetPhaseColor(p));
        UpdateAlembicAnimatorPhase();
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
        UpdateAlembicAnimatorPhase();
        alembicAnimator.SetBool(alembicBoilBoolParam, on);
    }

    private IEnumerator PlayAlembicPourAndWait()
    {
        if (alembicAnimator && triggerPourAnimation && !string.IsNullOrEmpty(alembicPourTrigger))
        {
            UpdateAlembicAnimatorPhase();
            alembicAnimator.ResetTrigger(alembicPourTrigger);
            alembicAnimator.SetTrigger(alembicPourTrigger);
        }

        if (useHardPourTimer)
        {
            float wait = GetPourTime(CurrentPhase);
            float t = 0f;
            while (t < wait) { t += Time.deltaTime; yield return null; }
        }
        else
        {
            alembicPourDoneFlag = false;
            float t = 0f, maxWait = Mathf.Max(0.05f, alembicPourFallbackSeconds);
            while (!alembicPourDoneFlag && t < maxWait) { t += Time.deltaTime; yield return null; }
        }
    }

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

    // ---------- Audio helpers ----------
    private void StartBoilLoop()
    {
        if (!boilLoopAudio) return;
        if (boilingLoopSfx)
        {
            boilLoopAudio.clip = boilingLoopSfx;
            boilLoopAudio.volume = boilingVolume;
            boilLoopAudio.loop = true;
            if (!boilLoopAudio.isPlaying) boilLoopAudio.Play();
        }
    }

    private void StopBoilLoop()
    {
        if (!boilLoopAudio) return;
        if (boilLoopAudio.isPlaying) boilLoopAudio.Stop();
        boilLoopAudio.clip = null;
    }

    private void PlayTakeSfx()
    {
        // Prefer a user-assigned safe UI source (doesn't get disabled on close), else fall back to world/temporary
        if (uiAudio && takeSfx) { uiAudio.PlayOneShot(takeSfx, takeVolume); return; }
        PlayOneShotSafe(worldAudio, takeSfx, takeVolume);
    }

    private void PlayOneShotSafe(AudioSource src, AudioClip clip, float vol = 1f, float pitch = 1f)
    {
        if (!clip) return;

        if (src)
        {
            float prev = src.pitch;
            src.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
            src.PlayOneShot(clip, Mathf.Clamp01(vol));
            src.pitch = prev;
        }
        else
        {
            // detached temp source (won't get cut off if hierarchy closes)
            var go = new GameObject("AlembicOneShot2D");
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = false; a.spatialBlend = 0f;
            a.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
            a.volume = Mathf.Clamp01(vol);
            a.clip = clip;
            a.Play();
            Destroy(go, Mathf.Max(0.02f, clip.length / Mathf.Max(0.01f, a.pitch)));
        }
    }
}
