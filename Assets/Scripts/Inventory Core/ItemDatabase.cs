using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Alchemy/Item Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemSO> items = new();
    private Dictionary<string, ItemSO> _byId;

    public void Init()
    {
        if (_byId != null) return;
        _byId = new Dictionary<string, ItemSO>();
        foreach (var it in items)
        {
            if (it == null || string.IsNullOrEmpty(it.id)) continue;
            if (!_byId.ContainsKey(it.id)) _byId.Add(it.id, it);
        }
    }

    public ItemSO Get(string id)
    {
        Init();
        return id != null && _byId.TryGetValue(id, out var it) ? it : null;
    }
}
