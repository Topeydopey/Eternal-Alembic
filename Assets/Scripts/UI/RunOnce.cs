// Assets/Scripts/Minigame V2/Common/RunOnce.cs
using System.Collections.Generic;
using UnityEngine;

public static class RunOnce
{
    private static readonly HashSet<string> used = new();

    // Auto-clear the per-run cache at each Play start, even if domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlayStart()
    {
        used.Clear();
        // If you have a run-tracker, you could call it here.
        // e.g. CauldronOrderRun.StartNewRunIfNone();
    }

    public static bool IsUsed(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return used.Contains(id);
    }

    public static void MarkUsed(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        used.Add(id);
    }

    public static void Clear(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        used.Remove(id);
    }

    public static void ClearAll() => used.Clear();
}
