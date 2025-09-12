// GameState.cs
using UnityEngine;
using System;

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    [Header("Recipe (one item per day)")]
    public ItemSO[] requiredPerDay;    // set in Inspector: Day1=itemA, Day2=itemB...

    [Header("Runtime")]
    public int currentDay = 1;         // 1-based
    public bool depositedToday = false;

    public event Action OnChanged;     // HUD can listen
    public bool advanceOnSubmit = true; // NEW

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ValidateDay();
    }

    private void ValidateDay()
    {
        if (currentDay < 1) currentDay = 1;
        if (currentDay > requiredPerDay.Length) currentDay = requiredPerDay.Length;
    }

    public ItemSO RequiredItemToday()
    {
        ValidateDay();
        if (requiredPerDay == null || requiredPerDay.Length == 0) return null;
        return requiredPerDay[Mathf.Clamp(currentDay - 1, 0, requiredPerDay.Length - 1)];
    }

    public bool SubmitItem(ItemSO item)
    {
        if (item == null) return false;
        var need = RequiredItemToday();
        if (need != null && item == need)
        {
            depositedToday = true;
            OnChanged?.Invoke();

            if (advanceOnSubmit)
                NextDay(); // reuse your existing increment/reset

            return true;
        }
        return false;
    }

    public bool CanSleep() => depositedToday; // simple gate

    public void NextDay()
    {
        if (!CanSleep()) return;
        currentDay = Mathf.Min(currentDay + 1, requiredPerDay.Length);
        depositedToday = false;
        OnChanged?.Invoke();
    }

    public string ObjectiveText()
    {
        var need = RequiredItemToday();
        if (need == null) return "No objective";
        return depositedToday ? "Rest at bed" : $"Bring: {need.displayName}";
    }
}
