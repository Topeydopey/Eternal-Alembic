// Inventory.cs (core)
using System.Collections.Generic;
using UnityEngine;
using System;

public class Inventory : MonoBehaviour
{
    public List<InventorySlot> slots = new List<InventorySlot>();
    public event Action OnChanged;

    /// <summary>
    /// Adds 'amount' units of 'item' into empty slots, one per slot.
    /// Returns true if everything was added.
    /// </summary>
    public bool Add(ItemSO item, int amount = 1)
    {
        int remaining = amount;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty && slots[i].Accepts(item))
            {
                remaining = slots[i].AddOne(item);
            }
        }

        OnChanged?.Invoke();
        return remaining == 0;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= slots.Count) return false;
        if (slots[index].IsEmpty) return false;
        slots[index].RemoveOne();
        OnChanged?.Invoke();
        return true;
    }

    public ItemSO PeekAt(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index].IsEmpty ? null : slots[index].item;
    }
}
