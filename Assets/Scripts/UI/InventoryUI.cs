using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInventory playerInventory;       // drag the PlayerInventory GameObject
    public Transform gridParent;                  // a RectTransform with GridLayoutGroup
    public GameObject slotPrefab;                 // prefab with Image+Text

    private void OnEnable()
    {
        if (!playerInventory) playerInventory = FindFirstObjectByType<PlayerInventory>();
        playerInventory.inv.OnChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (playerInventory) playerInventory.inv.OnChanged -= Rebuild;
    }

    public void Rebuild()
    {
        // clear
        for (int i = gridParent.childCount - 1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        // build
        var inv = playerInventory.inv;
        inv.InitIfNeeded();

        for (int i = 0; i < inv.slots.Count; i++)
        {
            var go = Instantiate(slotPrefab, gridParent);
            var img = go.transform.Find("Icon").GetComponent<Image>();
            var txt = go.transform.Find("Count").GetComponent<TMP_Text>();

            var s = inv.slots[i];
            if (!s.IsEmpty)
            {
                img.enabled = true;
                img.sprite = s.item.icon;
                txt.text = s.count > 1 ? s.count.ToString() : "";
            }
            else
            {
                img.enabled = false;
                txt.text = "";
            }
        }
    }
}
