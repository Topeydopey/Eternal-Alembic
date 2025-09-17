using System;                     // for Action
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MortarPestleMinigame : MonoBehaviour
{
    public static MortarPestleMinigame Instance;

    [Header("Closing/Owner")]
    [SerializeField] private Canvas owningCanvas;     // assign the bottom canvas (minigame) OR auto-detect
    [SerializeField] private GameObject owningRoot;   // optional: a wrapper object to destroy instead of the whole Canvas
    [SerializeField] private bool disableInsteadOfDestroy = false; // <- reuse mode toggle (set via SetReuseMode)
    public event Action onClosed;                     // launcher can listen to clear its state

    [Header("References")]
    public GameObject growthPotion;       // tag = "Potion"
    public GameObject plantPot;           // DropSlot acceptsTag = "Potion"
    public GameObject deadPlantPrefab;    // will be tagged "Plant" when spawned
    public Transform plantSpawnParent;    // where the dead plant UI spawns
    public GameObject mortarPestle;       // DropSlot acceptsTag = "Plant"
    public Image mortarImage;             // Image on the mortar

    [Header("Mortar Sprites")]
    public Sprite mortarEmptySprite;
    public Sprite mortarFilledSprite;
    public Sprite mortarResultSprite;

    [Header("Result (Inventory)")]
    public ItemSO resultItem;                 // assign your ItemSO here
    public GameObject resultTokenPrefab;      // UI prefab: Image + CanvasGroup + DraggableItem, Tag="Result"
    public Transform resultTokenSpawnParent;  // near the mortar is fine
    public DropSlot takeZone;                 // bottom bar DropSlot (acceptsTag="Result")
    public Sprite resultIconOverride;         // optional: if your ItemSO doesn't have an icon

    [Header("Tuning")]
    public float grindDuration = 0f;          // keep 0 for instant

    // State
    private bool potionPoured;
    private bool plantSpawned;
    private bool mortarFilled;
    private bool grinding;
    private bool resultReady;
    private GameObject spawnedResultToken;
    [SerializeField] private Vector2 resultTokenSize = new Vector2(96, 96);
    [SerializeField] private Vector2 resultSpawnPos = Vector2.zero; // adjust in Inspector
    void Awake()
    {
        Instance = this;

        // Auto-detect the nearest parent Canvas if not assigned
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);

        // If you prefer to destroy a wrapper (e.g., "Minigame UI Prefab"), assign it.
        // Otherwise we’ll destroy the owning canvas GameObject.
        if (!owningRoot && owningCanvas) owningRoot = owningCanvas.gameObject;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------
    // Reuse-mode helpers
    // ---------------------------

    /// <summary>
    /// Call this when using an in-scene UI object you want to re-open/close without destroying.
    /// </summary>
    public void SetReuseMode(bool reuse, GameObject root = null)
    {
        disableInsteadOfDestroy = reuse;
        if (root) owningRoot = root;
        if (!owningCanvas) owningCanvas = GetComponentInParent<Canvas>(true);
    }

    /// <summary>
    /// Reset internal flags and visuals for a fresh round each time you open the UI.
    /// </summary>
    public void BeginSession()
    {
        // reset flags
        potionPoured = false;
        plantSpawned = false;
        mortarFilled = false;
        grinding = false;
        resultReady = false;

        // visuals
        if (mortarImage && mortarEmptySprite) mortarImage.sprite = mortarEmptySprite;

        // re-enable starting items (e.g., potion) if they were consumed last round
        if (growthPotion) growthPotion.SetActive(true);

        // clear any previously spawned plant/result tokens
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

    // Called by DropSlot when a valid item is dropped
    public void HandleDrop(DropSlot slot, GameObject item)
    {
        var t = item.tag;

        // Potion -> Pot
        if (!potionPoured && slot.acceptsTag == "Potion" && t == "Potion")
        {
            potionPoured = true;
            ConsumeUIItem(item);
            SpawnDeadPlant(); // instant (no animation)
            return;
        }

        // Plant -> Mortar
        if (slot.acceptsTag == "Plant" && t == "Plant")
        {
            ConsumeUIItem(item);
            mortarFilled = true;
            if (mortarImage && mortarFilledSprite) mortarImage.sprite = mortarFilledSprite;
            Debug.Log("[Minigame] Mortar filled. Click mortar to grind.");
            return;
        }

        // Result -> TakeZone (equip + close)
        if (slot.acceptsTag == "Result" && t == "Result")
        {
            OnResultTaken(item);
            return;
        }
    }

    public void OnMortarClicked()
    {
        if (!mortarFilled || grinding) return;
        StartCoroutine(GrindSequence());
    }

    private IEnumerator GrindSequence()
    {
        grinding = true;

        if (grindDuration > 0f)
            yield return new WaitForSeconds(grindDuration);

        // Show completed mortar visual
        if (mortarImage && mortarResultSprite) mortarImage.sprite = mortarResultSprite;

        // Spawn the draggable result token
        SpawnResultToken();

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

        // Instantiate prefab if it’s already a proper UI token, else build a clean one
        GameObject token = null;
        var prefabHasRT = resultTokenPrefab.GetComponent<RectTransform>() != null;
        if (prefabHasRT)
        {
            token = Instantiate(resultTokenPrefab, parent);
        }
        else
        {
            // World prefab accidentally assigned? Build a clean UI wrapper.
            token = new GameObject("ResultToken (UI)", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            token.transform.SetParent(parent, false);
            var img = token.GetComponent<Image>();

            // Try to pick a sprite: ItemSO icon (if you have one) -> override -> mortarResultSprite -> prefab SR
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

        // Normalize RT
        var rt = token.GetComponent<RectTransform>();
        if (!rt) rt = token.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;           // IMPORTANT: fixes “tiny” token
        rt.sizeDelta = resultTokenSize;        // visible default
        rt.anchoredPosition = resultSpawnPos;  // set in Inspector

        // Ensure CanvasGroup/raycast
        var cg = token.GetComponent<CanvasGroup>() ?? token.AddComponent<CanvasGroup>();
        cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true;
    }

    private void OnResultTaken(GameObject tokenGO)
    {
        // 1) Consume the token UI to prevent snap-back
        ConsumeUIItem(tokenGO);

        // 2) Equip to player hand (or first available)
        var eq = EquipmentInventory.Instance;
        if (eq && resultItem)
        {
            bool equipped = eq.TryEquip(eq.activeHand, resultItem) || eq.TryEquipToFirstAvailable(resultItem);
            if (!equipped)
            {
                Debug.LogWarning("[Minigame] Inventory full; could not equip result.");
                // Optional: drop to world or show message.
            }
        }
        else
        {
            Debug.LogWarning("[Minigame] No EquipmentInventory or resultItem not assigned.");
        }

        // 3) Close the minigame UI
        CloseUI();
    }

    private void ConsumeUIItem(GameObject go)
    {
        var drag = go.GetComponent<DraggableItem>();
        if (drag) drag.Consume();
        else go.SetActive(false);
    }

    // ---------------------------
    // Closing
    // ---------------------------

    public void CloseUI()
    {
        // Notify listeners (e.g., WorkstationLauncher) to clear references / re-enable HUD
        onClosed?.Invoke();

        if (disableInsteadOfDestroy)
        {
            // Reuse path: disable instead of destroy
            if (owningRoot) owningRoot.SetActive(false);
            else if (owningCanvas) owningCanvas.gameObject.SetActive(false);
            else gameObject.SetActive(false);
        }
        else
        {
            // Prefab path: destroy the UI
            if (owningRoot) Destroy(owningRoot);
            else if (owningCanvas) Destroy(owningCanvas.gameObject);
            else Destroy(gameObject);
        }
    }

    // Hook this to an X button or ESC
    public void CancelAndClose() => CloseUI();

    // If you need to replay later (manual reset utility)
    public void ResetMortarVisual()
    {
        mortarFilled = false;
        if (mortarImage && mortarEmptySprite) mortarImage.sprite = mortarEmptySprite;
        resultReady = false;
        if (spawnedResultToken) { Destroy(spawnedResultToken); spawnedResultToken = null; }
    }
}
