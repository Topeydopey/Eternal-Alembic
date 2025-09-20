// Assets/Scripts/Minigame V2/SnakeWorldMinigame.cs
// Unity 6.2 • Universal 2D • Input System
// Spawns your prefab for the result token (preserves highlight scripts), with optional UI overrides.
// Supports "once only" stations with PlayerPrefs persistence and a success event.
// + Audio: slither loop, eat, complete, take

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SnakeWorldMinigame : MonoBehaviour
{
    public event Action onClosed;
    public event Action onSuccess;   // fired only when result is successfully awarded

    [Header("Owns / Canvas")]
    public Canvas owningCanvas;
    public GameObject owningRoot;
    public bool disableInsteadOfDestroy = true;

    [Header("Once Only")]
    [Tooltip("Unique ID for this station (e.g., 'snake_station_A'). Required if Once Only is on.")]
    public string stationId = "snake_station";
    public bool onceOnly = false;
    [Tooltip("If already used and onceOnly is on, immediately close when opened.")]
    public bool autoCloseIfAlreadyUsed = true;

    [Header("Snake (World)")]
    public SnakePhysicsController snakeHead;   // required
    [Tooltip("Optional parent that contains head+body visuals to hide on complete.")]
    public GameObject snakeRoot;

    [Header("Seed Drop Surface (UI)")]
    public SeedDropSlotWorld dropSurface;      // disable when completed

    [Header("Result Token (UI)")]
    public ItemSO resultItem;                  // assign your ItemSO
    public GameObject resultTokenPrefab;       // UI prefab: RectTransform + Image + CanvasGroup (+ DraggableItem + your highlight script)
    public DropSlotResult takeZone;            // acceptsTag="Result"
    public Transform resultTokenSpawnParent;   // default: owningCanvas.transform
    public Vector2 resultSpawnAnchoredPos = new Vector2(0, -80); // visible spot
    public Sprite resultIconOverride;          // optional explicit icon override

    [Header("Result Token (Overrides)")]
    [SerializeField] private bool overridePrefabIcon = true;
    [SerializeField] private bool overridePrefabSizeAndScale = true;
    [SerializeField] private bool forceTagAndLayer = true;
    [SerializeField] private string resultTokenTag = "Result";

    [Header("Result Token Sizing")]
    [SerializeField] private Vector2 resultTokenSize = new Vector2(100, 100); // pixels
    [SerializeField] private bool sizeByScale = true;     // if true, also scale the RectTransform
    [SerializeField, Range(0.05f, 5f)]
    private float resultTokenScale = 0.2f;                // e.g., 0.2 to match your prefab look

    [Header("Win Condition")]
    [Tooltip("-1 = use SnakePhysicsController value")]
    public int seedsToWinOverride = -1;

    // ---------------------- AUDIO ----------------------
    [Header("Audio Sources")]
    [Tooltip("2D audio for snake world SFX (slither/eat/complete). Auto-added if empty.")]
    [SerializeField] private AudioSource snakeAudio;
    [Tooltip("2D audio for UI token SFX (take). Auto-added if empty.")]
    [SerializeField] private AudioSource uiAudio;

    [Header("SFX • Slither (ambient loop)")]
    [SerializeField] private bool enableSlitherLoop = true;
    [SerializeField] private AudioClip[] slitherClips;
    [SerializeField] private Vector2 slitherIntervalSeconds = new Vector2(1.8f, 3.0f);
    [SerializeField] private Vector2 slitherPitchRange = new Vector2(0.98f, 1.03f);
    [SerializeField] private Vector2 slitherVolumeRange = new Vector2(0.75f, 0.95f);

    [Header("SFX • Eat seed")]
    [SerializeField] private AudioClip[] eatClips;
    [SerializeField] private Vector2 eatPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private Vector2 eatVolumeRange = new Vector2(0.9f, 1.0f);

    [Header("SFX • Complete (snake → result)")]
    [SerializeField] private AudioClip[] completeClips;
    [SerializeField] private Vector2 completePitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 completeVolumeRange = new Vector2(0.95f, 1.0f);

    [Header("SFX • Take (pick result token)")]
    [SerializeField] private AudioClip[] takeClips;
    [SerializeField] private Vector2 takePitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 takeVolumeRange = new Vector2(0.9f, 1.0f);

    [Header("Audio Options")]
    [Tooltip("Avoid repeating the same clip back-to-back in each category.")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    private enum SfxCategory { Slither, Eat, Complete, Take }
    private int _lastSlither = -1, _lastEat = -1, _lastComplete = -1, _lastTake = -1;

    [Header("Debug")]
    public bool verbose = false;

    private GameObject spawnedToken;

    // state for slither loop
    private Coroutine slitherCo;
    private bool sessionActive;
    private bool completed;

    void Awake()
    {
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;
        if (!resultTokenSpawnParent && owningCanvas) resultTokenSpawnParent = owningCanvas.transform;

        snakeAudio = EnsureAudioSource(snakeAudio, "SnakeAudio2D");
        uiAudio = EnsureAudioSource(uiAudio, "UIAudio2D");
    }

    private AudioSource EnsureAudioSource(AudioSource src, string childName)
    {
        if (src) return src;
        var t = transform.Find(childName);
        if (t && t.TryGetComponent(out AudioSource found)) return found;

        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f; // 2D
        return a;
    }

    void OnEnable()
    {
        if (!snakeHead) { Debug.LogError("[SnakeWorldMini] snakeHead not assigned."); return; }
        snakeHead.OnCompleted += OnCompleted;

        // 🔒 Guard once-only right when station opens (only if we have a valid id)
        if (onceOnly && !string.IsNullOrEmpty(stationId) && WorkstationOnce.IsUsed(stationId))
        {
            if (verbose) Debug.Log($"[SnakeWorldMini] '{stationId}' already used. Auto-close={autoCloseIfAlreadyUsed}.");
            if (autoCloseIfAlreadyUsed) { CloseUI(); return; }
        }

        BeginSession();
    }

    void OnDisable()
    {
        if (snakeHead) snakeHead.OnCompleted -= OnCompleted;
        StopSlitherLoop();
    }

    public void BeginSession()
    {
        if (!snakeHead) { Debug.LogError("[SnakeWorldMini] snakeHead not assigned."); return; }

        snakeHead.SetDriveMode(SnakePhysicsController.DriveMode.PlayerSteer);
        snakeHead.SetFrozen(false);
        snakeHead.ResetSession(seedsToWinOverride);

        if (verbose) Debug.Log("[SnakeWorldMini] BeginSession");

        // Show the snake visuals
        if (snakeRoot) snakeRoot.SetActive(true);
        else snakeHead.gameObject.SetActive(true);

        // Enable seed drop surface
        if (dropSurface)
        {
            dropSurface.enabled = true;
            var img = dropSurface.GetComponent<Image>();
            if (img) img.raycastTarget = true;
            dropSurface.snake = snakeHead; // ensure linked
        }

        // Enable take zone; token not spawned yet
        if (takeZone) takeZone.gameObject.SetActive(true);

        // Cleanup any previous token
        if (spawnedToken) { Destroy(spawnedToken); spawnedToken = null; }

        completed = false;
        sessionActive = true;
        StartSlitherLoop();
    }

    private void OnCompleted()
    {
        if (verbose) Debug.Log("[SnakeWorldMini] Completed: hide snake & spawn result token");

        // 1) stop seed input
        if (dropSurface)
        {
            dropSurface.enabled = false;
            var img = dropSurface.GetComponent<Image>();
            if (img) img.raycastTarget = false;
        }

        // 2) freeze + hide snake visuals
        snakeHead.SetFrozen(true);
        if (snakeRoot) snakeRoot.SetActive(false);
        else snakeHead.gameObject.SetActive(false);

        // AUDIO: completion sting
        PlayRandomOneShotCategory(snakeAudio, completeClips, completeVolumeRange, completePitchRange, SfxCategory.Complete);

        // 3) spawn UI token
        SpawnResultToken();

        completed = true;
        StopSlitherLoop();
    }

    private void SpawnResultToken()
    {
        if (!owningCanvas)
        {
            Debug.LogError("[SnakeWorldMini] No owningCanvas; cannot spawn UI token.");
            return;
        }

        var parent = resultTokenSpawnParent ? resultTokenSpawnParent : owningCanvas.transform;

        // Resolve desired icon: explicit override -> prefab's Image -> prefab's SpriteRenderer (if any)
        Sprite resolvedIcon = resultIconOverride;
        if (!resolvedIcon && resultTokenPrefab)
        {
            var pImg = resultTokenPrefab.GetComponent<Image>();
            if (pImg && pImg.sprite) resolvedIcon = pImg.sprite;
            if (!resolvedIcon)
            {
                var pSr = resultTokenPrefab.GetComponent<SpriteRenderer>();
                if (pSr && pSr.sprite) resolvedIcon = pSr.sprite;
            }
        }

        GameObject go = null;

        if (resultTokenPrefab != null)
        {
            // --- Use the user's prefab (so highlight scripts, etc., are preserved) ---
            go = Instantiate(resultTokenPrefab, parent, worldPositionStays: false);
            go.name = $"{resultTokenPrefab.name} (ResultToken)";

            // Optional: enforce tag/layer for drop filtering + UI interaction
            if (forceTagAndLayer)
            {
                if (!string.IsNullOrEmpty(resultTokenTag)) go.tag = resultTokenTag;
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0) go.layer = uiLayer;
            }

            // Ensure it is a UI token (has RectTransform)
            var rt = go.GetComponent<RectTransform>();
            if (!rt)
            {
                Debug.LogError("[SnakeWorldMini] Result prefab is not UI (RectTransform missing). " +
                               "Use a UI prefab for this minigame’s takeZone, or change the takeZone to world-space.");
            }
            else
            {
                // Position/anchor
                rt.SetAsLastSibling();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = resultSpawnAnchoredPos;

                // Size & scale (optional override)
                if (overridePrefabSizeAndScale)
                {
                    rt.sizeDelta = resultTokenSize;
                    rt.localScale = sizeByScale ? Vector3.one * resultTokenScale : Vector3.one;
                }
            }

            // Image + icon override (optional)
            var img = go.GetComponent<Image>();
            if (!img)
            {
                img = go.AddComponent<Image>();
                img.preserveAspect = true;
            }
            if (overridePrefabIcon && resolvedIcon) img.sprite = resolvedIcon;
            img.raycastTarget = true;

            // CanvasGroup (ensure interactable)
            var cg = go.GetComponent<CanvasGroup>();
            if (!cg) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

            // DraggableItem (warn if missing)
            var drag = go.GetComponent<DraggableItem>();
            if (!drag)
            {
                drag = go.AddComponent<DraggableItem>();
                if (verbose) Debug.LogWarning("[SnakeWorldMini] Result prefab had no DraggableItem; added one.");
            }
        }
        else
        {
            // --- Fallback: build a simple UI token if no prefab assigned ---
            go = new GameObject("ResultToken (UI)", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            if (!string.IsNullOrEmpty(resultTokenTag)) go.tag = resultTokenTag;
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.SetAsLastSibling();

            var img = go.GetComponent<Image>();
            img.sprite = resolvedIcon;
            img.preserveAspect = true;
            img.raycastTarget = true;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = resultTokenSize;
            rt.localScale = sizeByScale ? Vector3.one * resultTokenScale : Vector3.one;
            rt.anchoredPosition = resultSpawnAnchoredPos;

            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

            go.AddComponent<DraggableItem>();
        }

        // Rebind hover baseline after scaling (prevents pop-to-giant/small on hover/drag)
        RebindHoverScaleOn(go);

        spawnedToken = go;

        // If your prefab/highlight script wants initialization data, it can implement this:
        var init = go.GetComponent<IResultTokenInit>();
        if (init != null) init.Init(resultItem, resolvedIcon);

        if (verbose)
            Debug.Log($"[SnakeWorldMini] Result token spawned from {(resultTokenPrefab ? "prefab" : "builder")} at {resultSpawnAnchoredPos}.");
    }

    private void RebindHoverScaleOn(GameObject go)
    {
        var highlighters = go.GetComponentsInChildren<UIHoverHighlighter>(true);
        foreach (var hh in highlighters) if (hh) hh.SetBaseScaleToCurrent();
    }

    public void HandleResultDrop(GameObject token)
    {
        // UI take sound
        PlayRandomOneShotCategory(uiAudio, takeClips, takeVolumeRange, takePitchRange, SfxCategory.Take);

        // consume UI token
        var di = token.GetComponent<DraggableItem>();
        if (di) di.Consume(); else token.SetActive(false);

        // equip the item
        var eq = EquipmentInventory.Instance;
        if (eq && resultItem)
        {
            bool ok = eq.TryEquip(eq.activeHand, resultItem) || eq.TryEquipToFirstAvailable(resultItem);
            if (!ok) Debug.LogWarning("[SnakeWorldMini] Inventory full; could not equip result.");
        }
        else Debug.LogWarning("[SnakeWorldMini] Missing EquipmentInventory or resultItem.");

        // ✅ mark once-only here (SUCCESS path) if we have a valid id
        if (onceOnly && !string.IsNullOrEmpty(stationId)) WorkstationOnce.MarkUsed(stationId);

        onSuccess?.Invoke();
        CloseUI();
    }

    public void CloseUI()
    {
        onClosed?.Invoke();

        StopSlitherLoop();
        sessionActive = false;

        if (disableInsteadOfDestroy)
        {
            if (owningRoot) owningRoot.SetActive(false);
            else if (owningCanvas) owningCanvas.gameObject.SetActive(false);
            else gameObject.SetActive(false);
        }
        else
        {
            if (owningRoot) Destroy(owningRoot);
            else if (owningCanvas) Destroy(owningCanvas.gameObject);
            else Destroy(gameObject);
        }
    }

    public void CancelAndClose() => CloseUI();

    // ------------------- PUBLIC HOOKS -------------------
    /// <summary>Call this when the snake eats a seed (from SnakePhysicsController or DropSurface).</summary>
    public void NotifySnakeAte()
    {
        if (!sessionActive || completed) return;
        PlayRandomOneShotCategory(snakeAudio, eatClips, eatVolumeRange, eatPitchRange, SfxCategory.Eat);
    }

    // ------------------- SLITHER LOOP -------------------
    private void StartSlitherLoop()
    {
        if (!enableSlitherLoop) return;
        if (slitherCo != null) StopCoroutine(slitherCo);
        slitherCo = StartCoroutine(CoSlither());
    }

    private void StopSlitherLoop()
    {
        if (slitherCo != null)
        {
            StopCoroutine(slitherCo);
            slitherCo = null;
        }
    }

    private IEnumerator CoSlither()
    {
        while (sessionActive && !completed && isActiveAndEnabled)
        {
            // wait random interval
            float delay = UnityEngine.Random.Range(
                Mathf.Max(0.02f, slitherIntervalSeconds.x),
                Mathf.Max(slitherIntervalSeconds.x, slitherIntervalSeconds.y));
            yield return new WaitForSeconds(delay);

            // play a random slither
            PlayRandomOneShotCategory(snakeAudio, slitherClips, slitherVolumeRange, slitherPitchRange, SfxCategory.Slither);
        }
    }

    // ------------------- AUDIO CORE -------------------
    private void PlayRandomOneShotCategory(AudioSource src, AudioClip[] clips, Vector2 volRange, Vector2 pitchRange, SfxCategory cat)
    {
        if (!src || clips == null || clips.Length == 0) return;

        int last = GetLastIndex(cat);
        int idx = PickIndex(clips.Length, last, avoidImmediateRepeat);
        SetLastIndex(cat, idx);

        var clip = clips[idx];
        if (!clip) return;

        float vol = Mathf.Clamp01(UnityEngine.Random.Range(volRange.x, volRange.y));
        float pitch = Mathf.Clamp(UnityEngine.Random.Range(pitchRange.x, pitchRange.y), 0.01f, 3f);

        float oldPitch = src.pitch;
        src.pitch = pitch;
        src.PlayOneShot(clip, vol);
        src.pitch = oldPitch;
    }

    private int GetLastIndex(SfxCategory cat) => cat switch
    {
        SfxCategory.Slither => _lastSlither,
        SfxCategory.Eat => _lastEat,
        SfxCategory.Complete => _lastComplete,
        SfxCategory.Take => _lastTake,
        _ => -1
    };

    private void SetLastIndex(SfxCategory cat, int idx)
    {
        switch (cat)
        {
            case SfxCategory.Slither: _lastSlither = idx; break;
            case SfxCategory.Eat: _lastEat = idx; break;
            case SfxCategory.Complete: _lastComplete = idx; break;
            case SfxCategory.Take: _lastTake = idx; break;
        }
    }

    private int PickIndex(int length, int last, bool avoidRepeat)
    {
        if (length <= 0) return 0;
        if (!avoidRepeat || length == 1 || last < 0) return UnityEngine.Random.Range(0, length);

        int idx;
        do { idx = UnityEngine.Random.Range(0, length); }
        while (idx == last && length > 1);
        return idx;
    }
}

// Optional initializer contract your highlight/selection script can implement
public interface IResultTokenInit
{
    void Init(ItemSO item, Sprite icon);
}
