using UnityEngine;
using UnityEngine.EventSystems;

public class MortarClickable : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        MortarPestleMinigame.Instance?.OnMortarClicked();
    }
}
