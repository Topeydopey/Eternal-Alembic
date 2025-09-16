using UnityEngine;
using System;

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("Recipe (ordered)")]
    [Tooltip("Required items in order. Example size = 3 for your 3 minigames.")]
    public ItemSO[] recipe;

    [Header("Runtime")]
    [Tooltip("How many correct items have been deposited so far.")]
    public int progress = 0; // 0..recipe.Length

    public event Action OnChanged;

    public bool IsRecipeComplete => recipe != null && progress >= recipe.Length;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ClampProgress();
    }

    private void ClampProgress()
    {
        if (recipe == null) { progress = 0; return; }
        progress = Mathf.Clamp(progress, 0, recipe.Length);
    }

    public ItemSO NextRequired()
    {
        if (recipe == null || recipe.Length == 0) return null;
        if (IsRecipeComplete) return null;
        return recipe[progress];
    }

    /// <summary>
    /// Submit an item. Returns true only if it matches the next required in sequence.
    /// </summary>
    public bool SubmitItem(ItemSO item)
    {
        var need = NextRequired();
        if (item != null && need != null && item == need)
        {
            progress++;
            OnChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reset the recipe progress (e.g., after dispensing the final potion).
    /// </summary>
    public void ResetRecipe()
    {
        progress = 0;
        OnChanged?.Invoke();
    }
}
