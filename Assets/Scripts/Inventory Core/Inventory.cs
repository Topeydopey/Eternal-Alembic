using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Inventory
{
    [Min(1)] public int slotCount = 16;
    public List<InventorySlot> slots = new();

    [NonSerialized] public Action OnChanged; // hook UI

    public void InitIfNeeded()
    {
        if (slots.Count == 0)
            for (int i = 0; i < slotCount; i++) slots.Add(new InventorySlot());
    }

    public int Add(ItemSO item, int amount)
    {
        if (item == null || amount <= 0) return 0;
        InitIfNeeded();
        int remaining = amount;

        // fill existing stacks
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty || slots[i].item != item) continue;
            int added = slots[i].AddUpTo(remaining);
            if (added > 0) { slots[i] = slots[i]; remaining -= added; }
        }
        // open empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int add = Mathf.Min(remaining, item.maxStack);
            slots[i] = new InventorySlot { item = item, count = add };
            remaining -= add;
        }

        if (remaining != amount) OnChanged?.Invoke();
        return amount - remaining; // actually added
    }

    public int Remove(ItemSO item, int amount)
    {
        if (item == null || amount <= 0) return 0;
        InitIfNeeded();
        int toRemove = amount;

        for (int i = 0; i < slots.Count && toRemove > 0; i++)
        {
            if (slots[i].IsEmpty || slots[i].item != item) continue;
            int take = Mathf.Min(toRemove, slots[i].count);
            slots[i].count -= take;
            if (slots[i].count <= 0) slots[i] = new InventorySlot();
            toRemove -= take;
        }

        if (toRemove != amount) OnChanged?.Invoke();
        return amount - toRemove; // removed
    }

    public bool Has(ItemSO item, int amount)
    {
        int have = 0;
        foreach (var s in slots) if (!s.IsEmpty && s.item == item) { have += s.count; if (have >= amount) return true; }
        return false;
    }

    public void MoveOrSwap(int from, int to)
    {
        if (from == to) return;
        InitIfNeeded();
        if (from < 0 || from >= slots.Count || to < 0 || to >= slots.Count) return;

        var a = slots[from];
        var b = slots[to];

        // merge if same item and space available
        if (!a.IsEmpty && !b.IsEmpty && a.item == b.item && b.SpaceLeft > 0)
        {
            int moved = Mathf.Min(a.count, b.SpaceLeft);
            b.count += moved;
            a.count -= moved;
            if (a.count <= 0) a = new InventorySlot();
            slots[from] = a; slots[to] = b;
        }
        else
        {
            // swap
            slots[from] = b; slots[to] = a;
        }
        OnChanged?.Invoke();
    }

    // ---------- Save/Load ----------
    [Serializable] private struct SlotSave { public string id; public int count; }
    [Serializable] private struct InvSave { public int slotCount; public List<SlotSave> slots; }

    public string ToJson()
    {
        var data = new InvSave { slotCount = slotCount, slots = new List<SlotSave>(slotCount) };
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            data.slots.Add(new SlotSave { id = s.item ? s.item.id : null, count = s.count });
        }
        return JsonUtility.ToJson(data);
    }

    public void FromJson(string json, ItemDatabase db)
    {
        if (string.IsNullOrEmpty(json) || db == null) return;
        db.Init();
        var data = JsonUtility.FromJson<InvSave>(json);
        slotCount = Mathf.Max(1, data.slotCount);
        slots.Clear();
        for (int i = 0; i < slotCount; i++)
        {
            if (i < data.slots.Count && !string.IsNullOrEmpty(data.slots[i].id))
            {
                var it = db.Get(data.slots[i].id);
                slots.Add(new InventorySlot { item = it, count = data.slots[i].count });
            }
            else slots.Add(new InventorySlot());
        }
        OnChanged?.Invoke();
    }
}
