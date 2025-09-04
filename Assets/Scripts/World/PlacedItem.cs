using UnityEngine;

public class PlacedItem : MonoBehaviour
{
    public ItemSO item;
    public Vector2Int size = Vector2Int.one; // all items = 1x1 on table for now
    public SpriteRenderer sr;

    public void Init(ItemSO i)
    {
        item = i;
        size = Vector2Int.one;
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr) sr.sprite = item ? item.icon : null; // use same sprite everywhere
    }
}
