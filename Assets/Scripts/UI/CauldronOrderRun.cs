// Assets/Scripts/Minigame V2/Common/CauldronOrderRun.cs
using UnityEngine;

public static class CauldronOrderRun
{
    private const string KeyRunId = "CAULDRON_RUN_ID";
    private static string _cached;

    /// <summary>Stable id for the current order run. Creates one if missing.</summary>
    public static string CurrentRunId
    {
        get
        {
            if (string.IsNullOrEmpty(_cached))
            {
                _cached = PlayerPrefs.GetString(KeyRunId, string.Empty);
                if (string.IsNullOrEmpty(_cached))
                {
                    _cached = System.DateTime.UtcNow.Ticks.ToString();
                    PlayerPrefs.SetString(KeyRunId, _cached);
                    PlayerPrefs.Save();
                }
            }
            return _cached;
        }
    }

    /// <summary>Begin a NEW order run (e.g., when the player respawns as a new alchemist).</summary>
    public static void StartNewRun(string seed = null)
    {
        _cached = string.IsNullOrEmpty(seed) ? System.DateTime.UtcNow.Ticks.ToString() : seed;
        PlayerPrefs.SetString(KeyRunId, _cached);
        PlayerPrefs.Save();
        // Note: we don't clear old keys; they’re namespaced by run id and become harmless.
    }
}
