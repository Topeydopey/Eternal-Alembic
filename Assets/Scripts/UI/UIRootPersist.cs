// UIRootPersist.cs
using UnityEngine;

public class UIRootPersist : MonoBehaviour
{
    void Awake()
    {
        // ensure a single UI root across scene loads
        var all = FindObjectsByType<UIRootPersist>(FindObjectsSortMode.None);
        if (all.Length > 1) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }
}
