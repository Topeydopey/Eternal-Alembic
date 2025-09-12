using System;
using UnityEngine;

public class EquipmentInventory : MonoBehaviour
{
    public static EquipmentInventory Instance { get; private set; }

    [Header("Mode")]
    public bool singleHandMode = true;   // <- NEW: true = only LeftHand is used

    [Header("Slots")]
    public EquipmentSlot leftHand = new() { type = EquipmentSlotType.LeftHand };
    public EquipmentSlot rightHand = new() { type = EquipmentSlotType.RightHand };
    public EquipmentSlot pocketL = new() { type = EquipmentSlotType.PocketL };
    public EquipmentSlot pocketR = new() { type = EquipmentSlotType.PocketR };

    [Header("Active Hand & Dropping")]
    public EquipmentSlotType activeHand = EquipmentSlotType.LeftHand;
    [Tooltip("Prefab with a Pickup component + SpriteRenderer + Collider2D")]
    public GameObject pickupPrefab;

    public event Action OnChanged;
    public bool MoveActiveTo(EquipmentSlotType to) => MoveOrSwap(activeHand, to);
    public bool MoveToActiveFrom(EquipmentSlotType from) => MoveOrSwap(from, activeHand);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (singleHandMode)
            activeHand = EquipmentSlotType.LeftHand; // hard-lock to left
    }

    public EquipmentSlot Get(EquipmentSlotType t)
    {
        if (singleHandMode && t == EquipmentSlotType.RightHand) return null; // disable right hand
        return t switch
        {
            EquipmentSlotType.LeftHand => leftHand,
            EquipmentSlotType.RightHand => rightHand,
            EquipmentSlotType.PocketL => pocketL,
            EquipmentSlotType.PocketR => pocketR,
            _ => null
        };
    }

    // Prefer active hand (Left), then pockets. Right hand ignored in single-hand mode.
    public bool TryEquipToFirstAvailable(ItemSO item)
    {
        if (item == null) return false;

        EquipmentSlotType[] order = singleHandMode
            ? new[] { EquipmentSlotType.LeftHand, EquipmentSlotType.PocketL, EquipmentSlotType.PocketR }
            : (activeHand == EquipmentSlotType.LeftHand)
                ? new[] { EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand, EquipmentSlotType.PocketL, EquipmentSlotType.PocketR }
                : new[] { EquipmentSlotType.RightHand, EquipmentSlotType.LeftHand, EquipmentSlotType.PocketL, EquipmentSlotType.PocketR };

        foreach (var t in order)
        {
            var slot = Get(t);
            if (slot != null && slot.IsEmpty && slot.Accepts(item))
            {
                slot.item = item;
                OnChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void SetActiveHand(EquipmentSlotType hand)
    {
        if (singleHandMode) { activeHand = EquipmentSlotType.LeftHand; OnChanged?.Invoke(); return; }
        if (hand == EquipmentSlotType.LeftHand || hand == EquipmentSlotType.RightHand)
        {
            activeHand = hand;
            OnChanged?.Invoke();
        }
    }

    public bool TryEquip(EquipmentSlotType slotType, ItemSO item)
    {
        var slot = Get(slotType);
        if (slot == null || !slot.Accepts(item)) return false;
        if (!slot.IsEmpty) return false;
        slot.item = item;
        OnChanged?.Invoke();
        return true;
    }

    public ItemSO Unequip(EquipmentSlotType slotType)
    {
        var slot = Get(slotType);
        if (slot == null || slot.IsEmpty) return null;
        var outItem = slot.item;
        slot.item = null;
        OnChanged?.Invoke();
        return outItem;
    }

    public bool MoveOrSwap(EquipmentSlotType from, EquipmentSlotType to)
    {
        if (from == to) return false;
        var a = Get(from);
        var b = Get(to);
        if (a == null || b == null || a.IsEmpty) return false;

        var itemA = a.item;
        var itemB = b.item;

        if (b.IsEmpty)
        {
            if (!b.Accepts(itemA)) return false;
            b.item = itemA;
            a.item = null;
            OnChanged?.Invoke();
            return true;
        }

        if (b.Accepts(itemA) && a.Accepts(itemB))
        {
            a.item = itemB;
            b.item = itemA;
            OnChanged?.Invoke();
            return true;
        }

        return false;
    }

    public void ToggleActiveHand()
    {
        if (singleHandMode)
        {
            activeHand = EquipmentSlotType.LeftHand; // no toggle
            OnChanged?.Invoke();
            return;
        }

        activeHand = (activeHand == EquipmentSlotType.LeftHand)
            ? EquipmentSlotType.RightHand
            : EquipmentSlotType.LeftHand;
        OnChanged?.Invoke();
    }

    public bool Drop(EquipmentSlotType from, Vector3 worldPosition)
    {
        var item = Unequip(from);
        if (item == null) return false;
        if (!pickupPrefab)
        {
            Debug.LogWarning("EquipmentInventory: No pickupPrefab assigned for dropping.");
            return false;
        }

        var go = Instantiate(pickupPrefab, worldPosition, Quaternion.identity);
        var pickup = go.GetComponent<Pickup>();
        if (pickup) { pickup.item = item; pickup.amount = 1; }
        else Debug.LogWarning("pickupPrefab has no Pickup component.");

        return true;
    }

    public bool DropActive(Vector3 worldPosition) => Drop(activeHand, worldPosition);
}
