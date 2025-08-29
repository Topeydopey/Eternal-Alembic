// PlayerPersist.cs
using UnityEngine;

public class PlayerPersist : MonoBehaviour
{
    private void Awake()
    {
        var existing = GameObject.FindGameObjectsWithTag("Player");
        if (existing.Length > 1) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }
}
