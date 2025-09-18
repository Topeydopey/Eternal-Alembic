// Assets/Scripts/Minigame V2/SnakeWorldMinigame.cs
// Unity 6.2 • Universal 2D • Input System
// Spawns your prefab for the result token (preserves highlight scripts), with optional UI overrides.
// Supports "once only" stations with PlayerPrefs persistence and a success event.

using System;
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

    [Header("Debug")]
    public bool verbose = false;

    private GameObject spawnedToken;

    void Awake()
    {
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;
        if (!resultTokenSpawnParent && owningCanvas) resultTokenSpawnParent = owningCanvas.transform;
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

        // 3) spawn UI token
        SpawnResultToken();
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
}

// Optional initializer contract your highlight/selection script can implement
public interface IResultTokenInit
{
    void Init(ItemSO item, Sprite icon);
}
