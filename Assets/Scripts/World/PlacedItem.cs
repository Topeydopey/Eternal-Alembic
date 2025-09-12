using UnityEngine;

public class PlacedItem : MonoBehaviour
{
    public ItemSO item;
    public SpriteRenderer sr;

    /// <param name="cellSize">World units of the table cell (should match your table’s cellSize)</param>
    /// <param name="padding01">0..1 fraction; 0.12 means 12% padding inside the cell</param>
    /// <param name="extraScale">Optional per-item multiplier (e.g. 1.2 for slightly bigger)</param>
    public void Init(ItemSO i, float cellSize = 1f, float padding01 = 0.12f, float extraScale = 1f)
    {
        item = i;
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
        if (!sr) return;

        // Use item icon if prefab has no sprite or you want to force consistency
        if (item && item.icon) sr.sprite = item.icon;

        // Guard: no sprite bounds yet
        if (!sr.sprite) return;
        var bounds = sr.sprite.bounds.size;          // in world units at scale = 1
        float longest = Mathf.Max(bounds.x, bounds.y);
        if (longest <= Mathf.Epsilon) return;

        // target side = cellSize minus padding
        float target = cellSize * Mathf.Clamp01(1f - padding01);

        // Keep any scale the prefab already had on the SR transform
        float baseScale = sr.transform.localScale.x; // assume uniform
        // scale factor to fit longest side into target
        float fit = (target / longest);

        float final = baseScale * fit * extraScale;

        // Clamp to avoid extreme tiny/huge cases
        final = Mathf.Clamp(final, 0.05f, 4f);

        sr.transform.localScale = new Vector3(final, final, 1f);
    }
}
