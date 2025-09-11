// ItemSO.cs — keep this version (no maxStack)
using UnityEngine;

public enum ItemSize { Small, Normal, Bulky }

[CreateAssetMenu(menuName = "Alchemy/Item", fileName = "NewItem")]
public class ItemSO : ScriptableObject
{
    [Tooltip("Unique, stable string (e.g., 'mushroom.redcap')")]
    public string id;
    public string displayName;
    public Sprite icon;

    [Header("Equipment")]
    public ItemSize size = ItemSize.Small;

    [Header("Optional visuals")]
    public GameObject tablePrefab;  // used by TableSurface when placing
}
