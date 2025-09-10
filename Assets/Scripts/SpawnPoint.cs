using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Identity")]
    public string spawnId;        // must match a portal's destinationSpawnId

    [Header("When there is NO NextSpawnId...")]
    public bool placeWhenNoRouter = false; // <-- default OFF; set true only in scenes where you still want an auto-spawn
    public bool defaultSpawn = false;      // used only if placeWhenNoRouter is true

    private void Start()
    {
        // only the first SpawnPoint runs the placement logic (cheap shortcut)
        if (FindFirstObjectByType<SpawnPoint>() != this) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        // 1) If coming from a portal, honor NextSpawnId
        if (!string.IsNullOrEmpty(SpawnRouter.NextSpawnId))
        {
            SpawnPoint chosen = null;
            foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
                if (sp.spawnId == SpawnRouter.NextSpawnId) { chosen = sp; break; }

            if (chosen) player.transform.position = chosen.transform.position;

            // consume once
            SpawnRouter.NextSpawnId = null;
            return;
        }

        // 2) If NOT coming from a portal:
        //    Only move the player if you explicitly want that in this scene
        if (!placeWhenNoRouter) return; // <-- pressing Play will keep the player where you put them in the editor

        // optional default behavior (legacy)
        SpawnPoint fallback = null;
        foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (sp.defaultSpawn) { fallback = sp; break; }

        if (!fallback) fallback = this;
        player.transform.position = fallback.transform.position;
    }
}
