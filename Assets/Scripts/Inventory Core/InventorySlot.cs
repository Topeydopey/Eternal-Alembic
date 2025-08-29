using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemSO item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;
    public int SpaceLeft => IsEmpty ? (item != null ? item.maxStack : 0) : Mathf.Max(0, item.maxStack - count);

    public int AddUpTo(int amount)
    {
        if (item == null || amount <= 0) return 0;
        int add = Mathf.Min(amount, SpaceLeft);
        count += add;
        return add;
    }
}
