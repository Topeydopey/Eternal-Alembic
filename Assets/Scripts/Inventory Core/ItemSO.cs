using UnityEngine;

[CreateAssetMenu(menuName = "Alchemy/Item", fileName = "NewItem")]
public class ItemSO : ScriptableObject
{
    [Tooltip("Unique ID (stable across builds). e.g., 'mushroom.redcap'")]
    public string id;
    public string displayName;
    public Sprite icon;
    [Min(1)] public int maxStack = 20;
}
