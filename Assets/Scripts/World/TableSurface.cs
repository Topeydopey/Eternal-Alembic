using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class TableSurface : MonoBehaviour
{
    [Header("Grid")]
    public float cellSize = 0.32f;              // tweak to match your art
    public GameObject placedItemPrefab;         // prefab with SpriteRenderer + PlacedItem
    public LayerMask tableItemMask;             // includes "TableItem" layer for placed item colliders

    private BoxCollider2D _box;
    private readonly HashSet<Vector2Int> _occupied = new(); // which cells are used

    void Awake() { _box = GetComponent<BoxCollider2D>(); }

    public bool TryPlace(ItemSO item, Vector2 worldPoint, out PlacedItem placed)
    {
        placed = null;
        if (!item) return false;

        // localize & inside tabletop?
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

        // 1x1 occupancy check
        if (_occupied.Contains(cell)) return false;

        // spawn visual as child of table
        var go = Instantiate(placedItemPrefab, transform);
        Vector2 centerLocal = origin + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
        go.transform.localPosition = centerLocal;

        var pi = go.GetComponent<PlacedItem>();
        if (!pi) pi = go.AddComponent<PlacedItem>();
        pi.Init(item);

        _occupied.Add(cell);
        placed = pi;
        return true;
    }

    public PlacedItem ItemAt(Vector2 worldPoint)
    {
        // find a placed item by collider
        var hit = Physics2D.OverlapPoint(worldPoint, tableItemMask);
        if (!hit) return null;
        return hit.GetComponentInParent<PlacedItem>();
    }

    public void Remove(PlacedItem pi)
    {
        if (!pi) return;

        // compute cell from current position
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
}
