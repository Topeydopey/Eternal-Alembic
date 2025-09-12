using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class TableSurface : MonoBehaviour
{
    [Header("Grid")]
    public float cellSize = 0.32f;

    [Header("Prefabs")]
    public GameObject placedItemPrefab;   // fallback if the item has no tablePrefab

    [Header("Masks")]
    public LayerMask tableItemMask;       // for ItemAt() (should include TableItem)

    private BoxCollider2D _box;
    private readonly HashSet<Vector2Int> _occupied = new();

    void Awake() { _box = GetComponent<BoxCollider2D>(); }

    public bool TryPlace(ItemSO item, Vector2 worldPoint, out PlacedItem placed)
    {
        placed = null;
        if (!item) return false;

        // inside tabletop?
        Vector2 local = transform.InverseTransformPoint(worldPoint);
        Vector2 min = (Vector2)_box.offset - _box.size * 0.5f;
        Vector2 max = (Vector2)_box.offset + _box.size * 0.5f;
        if (local.x < min.x || local.x > max.x || local.y < min.y || local.y > max.y) return false;

        // snap to grid
        Vector2 origin = min;
        Vector2Int cell = new(
            Mathf.FloorToInt((local.x - origin.x) / cellSize),
            Mathf.FloorToInt((local.y - origin.y) / cellSize)
        );

        if (_occupied.Contains(cell)) return false; // 1x1 occupancy

        // choose per-item prefab or fallback
        var prefab = item.tablePrefab != null ? item.tablePrefab : placedItemPrefab;

        // spawn as child of table
        var go = Instantiate(prefab, transform);              // <-- use prefab, not placedItemPrefab
        Vector2 centerLocal = origin + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
        go.transform.localPosition = centerLocal;

        // ensure correct layer for clicks
        int tableItemLayer = LayerMask.NameToLayer("TableItem");
        if (tableItemLayer >= 0) SetLayerRecursively(go, tableItemLayer);

        // ensure PlacedItem & init
        var pi = go.GetComponent<PlacedItem>();
        if (!pi) pi = go.AddComponent<PlacedItem>();

        // if your PlacedItem has Init(ItemSO, float), use that; else Init(ItemSO)
        pi.Init(item, cellSize);

        _occupied.Add(cell);
        placed = pi;
        return true;
    }

    public PlacedItem ItemAt(Vector2 worldPoint)
    {
        var hit = Physics2D.OverlapPoint(worldPoint, tableItemMask);
        return hit ? hit.GetComponentInParent<PlacedItem>() : null;
    }

    public void Remove(PlacedItem pi)
    {
        if (!pi) return;

        Vector2 min = (Vector2)_box.offset - _box.size * 0.5f;
        Vector2 origin = min;
        Vector2 pos = pi.transform.localPosition;
        Vector2Int cell = new(
            Mathf.FloorToInt((pos.x - origin.x) / cellSize),
            Mathf.FloorToInt((pos.y - origin.y) / cellSize)
        );

        _occupied.Remove(cell);
        Destroy(pi.gameObject);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform t in obj.transform) SetLayerRecursively(t.gameObject, layer);
    }
}
