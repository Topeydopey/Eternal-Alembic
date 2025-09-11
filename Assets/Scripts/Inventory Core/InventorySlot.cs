// InventorySlot.cs
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemSO item;
    public int count; // 0 or 1

    public bool IsEmpty => item == null || count <= 0;

    public void Clear()
    {
        item = null;
        count = 0;
    }

    public bool Accepts(ItemSO incoming)
    {
        // If you want per-slot filters, add here; for now accept anything
        return true;
    }

    /// <summary>
    /// Tries to put ONE unit of 'incoming' into this slot.
    /// Returns leftover amount (0 or 1 since we don't stack).
    /// </summary>
    public int AddOne(ItemSO incoming)
    {
        if (IsEmpty && Accepts(incoming))
        {
            item = incoming;
            count = 1;
            return 0; // no leftover
        }
        // slot is occupied or rejects item -> can't add
        return 1;
    }

    /// <summary>
    /// Removes one unit from this slot. Returns the removed item or null.
    /// </summary>
    public ItemSO RemoveOne()
    {
        if (IsEmpty) return null;
        var outItem = item;
        Clear();
        return outItem;
    }
}
