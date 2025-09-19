// Assets/Scripts/World/GameRunReset.cs
using UnityEngine;

public static class GameRunReset
{
    /// <summary>Clear all per-run state right now (call this before loading the gameplay scene).</summary>
    public static void ResetNowForNewRun()
    {
        // 1) Clear once-per-run workstation locks
        RunOnce.ClearAll();

        // 2) Reset singletons if they already exist (safe if they don't)
        if (EquipmentInventory.Instance) EquipmentInventory.Instance.ResetForNewRun();
        if (GameState.Instance) GameState.Instance.ResetForNewRun();

        // 3) Make sure time isn't paused, etc.
        Time.timeScale = 1f;

#if UNITY_EDITOR
        Debug.Log("[GameRunReset] Per-run state cleared.");
#endif
    }
}
