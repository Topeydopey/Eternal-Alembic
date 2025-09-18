using System;                     // for Action
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MortarPestleMinigame : MonoBehaviour
{
    public static MortarPestleMinigame Instance;

    [Header("Closing/Owner")]
    [SerializeField] public Canvas owningCanvas;     // assign the bottom canvas (minigame) OR auto-detect
    [SerializeField] public GameObject owningRoot;   // optional: a wrapper object to destroy instead of the whole Canvas
    [SerializeField] private bool disableInsteadOfDestroy = false; // <- reuse mode toggle (set via SetReuseMode)
    public event Action onClosed;                     // launcher can listen to clear its state
    public event Action onSucceeded;                  // 🔔 NEW: fired when result is granted

    [Header("References")]
    [Tooltip("UI token for Growth Potion (tag='Potion') placed in the UI.")]
    public GameObject growthPotion;       // tag = "Potion"
    [Tooltip("UI area that accepts the potion drop (DropSlot acceptsTag='Potion').")]
    public GameObject plantPot;
    [Tooltip("Dead plant UI prefab that will be spawned (will be tagged 'Plant').")]
    public GameObject deadPlantPrefab;
    [Tooltip("Where the dead plant UI spawns (usually a container near the pot).")]
    public Transform plantSpawnParent;
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
    public ItemSO resultItem;                 // assign your ItemSO here
    public GameObject resultTokenPrefab;      // UI prefab: Image + CanvasGroup + DraggableItem, Tag="Result"
    public Transform resultTokenSpawnParent;  // near the mortar is fine
    public DropSlot takeZone;                 // bottom bar DropSlot (acceptsTag="Result")
    public Sprite resultIconOverride;         // optional: if your ItemSO doesn't have an icon

    [Header("Result Token Sizing/Placement")]
    [SerializeField] private Vector2 resultTokenSize = new Vector2(96, 96);
    [SerializeField] private bool resultSizeByScale = false;
    [SerializeField] private float resultTokenScale = 1f;
    [SerializeField] private Vector2 resultTokenSpawnPos = Vector2.zero;

    [Header("Animation (optional but recommended)")]
    [SerializeField] private Animator potAnimator;
    [SerializeField] private string potPourTrigger = "Pour";
    [SerializeField] private float potPourAnimSeconds = 0.6f;

    [Space(8)]
    [SerializeField] private Animator mortarAnimator;
    [SerializeField] private string mortarGrindTrigger = "Grind";
    [SerializeField] private float mortarGrindAnimSeconds = 0.6f;

    [Header("Legacy Timing (still supported)")]
    public float grindDuration = 0f;

    [Header("UX Options")]
    [SerializeField] private bool disableMortarHoverAfterResult = true;
    [SerializeField] private bool disableMortarRaycastAfterResult = true;

    // State
    private bool potionPoured;
    private bool plantSpawned;
    private bool mortarFilled;
    private bool grinding;
    private bool resultReady;

    private GameObject spawnedResultToken;

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
        potionPoured = false;
        plantSpawned = false;
        mortarFilled = false;
        grinding = false;
        resultReady = false;

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
                if (child && (child.CompareTag("Plant") || child.name.Contains("DeadPlant")))
                    Destroy(child.gameObject);
            }
        }
    }

    // ---------------------------
    // Gameplay
    // ---------------------------

    public void HandleDrop(DropSlot slot, GameObject item)
    {
        var t = item.tag;

        // Potion -> Pot
        if (!potionPoured && slot.acceptsTag == "Potion" && t == "Potion")
        {
            StartCoroutine(PotionToPotSequence(item));
            return;
        }

        // Plant -> Mortar
        if (slot.acceptsTag == "Plant" && t == "Plant")
        {
            ConsumeUIItem(item);
            mortarFilled = true;
            ApplyMortarVisual(mortarFilledSprite, mortarFilledHighlight);
            Debug.Log("[Mortar] Mortar filled. Click mortar to grind.");
            return;
        }

        // Result -> TakeZone
        if (slot.acceptsTag == "Result" && t == "Result")
        {
            OnResultTaken(item);
            return;
        }
    }

    private IEnumerator PotionToPotSequence(GameObject potionToken)
    {
        potionPoured = true;
        ConsumeUIItem(potionToken);

        float wait = potPourAnimSeconds;
        if (potAnimator)
        {
            potAnimator.ResetTrigger(potPourTrigger);
            potAnimator.SetTrigger(potPourTrigger);
            yield return null;
            var st = potAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.length > 0.05f) wait = st.length;
        }

        if (wait > 0f) yield return new WaitForSeconds(wait);

        SpawnDeadPlant();
    }

    public void OnMortarClicked()
    {
        if (!mortarFilled || grinding) return;
        StartCoroutine(GrindSequence());
    }

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

    private void SpawnDeadPlant()
    {
        if (plantSpawned) return;
        plantSpawned = true;

        var parent = plantSpawnParent ? plantSpawnParent : transform;
        var plant = Instantiate(deadPlantPrefab, parent);
        plant.SetActive(true);
        plant.tag = "Plant";

        if (!plant.GetComponent<DraggableItem>()) plant.AddComponent<DraggableItem>();
        if (!plant.GetComponent<Image>()) Debug.LogWarning("[Minigame] Spawned plant has no Image; add one.");
    }

    private void SpawnResultToken()
    {
        if (!resultTokenPrefab)
        {
            Debug.LogError("[Minigame] resultTokenPrefab not assigned.");
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
            if (!equipped) Debug.LogWarning("[Minigame] Inventory full; could not equip result.");
        }
        else Debug.LogWarning("[Minigame] No EquipmentInventory or resultItem not assigned.");

        // 🔔 Notify listeners (WorkstationLauncher will lock the station)
        onSucceeded?.Invoke();

        CloseUI();
    }

    private void ConsumeUIItem(GameObject go)
    {
        var drag = go.GetComponent<DraggableItem>();
        if (drag) drag.Consume();
        else go.SetActive(false);

        if (go == growthPotion && growthPotion) growthPotion.SetActive(false);
    }

    // Animation Events (optional)
    public void AE_PourFinished_SpawnPlant()
    {
        if (!plantSpawned) SpawnDeadPlant();
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
