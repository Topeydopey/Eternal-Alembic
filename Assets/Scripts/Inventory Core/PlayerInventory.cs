using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Config")]
    public int startingSlots = 16;
    public string saveKey = "player_inventory_v1";
    public ItemDatabase database; // assign or auto-load from Resources

    [Header("Runtime")]
    public Inventory inv = new Inventory();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!database) database = Resources.Load<ItemDatabase>("ItemDatabase");

        inv.slotCount = startingSlots;
        inv.InitIfNeeded();

        // load save if present
        if (PlayerPrefs.HasKey(saveKey))
            inv.FromJson(PlayerPrefs.GetString(saveKey), database);
    }

    private void OnApplicationQuit() => SaveNow();
    private void OnDestroy() { if (Instance == this) SaveNow(); }

    public void SaveNow()
    {
        var json = inv.ToJson();
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }
}
