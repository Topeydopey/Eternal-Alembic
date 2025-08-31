using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class SlotUI : MonoBehaviour, IPointerClickHandler
{
    public EquipmentSlotType slotType;
    public Image icon;
    public TMP_Text label;   // optional
    public Image backdrop;   // background for highlight

    [Header("Colors")]
    public Color normalColor = new Color(1, 1, 1, 0.15f);
    public Color activeHandColor = new Color(1, 1, 0.3f, 0.35f);

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

    public void SetActive(bool isActiveHand)
    {
        if (backdrop) backdrop.color = isActiveHand ? activeHandColor : normalColor;
    }

    public void OnPointerClick(PointerEventData e)
    {
        _ui?.OnSlotClicked(this, e.button);
    }
}
