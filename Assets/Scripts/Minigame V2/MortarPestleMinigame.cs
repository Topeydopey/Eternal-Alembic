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
    [Tooltip("Prefab used when spawning a NEW plant token.")]
    public GameObject deadPlantPrefab;
    [Tooltip("If assigned AND reuseScenePlantInstance = true, this disabled scene object will be activated instead of instantiating a clone.")]
    public GameObject deadPlantSceneInstance;
    [Tooltip("Parent to hold the plant token UI.")]
    public Transform plantSpawnParent;
    [Tooltip("Optional anchor to place the plant token. Must share the same parent space as the spawned token.")]
    public RectTransform plantSpawnAnchor;
    [Tooltip("Fallback anchored position if no anchor is given.")]
    public Vector2 plantSpawnAnchoredPos = Vector2.zero;
    [Tooltip("If true and a scene instance is assigned, we will activate/reuse it. Otherwise we always instantiate the prefab.")]
    public bool reuseScenePlantInstance = true;
    [Tooltip("Tag name expected by your DropSlot on the mortar. Must exist in Project Tags.")]
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
    [Tooltip("Trigger on the pot animator that runs the entire sequence (pour + growth).")]
    [SerializeField] private string potFullSequenceTrigger = "FullPourAndGrow";
    [Tooltip("Fallback duration if we can’t read the current clip length.")]
    [SerializeField] private float potFullSequenceSeconds = 1.2f;

    [Tooltip("Optional Animator state TAG on the pot's full sequence state. If set, we'll wait until this state finishes.")]
    [SerializeField] private string potFullStateTag = "FullPourAndGrow";
    [Tooltip("Safety timeout in case the animator never reaches the tagged state or never finishes.")]
    [SerializeField, Min(0.25f)] private float potSequenceWaitTimeout = 3f;
    [Tooltip("Small extra delay after the state completes before we spawn the plant.")]
    [SerializeField, Min(0f)] private float potSequenceEndExtraWait = 0.05f;

    // Set by an Animation Event at the end of the pot's sequence (optional)
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

    // State
    private bool busy;               // blocks re-entrant sequences
    private bool potionPoured;
    private bool plantSpawned;
    private bool mortarFilled;
    private bool grinding;
    private bool resultReady;

    private GameObject spawnedResultToken;
    private GameObject activePlantToken;     // <- track the actual plant instance we activated/spawned

    void Awake()
    {
        Instance = this;

        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;

        mortarHighlighter = mortarImage ? mortarImage.GetComponent<UIHoverHighlighter>() : null;
        mortarClickable = mortarPestle ? mortarPestle.GetComponent<MortarClickable>() : null;
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

        // Ensure the scene instance stays disabled until we need it
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

        // Immediately hide/consume the potion token — the pot animation will visually show the bottle emptying.
        ConsumeUIItem(potionToken);

        if (potAnimator && !string.IsNullOrEmpty(potFullSequenceTrigger))
        {
            potSequenceDoneFlag = false;

            potAnimator.ResetTrigger(potFullSequenceTrigger);
            potAnimator.SetTrigger(potFullSequenceTrigger);

            // Let Animator update at least once
            yield return null;

            float t = 0f;
            bool started = false;

            while (t < potSequenceWaitTimeout)
            {
                if (!potAnimator) break;

                var st = potAnimator.GetCurrentAnimatorStateInfo(0);
                bool inTarget =
                    string.IsNullOrEmpty(potFullStateTag) || st.IsTag(potFullStateTag);

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
            if (potFullSequenceSeconds > 0f)
                yield return new WaitForSeconds(potFullSequenceSeconds);
        }

        potionPoured = true;
        SpawnDeadPlant();   // <- activates or instantiates + positions + makes draggable

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
            yield return null;
            var stm = mortarAnimator.GetCurrentAnimatorStateInfo(0);
            waitMortar = stm.length > 0.05f ? stm.length : mortarGrindAnimSeconds;
        }
        else
        {
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
            yield return null;
            var st = mortarAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.length > 0.05f) wait = st.length;
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

            // ensure parent if provided
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

        // Ensure UI components exist & are visible
        var rt = plant.GetComponent<RectTransform>() ?? plant.AddComponent<RectTransform>();
        var img = plant.GetComponent<Image>();
        if (!img) Debug.LogWarning("[MortarPestle] Spawned plant has no Image; add one for UI visibility.");

        var cg = plant.GetComponent<CanvasGroup>() ?? plant.AddComponent<CanvasGroup>();
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

        // Position in UI
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

        // Ensure draggable + correct tag
        if (!plant.GetComponent<DraggableItem>()) plant.AddComponent<DraggableItem>();
        TrySetTag(plant, plantTag);

        plant.SetActive(true);
        plant.transform.SetAsLastSibling();

        activePlantToken = plant;
        plantSpawned = true;
        // Debug.Log("[MortarPestle] Plant spawned/activated.");
    }

    private void TrySetTag(GameObject go, string tagName)
    {
        if (!go || string.IsNullOrEmpty(tagName)) return;
        try
        {
            go.tag = tagName; // Will throw if tag isn't defined in Project Settings > Tags and Layers
        }
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
        if (drag) { drag.Consume(); }
        else { go.SetActive(false); }

        if (go == growthPotion && growthPotion) growthPotion.SetActive(false);
    }

    // -------- Animation Events --------
    public void AE_PourFinished_SpawnPlant()
    {
        if (!plantSpawned) SpawnDeadPlant();
    }

    public void AE_PotFullSequence_Done()
    {
        potSequenceDoneFlag = true;
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
}
