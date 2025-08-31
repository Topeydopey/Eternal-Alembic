// EquipmentInventory.cs
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

    public bool TryEquipToFirstAvailable(ItemSO item)
    {
        // Prefer hands if empty; then pockets
        if (leftHand.Accepts(item) && leftHand.IsEmpty) { leftHand.item = item; OnChanged?.Invoke(); return true; }
        if (rightHand.Accepts(item) && rightHand.IsEmpty) { rightHand.item = item; OnChanged?.Invoke(); return true; }
        if (pocketL.Accepts(item) && pocketL.IsEmpty) { pocketL.item = item; OnChanged?.Invoke(); return true; }
        if (pocketR.Accepts(item) && pocketR.IsEmpty) { pocketR.item = item; OnChanged?.Invoke(); return true; }
        return false;
    }

    public void SwapHands()
    {
        (leftHand.item, rightHand.item) = (rightHand.item, leftHand.item);
        OnChanged?.Invoke();
    }
}
