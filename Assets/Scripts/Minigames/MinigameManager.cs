// MinigameManager.cs
using System;
using UnityEngine;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    [Header("UI Root")]
    public Canvas rootCanvas;        // Screen Space - Overlay
    public RectTransform modalLayer; // Empty RectTransform under the canvas; will host minigames
    public GameObject dimmer;        // Fullscreen Image to dim background (optional)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (rootCanvas) rootCanvas.enabled = false; // hidden until used
        if (dimmer) dimmer.SetActive(false);
    }

    public void Open(GameObject minigamePrefab, Action<ItemSO> onComplete)
    {
        if (!rootCanvas || !modalLayer || !minigamePrefab) return;

        rootCanvas.enabled = true;
        if (dimmer) dimmer.SetActive(true);

        var go = Instantiate(minigamePrefab, modalLayer);
        var mg = go.GetComponent<UIMinigameBase>();
        if (!mg) { Debug.LogError("Minigame prefab missing UIMinigameBase"); return; }

        mg.StartMinigame(resultItem =>
        {
            // close
            Destroy(go);
            if (dimmer) dimmer.SetActive(false);
            rootCanvas.enabled = false;

            onComplete?.Invoke(resultItem);
        });
    }
}
