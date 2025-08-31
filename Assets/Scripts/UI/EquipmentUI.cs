using UnityEngine;
using UnityEngine.UI;

public class EquipmentUI : MonoBehaviour
{
    public EquipmentInventory equipment; // assign or will auto-find
    [Header("Slots")]
    public SlotUI leftHandUI;
    public SlotUI rightHandUI;
    public SlotUI pocketLUI;
    public SlotUI pocketRUI;

    private void OnEnable()
    {
        if (!equipment) equipment = FindFirstObjectByType<EquipmentInventory>();
        if (equipment) equipment.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (equipment) equipment.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!equipment) return;

        leftHandUI?.Set(equipment.leftHand.IsEmpty ? null : equipment.leftHand.item.icon);
        rightHandUI?.Set(equipment.rightHand.IsEmpty ? null : equipment.rightHand.item.icon);
        pocketLUI?.Set(equipment.pocketL.IsEmpty ? null : equipment.pocketL.item.icon);
        pocketRUI?.Set(equipment.pocketR.IsEmpty ? null : equipment.pocketR.item.icon);
    }
}
