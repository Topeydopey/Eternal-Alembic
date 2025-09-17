// SnakeWorldMinigame.cs
using System;
using UnityEngine;
using UnityEngine.UI;

public class SnakeWorldMinigame : MonoBehaviour
{
    public event Action onClosed;

    [Header("Owns / Canvas")]
    public Canvas owningCanvas;
    public GameObject owningRoot;
    public bool disableInsteadOfDestroy = true;

    [Header("Snake (World)")]
    public SnakePhysicsController snakeHead;   // required
    [Tooltip("Optional parent that contains head+body visuals to hide on complete.")]
    public GameObject snakeRoot;

    [Header("Seed Drop Surface (UI)")]
    public SeedDropSlotWorld dropSurface;      // disable when completed

    [Header("Result Token (UI)")]
    public ItemSO resultItem;                  // assign your ItemSO
    public GameObject resultTokenPrefab;       // Image + CanvasGroup + DraggableItem; tag="Result"
    public DropSlotResult takeZone;            // acceptsTag="Result"
    public Transform resultTokenSpawnParent;   // default: owningCanvas.transform
    public Vector2 resultSpawnAnchoredPos = new Vector2(0, -80); // visible spot
    public Sprite resultIconOverride;          // optional

    [Header("Win Condition")]
    [Tooltip("-1 = use SnakePhysicsController value")]
    public int seedsToWinOverride = -1;

    [Header("Debug")]
    public bool verbose = false;

    private GameObject spawnedToken;
    // --- Result token sizing ---
    [SerializeField] private Vector2 resultTokenSize = new Vector2(100, 100); // pixels
    [SerializeField] private bool sizeByScale = true;     // if true, also scale the RectTransform
    [SerializeField]
    [Range(0.05f, 5f)]
    private float resultTokenScale = 0.2f;                   // e.g. 0.2 to match your prefab look

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
        BeginSession();
    }

    void OnDisable()
    {
        if (snakeHead) snakeHead.OnCompleted -= OnCompleted;
    }

    public void BeginSession()
    {
        snakeHead.ResetSession(seedsToWinOverride);
        snakeHead.SetDriveMode(SnakePhysicsController.DriveMode.PlayerSteer);
        snakeHead.SetFrozen(false);

        if (verbose) Debug.Log("[SnakeWorldMini] BeginSession");

        // Reset the physics head and optionally override required count
        snakeHead.ResetSession(seedsToWinOverride);

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

    // SnakeWorldMinigame.cs
    private void SpawnResultToken()
    {
        if (!owningCanvas)
        {
            Debug.LogError("[SnakeWorldMini] No owningCanvas; cannot spawn UI token.");
            return;
        }

        var parent = resultTokenSpawnParent ? resultTokenSpawnParent : owningCanvas.transform;

        // Resolve a sprite (override -> prefab Image -> prefab SpriteRenderer)
        Sprite icon = resultIconOverride;
        if (!icon && resultTokenPrefab)
        {
            var pImg = resultTokenPrefab.GetComponent<Image>();
            if (pImg && pImg.sprite) icon = pImg.sprite;
            if (!icon)
            {
                var pSr = resultTokenPrefab.GetComponent<SpriteRenderer>();
                if (pSr && pSr.sprite) icon = pSr.sprite;
            }
        }

        // Build a clean UI token
        var go = new GameObject("ResultToken (UI)", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.tag = "Result";
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = true;

        var rt = go.GetComponent<RectTransform>();
        // Normalize anchors/pivot
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // Apply sizing
        rt.sizeDelta = resultTokenSize;              // base pixel size (Canvas Scaler will handle DPI)
        rt.localScale = sizeByScale ? Vector3.one * resultTokenScale : Vector3.one;
        // Position
        rt.anchoredPosition = resultSpawnAnchoredPos; // e.g. (0,-80) in your inspector

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;

        go.AddComponent<DraggableItem>(); // your drag script (make sure it does NOT force scale=1)
        spawnedToken = go;

        if (verbose) Debug.Log($"[SnakeWorldMini] Result token spawned. size={resultTokenSize}, scale={(sizeByScale ? resultTokenScale : 1f)}");
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
