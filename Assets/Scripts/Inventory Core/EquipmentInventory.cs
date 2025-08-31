using System;
using UnityEngine;

public class EquipmentInventory : MonoBehaviour
{
    public static EquipmentInventory Instance { get; private set; }

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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public EquipmentSlot Get(EquipmentSlotType t) => t switch
    {
        EquipmentSlotType.LeftHand => leftHand,
        EquipmentSlotType.RightHand => rightHand,
        EquipmentSlotType.PocketL => pocketL,
        EquipmentSlotType.PocketR => pocketR,
        _ => null
    };

    // Prefer active hand if empty, then the other hand, then pockets.
    public bool TryEquipToFirstAvailable(ItemSO item)
    {
        if (item == null) return false;

        // Build an order that prefers the current active hand
        EquipmentSlotType[] order = (activeHand == EquipmentSlotType.LeftHand)
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
        return false; // nowhere to put it
    }

    public void SetActiveHand(EquipmentSlotType hand)
    {
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

    // Move or swap between two equipment slots
    public bool MoveOrSwap(EquipmentSlotType from, EquipmentSlotType to)
    {
        if (from == to) return false;
        var a = Get(from);
        var b = Get(to);
        if (a == null || b == null || a.IsEmpty) return false;

        var itemA = a.item;
        var itemB = b.item;

        // Move into empty if accepted
        if (b.IsEmpty)
        {
            if (!b.Accepts(itemA)) return false;
            b.item = itemA;
            a.item = null;
            OnChanged?.Invoke();
            return true;
        }

        // Swap if both sides accept each other
        if (b.Accepts(itemA) && a.Accepts(itemB))
        {
            a.item = itemB;
            b.item = itemA;
            OnChanged?.Invoke();
            return true;
        }

        return false;
    }

    // Toggle active hand (does NOT swap items)
    public void ToggleActiveHand()
    {
        activeHand = (activeHand == EquipmentSlotType.LeftHand)
            ? EquipmentSlotType.RightHand
            : EquipmentSlotType.LeftHand;
        OnChanged?.Invoke();
    }

    // Drop an item from a slot to the world (spawns a Pickup)
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

    // Convenience: drop from active hand
    public bool DropActive(Vector3 worldPosition) => Drop(activeHand, worldPosition);
}
