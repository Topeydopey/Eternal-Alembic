using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("Recipe (unordered)")]
    public ItemSO[] requiredItems;   // assign your 3 result ItemSOs here

    [Header("Runtime (read-only)")]
    [SerializeField] private List<ItemSO> delivered = new(); // items already turned in
    [SerializeField] private bool rewardAvailable = false;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---- Query API ----
    public IReadOnlyList<ItemSO> Delivered => delivered;
    public bool RewardAvailable => rewardAvailable;
    public bool IsRecipeComplete => requiredItems != null && delivered.Count >= (requiredItems?.Length ?? 0);
    public float Progress01 => (requiredItems == null || requiredItems.Length == 0)
        ? 0f : Mathf.Clamp01((float)delivered.Count / requiredItems.Length);

    public IEnumerable<ItemSO> MissingItems()
    {
        if (requiredItems == null) yield break;
        foreach (var it in requiredItems)
            if (!delivered.Contains(it)) yield return it;
    }

    public string ObjectiveText()
    {
        if (requiredItems == null || requiredItems.Length == 0) return "No objective";
        if (IsRecipeComplete && rewardAvailable) return "Collect the potion from the cauldron.";
        var missing = string.Join(", ", MissingItems().Select(i => i.displayName));
        return $"Add ingredients: {delivered.Count}/{requiredItems.Length}. Missing: {missing}";
    }

    // ---- Mutating API ----
    public bool SubmitItem(ItemSO item)
    {
        if (item == null || requiredItems == null || requiredItems.Length == 0) return false;

        // Must be one of the required items and not already submitted
        bool isRequired = Array.Exists(requiredItems, it => it == item);
        if (!isRequired) return false;
        if (delivered.Contains(item)) return false; // reject duplicates

        delivered.Add(item);

        if (IsRecipeComplete) rewardAvailable = true;

        OnChanged?.Invoke();
        return true;
    }

    public void MarkRewardCollected()
    {
        if (!rewardAvailable) return;
        rewardAvailable = false;
        OnChanged?.Invoke();
    }

    public void ResetRecipe()
    {
        delivered.Clear();
        rewardAvailable = false;
        OnChanged?.Invoke();
    }
}
