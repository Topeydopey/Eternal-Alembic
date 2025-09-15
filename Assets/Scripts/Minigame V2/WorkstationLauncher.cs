using UnityEngine;

public class WorkstationLauncher : MonoBehaviour
{
    public GameObject minigameUIPrefab;

    private GameObject activeMinigame;

    public void Launch()
    {
        if (activeMinigame == null)
        {
            activeMinigame = Instantiate(minigameUIPrefab, FindFirstObjectByType<Canvas>().transform);
            activeMinigame.SetActive(true);
        }
    }
}
