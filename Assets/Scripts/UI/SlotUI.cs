using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class SlotUI : MonoBehaviour, IPointerClickHandler
{
    public EquipmentSlotType slotType;
    public Image icon;
    public TMP_Text label;         // optional label like "L", "R", "PL", "PR"
    public Image backdrop;         // background Image for highlight

    [Header("Colors")]
    public Color normalColor = new Color(1, 1, 1, 0.15f);
    public Color activeHandColor = new Color(1, 1, 0.3f, 0.35f);
    public Color selectedColor = new Color(0.3f, 0.7f, 1f, 0.35f);

    private EquipmentUI _ui;

    public void Bind(EquipmentUI ui) { _ui = ui; }

    public void SetSprite(Sprite s)
    {
        if (icon)
        {
            icon.enabled = s != null;
            icon.sprite = s;
        }
    }

    public void SetBackdrop(bool isActiveHand, bool isSelectedSource)
    {
        if (!backdrop) return;
        backdrop.color = isSelectedSource ? selectedColor :
                         isActiveHand ? activeHandColor :
                                           normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _ui?.OnSlotClicked(this);
    }
}
