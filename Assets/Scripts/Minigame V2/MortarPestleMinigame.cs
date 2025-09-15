using System;                     // <-- needed for Action
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MortarPestleMinigame : MonoBehaviour
{
    public static MortarPestleMinigame Instance;

    [Header("Closing/Owner")]
    [SerializeField] private Canvas owningCanvas;     // assign the bottom canvas (minigame) OR auto-detect
    [SerializeField] private GameObject owningRoot;   // optional: a wrapper object to destroy instead of the whole Canvas
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
    public ItemSO resultItem;                 // <- assign your ItemSO here
    public GameObject resultTokenPrefab;      // <- UI prefab: Image + CanvasGroup + DraggableItem, Tag="Result"
    public Transform resultTokenSpawnParent;  // <- near the mortar is fine
    public DropSlot takeZone;                 // <- the bottom bar DropSlot (acceptsTag="Result")
    public Sprite resultIconOverride;         // <- optional: if your ItemSO doesn't have an icon

    [Header("Tuning")]
    public float grindDuration = 0f;          // keep 0 for instant

    private bool potionPoured;
    private bool plantSpawned;
    private bool mortarFilled;
    private bool grinding;
    private bool resultReady;
    private GameObject spawnedResultToken;

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

    // Drop dispatch from DropSlot
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

        if (grindDuration > 0f) yield return new WaitForSeconds(grindDuration);

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

        var parent = resultTokenSpawnParent ? resultTokenSpawnParent : transform;
        spawnedResultToken = Instantiate(resultTokenPrefab, parent);
        spawnedResultToken.SetActive(true);
        spawnedResultToken.tag = "Result";

        // Set token icon (ItemSO icon if available, else override, else mortarResultSprite)
        var img = spawnedResultToken.GetComponent<Image>();
        if (img)
        {
            Sprite icon = null;
            // If your ItemSO has an icon field (e.g., resultItem.icon), prefer it:
            // icon = resultItem ? resultItem.icon : null;
            icon = icon ?? (resultIconOverride ? resultIconOverride : mortarResultSprite);
            img.sprite = icon;
        }
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

    public void CloseUI()
    {
        // Notify listeners (e.g., WorkstationLauncher) to clear references
        onClosed?.Invoke();

        // Prefer destroying a wrapper root if assigned, else the owning canvas, else this object.
        if (owningRoot != null)
        {
            Destroy(owningRoot);
        }
        else if (owningCanvas != null)
        {
            Destroy(owningCanvas.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Hook this to an X button or ESC
    public void CancelAndClose() => CloseUI();

    // If you need to replay later:
    public void ResetMortarVisual()
    {
        mortarFilled = false;
        if (mortarImage && mortarEmptySprite) mortarImage.sprite = mortarEmptySprite;
        resultReady = false;
        if (spawnedResultToken) Destroy(spawnedResultToken);
    }
}
