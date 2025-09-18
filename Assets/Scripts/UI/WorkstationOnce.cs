// Assets/Scripts/Minigame V2/Common/WorkstationOnce.cs
using UnityEngine;

public static class WorkstationOnce
{
    private static string Key(string id) => $"WS_USED::{id}";

    public static bool IsUsed(string stationId)
    {
        if (string.IsNullOrEmpty(stationId)) return false;
        return PlayerPrefs.GetInt(Key(stationId), 0) == 1;
    }

    public static void MarkUsed(string stationId)
    {
        if (string.IsNullOrEmpty(stationId)) return;
        PlayerPrefs.SetInt(Key(stationId), 1);
        PlayerPrefs.Save();
    }
}
