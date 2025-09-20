// Assets/Scripts/Minigame V2/MortarPestle/MortarPestleMinigame.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MortarPestleMinigame : MonoBehaviour
{
    public static MortarPestleMinigame Instance;

    [Header("Closing/Owner")]
    [SerializeField] public Canvas owningCanvas;
    [SerializeField] public GameObject owningRoot;
    [SerializeField] private bool disableInsteadOfDestroy = false;
    public event Action onClosed;
    public event Action onSucceeded;

    [Header("References")]
    [Tooltip("UI token for Growth Potion (tag='Potion') placed in the UI.")]
    public GameObject growthPotion;       // tag = "Potion"
    [Tooltip("UI area that accepts the potion drop (DropSlot acceptsTag='Potion').")]
    public GameObject plantPot;

    [Header("Plant (Remnant)")]
    public GameObject deadPlantPrefab;
    public GameObject deadPlantSceneInstance;
    public Transform plantSpawnParent;
    public RectTransform plantSpawnAnchor;
    public Vector2 plantSpawnAnchoredPos = Vector2.zero;
    public bool reuseScenePlantInstance = true;
    public string plantTag = "Plant";

    [Tooltip("Clickable mortar+pestle root; a separate MortarClickable is attached to it.")]
    public GameObject mortarPestle;
    [Tooltip("Image that swaps between empty/filled/result mortar sprites.")]
    public Image mortarImage;

    [Header("Mortar Sprites (fallback visuals)")]
    public Sprite mortarEmptySprite;
    public Sprite mortarFilledSprite;
    public Sprite mortarResultSprite;

    [Header("Mortar Highlight Sprites (for UIHoverHighlighter)")]
    public Sprite mortarEmptyHighlight;
    public Sprite mortarFilledHighlight;
    public Sprite mortarResultHighlight;

    private UIHoverHighlighter mortarHighlighter; // cached
    private MortarClickable mortarClickable;       // cached

    [Header("Result (Inventory)")]
    public ItemSO resultItem;
    public GameObject resultTokenPrefab;      // UI prefab: Image + CanvasGroup + DraggableItem, Tag="Result"
    public Transform resultTokenSpawnParent;  // near the mortar is fine
    public DropSlot takeZone;                 // acceptsTag="Result"
    public Sprite resultIconOverride;

    [Header("Result Token Sizing/Placement")]
    [SerializeField] private Vector2 resultTokenSize = new Vector2(96, 96);
    [SerializeField] private bool resultSizeByScale = false;
    [SerializeField] private float resultTokenScale = 1f;
    [SerializeField] private Vector2 resultTokenSpawnPos = Vector2.zero;

    // -------- Pot unified sequence (potion pour + tree grow in one clip) --------
    [Header("Pot Sequence (single animation clip)")]
    [SerializeField] private Animator potAnimator;
    [SerializeField] private string potFullSequenceTrigger = "FullPourAndGrow";
    [SerializeField] private float potFullSequenceSeconds = 1.2f;
    [SerializeField] private string potFullStateTag = "FullPourAndGrow";
    [SerializeField, Min(0.25f)] private float potSequenceWaitTimeout = 3f;
    [SerializeField, Min(0f)] private float potSequenceEndExtraWait = 0.05f;
    private bool potSequenceDoneFlag = false;

    [Header("Mortar Animation")]
    [SerializeField] private Animator mortarAnimator;
    [SerializeField] private string mortarGrindTrigger = "Grind";
    [SerializeField] private float mortarGrindAnimSeconds = 0.6f;

    [Header("Legacy Timing (still supported)")]
    public float grindDuration = 0f;

    [Header("UX Options")]
    [SerializeField] private bool disableMortarHoverAfterResult = true;
    [SerializeField] private bool disableMortarRaycastAfterResult = true;

    // ===================== AUDIO =====================
    [Header("Audio Sources")]
    [SerializeField] private AudioSource potAudio;    // pour + tree
    [SerializeField] private AudioSource mortarAudio; // grinding
    [SerializeField] private AudioSource uiAudio;     // take

    [Header("SFX: Potion → Pot")]
    [SerializeField] private AudioClip[] pourClips;
    [SerializeField] private Vector2 pourPitchRange = new Vector2(0.98f, 1.03f);
    [SerializeField] private Vector2 pourVolumeRange = new Vector2(0.9f, 1.0f);

    [SerializeField] private AudioClip[] treeClips;
    [SerializeField] private float treeSfxDelay = 0.18f;
    [SerializeField] private Vector2 treePitchRange = new Vector2(0.98f, 1.03f);
    [SerializeField] private Vector2 treeVolumeRange = new Vector2(0.9f, 1.0f);

    [Header("SFX: Mortar Grind")]
    [SerializeField] private AudioClip[] grindClips;
    [SerializeField] private Vector2 grindPitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private Vector2 grindVolumeRange = new Vector2(0.9f, 1.0f);

    [Header("SFX: Take from TakeZone")]
    [SerializeField] private AudioClip[] takeClips;
    [SerializeField] private Vector2 takePitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 takeVolumeRange = new Vector2(0.9f, 1.0f);

    [Header("Audio Options")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    private enum SfxCategory { Pour, Tree, Grind, Take }
    private int _lastPour = -1, _lastTree = -1, _lastGrind = -1, _lastTake = -1;

    // ----------------- State -----------------
    private bool busy;
    private bool potionPoured;
    private bool plantSpawned;
    private bool mortarFilled;
    private bool grinding;
    private bool resultReady;

    private GameObject spawnedResultToken;
    private GameObject activePlantToken;

    void Awake()
    {
        Instance = this;

        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;

        mortarHighlighter = mortarImage ? mortarImage.GetComponent<UIHoverHighlighter>() : null;
        mortarClickable = mortarPestle ? mortarPestle.GetComponent<MortarClickable>() : null;

        potAudio = EnsureAudioSource(potAudio, "PotAudio");
        mortarAudio = EnsureAudioSource(mortarAudio, "MortarAudio");
        uiAudio = EnsureAudioSource(uiAudio, "UIAudio");
    }

    private AudioSource EnsureAudioSource(AudioSource src, string childName)
    {
        if (src) return src;
        var child = transform.Find(childName);
        if (child && child.TryGetComponent<AudioSource>(out var found)) return found;

        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f; // 2D
        return a;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------
    // Helpers
    // ---------------------------

    private void ApplyMortarVisual(Sprite baseSprite, Sprite highlightSprite, bool resetHighlightFade = false)
    {
        if (mortarImage && baseSprite) mortarImage.sprite = baseSprite;
        if (mortarHighlighter && highlightSprite) mortarHighlighter.SetHighlightSprite(highlightSprite, resetHighlightFade);
    }

    private void SetMortarInteractive(bool on)
    {
        if (mortarHighlighter)
        {
            var overlayTf = mortarImage ? mortarImage.transform.Find("HighlightOverlay") : null;
            if (overlayTf)
            {
                var cg = overlayTf.GetComponent<CanvasGroup>();
                if (cg) cg.alpha = 0f;
            }
            mortarHighlighter.enabled = on;
        }

        if (mortarClickable)
            mortarClickable.enabled = on;

        if (mortarImage && disableMortarRaycastAfterResult)
            mortarImage.raycastTarget = on;
    }

    // ---------------------------
    // Reuse-mode helpers
    // ---------------------------

    public void SetReuseMode(bool reuse, GameObject root = null)
    {
        disableInsteadOfDestroy = reuse;
        if (root) owningRoot = root;
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
    }

    public void BeginSession()
    {
        busy = false;
        potionPoured = false;
        plantSpawned = false;
        mortarFilled = false;
        grinding = false;
        resultReady = false;
        potSequenceDoneFlag = false;

        activePlantToken = null;

        ApplyMortarVisual(mortarEmptySprite, mortarEmptyHighlight, resetHighlightFade: true);
        SetMortarInteractive(true);

        if (growthPotion) growthPotion.SetActive(true);

        if (spawnedResultToken)
        {
            Destroy(spawnedResultToken);
            spawnedResultToken = null;
        }

        if (plantSpawnParent)
        {
            for (int i = plantSpawnParent.childCount - 1; i >= 0; i--)
            {
                var child = plantSpawnParent.GetChild(i);
                if (child && (child.CompareTag(plantTag) || child.name.Contains("DeadPlant")))
                    Destroy(child.gameObject);
            }
        }

        if (deadPlantSceneInstance && reuseScenePlantInstance)
            deadPlantSceneInstance.SetActive(false);
    }

    // ---------------------------
    // Gameplay
    // ---------------------------

    public void HandleDrop(DropSlot slot, GameObject item)
    {
        if (busy) return;
        var t = item.tag;

        // Potion -> Pot
        if (!potionPoured && slot.acceptsTag == "Potion" && t == "Potion")
        {
            StartCoroutine(PotionIntoPot_FullPotSequence(item));
            return;
        }

        // Plant -> Mortar (auto-start grind)
        if (slot.acceptsTag == plantTag && t == plantTag)
        {
            StartCoroutine(PlantToMortarGrindSequence(item));
            return;
        }

        // Result -> TakeZone
        if (slot.acceptsTag == "Result" && t == "Result")
        {
            OnResultTaken(item);
            return;
        }
    }

    // --------- Potion → Pot unified sequence (pot anim shows pour + growth) ----------
    private IEnumerator PotionIntoPot_FullPotSequence(GameObject potionToken)
    {
        busy = true;

        // Hide/consume potion token — pot anim shows bottle emptying.
        ConsumeUIItem(potionToken);

        if (potAnimator && !string.IsNullOrEmpty(potFullSequenceTrigger))
        {
            potSequenceDoneFlag = false;

            potAnimator.ResetTrigger(potFullSequenceTrigger);
            potAnimator.SetTrigger(potFullSequenceTrigger);

            // Let Animator update at least once
            yield return null;

            // AUDIO: pour now, tree after small delay
            PlayRandomOneShotCategory(potAudio, pourClips, pourVolumeRange, pourPitchRange, SfxCategory.Pour);
            if (treeClips != null && treeClips.Length > 0 && treeSfxDelay >= 0f)
                StartCoroutine(PlayDelayed(potAudio, treeClips, treeVolumeRange, treePitchRange, SfxCategory.Tree, treeSfxDelay));

            float t = 0f;
            bool started = false;

            while (t < potSequenceWaitTimeout)
            {
                if (!potAnimator) break;

                var st = potAnimator.GetCurrentAnimatorStateInfo(0);
                bool inTarget = string.IsNullOrEmpty(potFullStateTag) || st.IsTag(potFullStateTag);

                if (inTarget)
                {
                    started = true;
                    if (potSequenceDoneFlag || (st.normalizedTime >= 1f && !potAnimator.IsInTransition(0)))
                        break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            if (!started && potFullSequenceSeconds > 0f)
                yield return new WaitForSeconds(potFullSequenceSeconds);

            if (potSequenceEndExtraWait > 0f)
                yield return new WaitForSeconds(potSequenceEndExtraWait);
        }
        else
        {
            // No animator: still do SFX timing
            PlayRandomOneShotCategory(potAudio, pourClips, pourVolumeRange, pourPitchRange, SfxCategory.Pour);
            if (treeClips != null && treeClips.Length > 0 && treeSfxDelay >= 0f)
                StartCoroutine(PlayDelayed(potAudio, treeClips, treeVolumeRange, treePitchRange, SfxCategory.Tree, treeSfxDelay));

            if (potFullSequenceSeconds > 0f)
                yield return new WaitForSeconds(potFullSequenceSeconds);
        }

        potionPoured = true;
        SpawnDeadPlant();

        potSequenceDoneFlag = false;
        busy = false;
    }

    // Back-compat click (still supported if you keep MortarClickable)
    public void OnMortarClicked()
    {
        if (busy || grinding || !mortarFilled) return;
        StartCoroutine(GrindSequence());
    }

    // --------- Plant → Mortar full sequence ----------
    private IEnumerator PlantToMortarGrindSequence(GameObject plantToken)
    {
        busy = true;

        mortarFilled = true;
        ApplyMortarVisual(mortarFilledSprite, mortarFilledHighlight);

        float waitMortar = 0f;
        if (mortarAnimator && !string.IsNullOrEmpty(mortarGrindTrigger))
        {
            mortarAnimator.ResetTrigger(mortarGrindTrigger);
            mortarAnimator.SetTrigger(mortarGrindTrigger);

            // AUDIO: grind start
            PlayRandomOneShotCategory(mortarAudio, grindClips, grindVolumeRange, grindPitchRange, SfxCategory.Grind);

            yield return null;
            var stm = mortarAnimator.GetCurrentAnimatorStateInfo(0);
            waitMortar = stm.length > 0.05f ? stm.length : mortarGrindAnimSeconds;
        }
        else
        {
            PlayRandomOneShotCategory(mortarAudio, grindClips, grindVolumeRange, grindPitchRange, SfxCategory.Grind);
            waitMortar = mortarGrindAnimSeconds;
        }

        if (grindDuration > 0f) waitMortar += grindDuration;
        if (waitMortar > 0f) yield return new WaitForSeconds(waitMortar);

        // Consume plant and spawn result
        ConsumeUIItem(plantToken);
        ApplyMortarVisual(mortarResultSprite, mortarResultHighlight);
        SpawnResultToken();

        if (disableMortarHoverAfterResult)
            SetMortarInteractive(false);

        grinding = false;
        resultReady = true;
        busy = false;
    }

    // Original click-driven grind (kept)
    private IEnumerator GrindSequence()
    {
        grinding = true;

        float wait = mortarGrindAnimSeconds;
        if (mortarAnimator)
        {
            mortarAnimator.ResetTrigger(mortarGrindTrigger);
            mortarAnimator.SetTrigger(mortarGrindTrigger);

            // AUDIO
            PlayRandomOneShotCategory(mortarAudio, grindClips, grindVolumeRange, grindPitchRange, SfxCategory.Grind);

            yield return null;
            var st = mortarAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.length > 0.05f) wait = st.length;
        }
        else
        {
            PlayRandomOneShotCategory(mortarAudio, grindClips, grindVolumeRange, grindPitchRange, SfxCategory.Grind);
        }

        if (grindDuration > 0f) wait += grindDuration;
        if (wait > 0f) yield return new WaitForSeconds(wait);

        ApplyMortarVisual(mortarResultSprite, mortarResultHighlight);
        SpawnResultToken();

        if (disableMortarHoverAfterResult)
            SetMortarInteractive(false);

        grinding = false;
        resultReady = true;
    }

    // -------------------- Spawning & Utils --------------------

    private void SpawnDeadPlant()
    {
        if (plantSpawned) return;

        var parent = plantSpawnParent ? plantSpawnParent : transform;
        GameObject plant = null;

        if (reuseScenePlantInstance && deadPlantSceneInstance)
        {
            plant = deadPlantSceneInstance;
            if (parent && plant.transform.parent != parent)
                plant.transform.SetParent(parent, worldPositionStays: false);
        }
        else
        {
            if (!deadPlantPrefab)
            {
                Debug.LogError("[MortarPestle] deadPlantPrefab not assigned.");
                return;
            }
            plant = Instantiate(deadPlantPrefab, parent);
        }

        var rt = plant.GetComponent<RectTransform>() ?? plant.AddComponent<RectTransform>();
        var img = plant.GetComponent<Image>();
        if (!img) Debug.LogWarning("[MortarPestle] Spawned plant has no Image; add one for UI visibility.");

        var cg = plant.GetComponent<CanvasGroup>() ?? plant.AddComponent<CanvasGroup>();
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

        if (plantSpawnAnchor && plantSpawnAnchor.parent == (parent as RectTransform))
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = plantSpawnAnchor.anchoredPosition;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = plantSpawnAnchoredPos;
        }

        if (!plant.GetComponent<DraggableItem>()) plant.AddComponent<DraggableItem>();
        TrySetTag(plant, plantTag);

        plant.SetActive(true);
        plant.transform.SetAsLastSibling();

        activePlantToken = plant;
        plantSpawned = true;
    }

    private void TrySetTag(GameObject go, string tagName)
    {
        if (!go || string.IsNullOrEmpty(tagName)) return;
        try { go.tag = tagName; }
        catch (UnityException)
        {
            Debug.LogWarning($"[MortarPestle] Tag '{tagName}' is not defined. Please add it in Tags & Layers. Using '{go.tag}' instead.");
        }
    }

    private void SpawnResultToken()
    {
        if (!resultTokenPrefab)
        {
            Debug.LogError("[MortarPestle] resultTokenPrefab not assigned.");
            return;
        }

        var parent = resultTokenSpawnParent ? resultTokenSpawnParent : (Transform)owningCanvas?.transform ?? transform;
        GameObject token = null;
        var prefabHasRT = resultTokenPrefab.GetComponent<RectTransform>() != null;
        if (prefabHasRT)
        {
            token = Instantiate(resultTokenPrefab, parent);
        }
        else
        {
            token = new GameObject("ResultToken (UI)", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            token.transform.SetParent(parent, false);
            var img = token.GetComponent<Image>();
            Sprite icon = resultIconOverride ? resultIconOverride : mortarResultSprite;
            var pSr = resultTokenPrefab.GetComponent<SpriteRenderer>();
            if (!icon && pSr && pSr.sprite) icon = pSr.sprite;
            img.sprite = icon;
            img.preserveAspect = true;
            token.AddComponent<DraggableItem>();
        }

        token.SetActive(true);
        token.tag = "Result";
        token.transform.SetAsLastSibling();

        var rt = token.GetComponent<RectTransform>() ?? token.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = resultTokenSpawnPos;

        if (resultSizeByScale)
        {
            rt.sizeDelta = new Vector2(100, 100);
            rt.localScale = Vector3.one * Mathf.Max(0.0001f, resultTokenScale);
        }
        else
        {
            rt.sizeDelta = resultTokenSize;
            rt.localScale = Vector3.one;
        }

        var cg = token.GetComponent<CanvasGroup>() ?? token.AddComponent<CanvasGroup>();
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

        spawnedResultToken = token;
    }

    private void OnResultTaken(GameObject tokenGO)
    {
        // AUDIO
        PlayRandomOneShotCategory(uiAudio, takeClips, takeVolumeRange, takePitchRange, SfxCategory.Take);

        ConsumeUIItem(tokenGO);

        var eq = EquipmentInventory.Instance;
        if (eq && resultItem)
        {
            bool equipped = eq.TryEquip(eq.activeHand, resultItem) || eq.TryEquipToFirstAvailable(resultItem);
            if (!equipped) Debug.LogWarning("[MortarPestle] Inventory full; could not equip result.");
        }
        else Debug.LogWarning("[MortarPestle] No EquipmentInventory or resultItem not assigned.");

        onSucceeded?.Invoke();
        CloseUI();
    }

    private void ConsumeUIItem(GameObject go)
    {
        var drag = go.GetComponent<DraggableItem>();
        if (drag) { drag.Consume(); }  // <- FIX: just call Consume()
        else { go.SetActive(false); }

        if (go == growthPotion && growthPotion) growthPotion.SetActive(false);
    }

    // -------- Animation Events (optional) --------
    public void AE_PourFinished_SpawnPlant()
    {
        if (!plantSpawned) SpawnDeadPlant();
    }

    public void AE_PotFullSequence_Done()
    {
        potSequenceDoneFlag = true;
    }

    public void AE_PlayTreeSfx()
    {
        PlayRandomOneShotCategory(potAudio, treeClips, treeVolumeRange, treePitchRange, SfxCategory.Tree);
    }

    public void AE_PlayGrindSfx()
    {
        PlayRandomOneShotCategory(mortarAudio, grindClips, grindVolumeRange, grindPitchRange, SfxCategory.Grind);
    }

    public void AE_GrindFinished_SpawnResult()
    {
        if (!resultReady)
        {
            ApplyMortarVisual(mortarResultSprite, mortarResultHighlight);
            SpawnResultToken();

            if (disableMortarHoverAfterResult)
                SetMortarInteractive(false);

            resultReady = true;
            grinding = false;
            busy = false;
        }
    }

    // Closing
    public void CloseUI()
    {
        onClosed?.Invoke();

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

    public void ResetMortarVisual()
    {
        mortarFilled = false;
        ApplyMortarVisual(mortarEmptySprite, mortarEmptyHighlight, resetHighlightFade: true);
        resultReady = false;
        SetMortarInteractive(true);

        if (spawnedResultToken) { Destroy(spawnedResultToken); spawnedResultToken = null; }
    }

    // ===================== AUDIO HELPERS =====================

    private IEnumerator PlayDelayed(AudioSource src, AudioClip[] clips, Vector2 volRange, Vector2 pitchRange, SfxCategory cat, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        PlayRandomOneShotCategory(src, clips, volRange, pitchRange, cat);
    }

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
        SfxCategory.Pour => _lastPour,
        SfxCategory.Tree => _lastTree,
        SfxCategory.Grind => _lastGrind,
        SfxCategory.Take => _lastTake,
        _ => -1
    };

    private void SetLastIndex(SfxCategory cat, int idx)
    {
        switch (cat)
        {
            case SfxCategory.Pour: _lastPour = idx; break;
            case SfxCategory.Tree: _lastTree = idx; break;
            case SfxCategory.Grind: _lastGrind = idx; break;
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
