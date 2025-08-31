using UnityEngine;

public class EquipmentUI : MonoBehaviour
{
    public EquipmentInventory equipment; // auto-found if null
    [Header("Slots")]
    public SlotUI leftHandUI;
    public SlotUI rightHandUI;
    public SlotUI pocketLUI;
    public SlotUI pocketRUI;

    // click-to-move state
    private EquipmentSlotType? _selectedSource = null;

    private void OnEnable()
    {
        if (!equipment) equipment = FindFirstObjectByType<EquipmentInventory>();
        BindSlots();
        if (equipment) equipment.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (equipment) equipment.OnChanged -= Refresh;
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

        // icons
        leftHandUI?.SetSprite(equipment.leftHand.IsEmpty ? null : equipment.leftHand.item.icon);
        rightHandUI?.SetSprite(equipment.rightHand.IsEmpty ? null : equipment.rightHand.item.icon);
        pocketLUI?.SetSprite(equipment.pocketL.IsEmpty ? null : equipment.pocketL.item.icon);
        pocketRUI?.SetSprite(equipment.pocketR.IsEmpty ? null : equipment.pocketR.item.icon);

        // highlights
        bool isSel(EquipmentSlotType t) => _selectedSource.HasValue && _selectedSource.Value == t;
        bool isActive(EquipmentSlotType t) => equipment.activeHand == t;

        leftHandUI?.SetBackdrop(isActive(EquipmentSlotType.LeftHand), isSel(EquipmentSlotType.LeftHand));
        rightHandUI?.SetBackdrop(isActive(EquipmentSlotType.RightHand), isSel(EquipmentSlotType.RightHand));
        pocketLUI?.SetBackdrop(false, isSel(EquipmentSlotType.PocketL));
        pocketRUI?.SetBackdrop(false, isSel(EquipmentSlotType.PocketR));
    }

    // Called by SlotUI when clicked
    public void OnSlotClicked(SlotUI clicked)
    {
        if (!equipment) return;

        var type = clicked.slotType;

        // If nothing selected yet:
        if (_selectedSource == null)
        {
            // Click a HAND slot → just set active hand highlight
            if (type == EquipmentSlotType.LeftHand || type == EquipmentSlotType.RightHand)
            {
                equipment.SetActiveHand(type);
                // If that hand has an item, also mark it as source so next click moves it
                if (!equipment.Get(type).IsEmpty) _selectedSource = type;
            }
            else
            {
                // Clicking a pocket with an item selects it as source; empty pocket does nothing
                if (!equipment.Get(type).IsEmpty) _selectedSource = type;
            }
            Refresh();
            return;
        }

        // Second click: attempt move/swap selected → clicked
        var from = _selectedSource.Value;
        if (equipment.MoveOrSwap(from, type))
        {
            _selectedSource = null;
            Refresh();
            return;
        }

        // If move failed and user clicked same slot, deselect
        if (from == type) { _selectedSource = null; Refresh(); return; }

        // Else: switch source to the clicked slot if it holds an item
        if (!equipment.Get(type).IsEmpty) _selectedSource = type;
        Refresh();
    }
}
