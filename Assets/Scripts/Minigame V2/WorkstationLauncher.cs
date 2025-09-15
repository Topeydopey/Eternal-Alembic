using UnityEngine;

[DisallowMultipleComponent]
public class WorkstationLauncher : MonoBehaviour
{
    [Header("Scene Objects (no prefabs here)")]
    [SerializeField] private Canvas hudCanvas;          // top HUD/slots
    [SerializeField] private GameObject minigameRoot;   // in-scene minigame UI root (inactive at start)

    [Header("Options")]
    public bool hideHudWhileOpen = true;

    private MortarPestleMinigame ctrl;
    private bool isOpen;

    public void Launch()
    {
        if (!minigameRoot)
        {
            Debug.LogError("[Launcher] minigameRoot not assigned.");
            return;
        }
        if (isOpen) return;

        // Enable minigame UI
        minigameRoot.SetActive(true);

        // Get controller + tell it we are reusing this UI (disable on close, don’t destroy)
        ctrl = minigameRoot.GetComponentInChildren<MortarPestleMinigame>(true);
        if (!ctrl)
        {
            Debug.LogError("[Launcher] MortarPestleMinigame not found under minigameRoot.");
            minigameRoot.SetActive(false);
            return;
        }

        ctrl.SetReuseMode(true, minigameRoot);   // <-- reuse (disable) on close
        ctrl.BeginSession();                     // <-- reset UI/state for a fresh round
        ctrl.onClosed += HandleClosed;

        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(false);

        isOpen = true;
    }

    private void HandleClosed()
    {
        if (ctrl) ctrl.onClosed -= HandleClosed;
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        isOpen = false;
        ctrl = null;
    }

    private void OnDisable()
    {
        if (ctrl) ctrl.onClosed -= HandleClosed;
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        isOpen = false;
    }
}
