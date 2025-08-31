// ItemSO.cs
using UnityEngine;

public enum ItemSize { Small, Normal, Bulky }

[CreateAssetMenu(menuName = "Alchemy/Item", fileName = "NewItem")]
public class ItemSO : ScriptableObject
{
    [Tooltip("Unique, stable string (e.g., 'mushroom.redcap')")]
    public string id;
    public string displayName;
    public Sprite icon;
    [Min(1)] public int maxStack = 20;

    [Header("Equipment")]
    public ItemSize size = ItemSize.Small;
}
