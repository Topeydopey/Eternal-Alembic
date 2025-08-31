using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text label; // optional, e.g., "L", "R", "PL", "PR"

    public void Set(Sprite sprite)
    {
        if (icon)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
        }
    }
}
