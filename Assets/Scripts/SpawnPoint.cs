using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    public string spawnId;      // must match a portal's destinationSpawnId
    public bool defaultSpawn;   // used when no NextSpawnId was set

    private void Start()
    {
        // only the first SpawnPoint runs the placement logic (cheap shortcut)
        if (FindFirstObjectByType<SpawnPoint>() != this) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // choose spawn: match id or use default
        SpawnPoint chosen = null;

        if (!string.IsNullOrEmpty(SpawnRouter.NextSpawnId))
        {
            foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
                if (sp.spawnId == SpawnRouter.NextSpawnId) { chosen = sp; break; }
        }
        if (chosen == null)
        {
            foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
                if (sp.defaultSpawn) { chosen = sp; break; }
        }
        if (chosen == null) chosen = this; // fallback

        player.transform.position = chosen.transform.position;

        // clear router so future loads can use defaults
        SpawnRouter.NextSpawnId = null;
    }
}
