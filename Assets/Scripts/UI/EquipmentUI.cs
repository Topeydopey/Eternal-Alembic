// EquipmentUI.cs
using UnityEngine;
using UnityEngine.EventSystems;

public class EquipmentUI : MonoBehaviour
{
    public EquipmentInventory equipment; // auto-find if null

    [Header("Slots (assign in Inspector)")]
    public SlotUI leftHandUI;
    public SlotUI rightHandUI;
    public SlotUI pocketLUI;
    public SlotUI pocketRUI;

    private void OnEnable()
    {
        if (!equipment) equipment = FindFirstObjectByType<EquipmentInventory>();
        BindSlots();
        if (equipment != null) equipment.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.OnChanged -= Refresh;
    }

    private void BindSlots()
    {
        if (leftHandUI) { leftHandUI.slotType = EquipmentSlotType.LeftHand; leftHandUI.Bind(this); }
        if (rightHandUI) { rightHandUI.slotType = EquipmentSlotType.RightHand; rightHandUI.Bind(this); }
        if (pocketLUI) { pocketLUI.slotType = EquipmentSlotType.PocketL; pocketLUI.Bind(this); }
        if (pocketRUI) { pocketRUI.slotType = EquipmentSlotType.PocketR; pocketRUI.Bind(this); }
    }

    public void Refresh()
    {
        if (!equipment) return;

        // Update icons
        leftHandUI?.SetSprite(equipment.leftHand.IsEmpty ? null : equipment.leftHand.item.icon);
        rightHandUI?.SetSprite(equipment.rightHand.IsEmpty ? null : equipment.rightHand.item.icon);
        pocketLUI?.SetSprite(equipment.pocketL.IsEmpty ? null : equipment.pocketL.item.icon);
        pocketRUI?.SetSprite(equipment.pocketR.IsEmpty ? null : equipment.pocketR.item.icon);

        // Highlight ONLY the active hand
        leftHandUI?.SetActive(equipment.activeHand == EquipmentSlotType.LeftHand);
        rightHandUI?.SetActive(equipment.activeHand == EquipmentSlotType.RightHand);
        pocketLUI?.SetActive(false);
        pocketRUI?.SetActive(false);
    }

    /// <summary>
    /// Single-click behavior:
    /// - Clicking a hand: set active hand.
    /// - Clicking a pocket:
    ///     If active hand has an item -> move/swap active→pocket.
    ///     If active hand empty and pocket has an item -> move/swap pocket→active.
    /// </summary>
    public void OnSlotClicked(SlotUI clicked, PointerEventData.InputButton button)
    {
        if (!equipment || button != PointerEventData.InputButton.Left) return;

        var clickedType = clicked.slotType;

        // Clicking a hand: set active hand and refresh
        if (clickedType == EquipmentSlotType.LeftHand || clickedType == EquipmentSlotType.RightHand)
        {
            equipment.SetActiveHand(clickedType);
            Refresh();
            return;
        }

        // Otherwise (e.g., pocket): push/pull relative to active hand
        var active = equipment.activeHand;
        var activeSlot = equipment.Get(active);
        var targetSlot = equipment.Get(clickedType);

        bool activeHasItem = activeSlot != null && !activeSlot.IsEmpty;
        bool targetHasItem = targetSlot != null && !targetSlot.IsEmpty;

        if (activeHasItem)
        {
            // Move/swap from active hand → clicked slot
            if (equipment.MoveActiveTo(clickedType))
                Refresh();
        }
        else if (targetHasItem)
        {
            // Pull from clicked slot → active hand
            if (equipment.MoveToActiveFrom(clickedType))
                Refresh();
        }
        // else: both empty, do nothing
    }
}
