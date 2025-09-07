// DayHud.cs
using UnityEngine;
using TMPro;

public class DayHud : MonoBehaviour
{
    public TMP_Text dayText;
    public TMP_Text objectiveText;

    private void OnEnable()
    {
        if (GameState.Instance) GameState.Instance.OnChanged += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        if (GameState.Instance) GameState.Instance.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        var gs = GameState.Instance;
        if (!gs) return;
        if (dayText) dayText.text = $"Day {gs.currentDay}";
        if (objectiveText) objectiveText.text = gs.ObjectiveText();
    }
}
