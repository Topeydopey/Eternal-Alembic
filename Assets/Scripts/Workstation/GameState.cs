// Assets/Scripts/State/GameState.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("Recipe (unordered)")]
    public ItemSO[] requiredItems;

    [Header("Runtime (read-only)")]
    [SerializeField] private List<ItemSO> delivered = new();
    [SerializeField] private bool rewardAvailable = false;

    public event Action OnChanged;

    void Awake()
    {
        // Scene-scoped singleton: keep the FIRST one in the scene, no cross-scene persistence.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // NOTE: No DontDestroyOnLoad here anymore.
    }

    void OnDestroy()
    {
        // Clear static reference if this was the active instance.
        if (Instance == this) Instance = null;
    }

    // Also clear the static reference when domain reload happens (entering Play Mode)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticRefOnDomainReload() => Instance = null;

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

        bool isRequired = Array.Exists(requiredItems, it => it == item);
        if (!isRequired) return false;
        if (delivered.Contains(item)) return false;

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

    // ----------------- Per-run reset -----------------
    public void ResetForNewRun()
    {
        ResetRecipe();
    }
}
