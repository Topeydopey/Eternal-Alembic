// EquipmentSlot.cs
using UnityEngine;

[System.Serializable]
public class EquipmentSlot
{
    public EquipmentSlotType type;
    public ItemSO item;   // single item, no stacks

    public bool IsEmpty => item == null;

    public bool Accepts(ItemSO candidate)
    {
        if (candidate == null) return false;
        switch (type)
        {
            case EquipmentSlotType.LeftHand:
            case EquipmentSlotType.RightHand:
                // Hands can hold Small or Normal (not Bulky)
                return candidate.size != ItemSize.Bulky;

            case EquipmentSlotType.PocketL:
            case EquipmentSlotType.PocketR:
                // Pockets hold only Small
                return candidate.size == ItemSize.Small;

            default: return false;
        }
    }
}
