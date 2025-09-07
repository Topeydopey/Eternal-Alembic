// BedSleep.cs
using UnityEngine;

public class BedSleep : MonoBehaviour
{
    public void TrySleep()
    {
        var gs = GameState.Instance;
        if (!gs) return;

        if (gs.CanSleep())
        {
            gs.NextDay();
            // TODO: fade/sfx
        }
        else
        {
            // TODO: feedback "You feel unfinished... (deposit today's item)"
        }
    }
}
