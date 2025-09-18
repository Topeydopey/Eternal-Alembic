using UnityEngine;

[DisallowMultipleComponent]
public class WorkstationLauncher : MonoBehaviour
{
    [Header("Scene Objects (no prefabs here)")]
    [SerializeField] private Canvas hudCanvas;          // your top HUD/slots canvas
    [SerializeField] private GameObject minigameRoot;   // the minigame Canvas root (set inactive at start)

    [Header("Modal Dimmer")]
    [Tooltip("Full-screen Image with MinigameDimmer on the minigame canvas. Blocks clicks + fades.")]
    [SerializeField] private MinigameDimmer dimmer;     // optional but recommended

    [Header("Disable While Open (optional)")]
    [Tooltip("Scripts to disable when a minigame is open (e.g., PlayerMovement, PlayerClickInteractor).")]
    [SerializeField] private MonoBehaviour[] disableWhileOpen;

    [Header("Options")]
    public bool hideHudWhileOpen = true;

    // controllers (one of these will be found)
    private MortarPestleMinigame mortar;
    private SnakeStationMinigame snakeStation;
    private SnakeWorldMinigame snakeWorld;
    private PhilosophersAlembicMinigame alembic;

    private bool isOpen;

    public void Launch()
    {
        if (!minigameRoot)
        {
            Debug.LogError("[Launcher] minigameRoot not assigned.");
            return;
        }
        if (isOpen) return;

        // Turn on the minigame canvas
        minigameRoot.SetActive(true);
        Debug.Log($"[Launcher] Activated {minigameRoot.name}");

        // Try each known controller under that root
        mortar = minigameRoot.GetComponentInChildren<MortarPestleMinigame>(true);
        snakeStation = minigameRoot.GetComponentInChildren<SnakeStationMinigame>(true);
        snakeWorld = minigameRoot.GetComponentInChildren<SnakeWorldMinigame>(true);
        alembic = minigameRoot.GetComponentInChildren<PhilosophersAlembicMinigame>(true);

        if (mortar)
        {
            mortar.SetReuseMode(true, minigameRoot);
            mortar.onClosed += HandleClosedMortar;
            mortar.BeginSession();
            Debug.Log("[Launcher] Opened MortarPestleMinigame");
        }
        else if (snakeStation)
        {
            snakeStation.SetReuseMode(true, minigameRoot);
            snakeStation.onClosed += HandleClosedStation;
            snakeStation.BeginSession();
            Debug.Log("[Launcher] Opened SnakeStationMinigame");
        }
        else if (snakeWorld)
        {
            // Ensure it disables instead of destroying when closing
            snakeWorld.disableInsteadOfDestroy = true;
            if (snakeWorld.owningRoot == null) snakeWorld.owningRoot = minigameRoot;
            if (snakeWorld.owningCanvas == null) snakeWorld.owningCanvas = minigameRoot.GetComponent<Canvas>();

            snakeWorld.onClosed += HandleClosedWorld;
            snakeWorld.BeginSession();
            Debug.Log("[Launcher] Opened SnakeWorldMinigame");
        }
        else if (alembic)
        {
            // If not already set in Inspector, set owning refs for consistency
            if (alembic.owningRoot == null) alembic.owningRoot = minigameRoot;
            if (alembic.owningCanvas == null) alembic.owningCanvas = minigameRoot.GetComponent<Canvas>();

            alembic.onClosed.AddListener(HandleClosedAlembic);
            alembic.BeginSession();
            Debug.Log("[Launcher] Opened PhilosophersAlembicMinigame");
        }
        else
        {
            Debug.LogError("[Launcher] No known minigame found (Mortar, SnakeStation, SnakeWorld, Alembic) under the canvas.");
            minigameRoot.SetActive(false);
            return;
        }

        // Global UI/UX while open
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(false);
        if (dimmer) dimmer.Show();
        if (disableWhileOpen != null)
            foreach (var mb in disableWhileOpen) if (mb) mb.enabled = false;

        isOpen = true;
    }

    private void HandleClosedCommon()
    {
        // Restore UI/controls
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        if (dimmer) dimmer.Hide();
        if (disableWhileOpen != null)
            foreach (var mb in disableWhileOpen) if (mb) mb.enabled = true;

        isOpen = false;

        // clear refs
        if (mortar) mortar.onClosed -= HandleClosedMortar;
        if (snakeStation) snakeStation.onClosed -= HandleClosedStation;
        if (snakeWorld) snakeWorld.onClosed -= HandleClosedWorld;
        if (alembic) alembic.onClosed.RemoveListener(HandleClosedAlembic);

        mortar = null;
        snakeStation = null;
        snakeWorld = null;
        alembic = null;

        // Turn off the minigame canvas (if your minigame didn't already)
        if (minigameRoot) minigameRoot.SetActive(false);
    }

    private void HandleClosedMortar()
    {
        HandleClosedCommon();
    }

    private void HandleClosedStation()
    {
        HandleClosedCommon();
    }

    private void HandleClosedWorld()
    {
        HandleClosedCommon();
    }

    // UnityEvent callback (no args) for Alembic
    private void HandleClosedAlembic()
    {
        HandleClosedCommon();
    }

    private void OnDisable()
    {
        // Safety: unsubscribe and restore UI on disable
        if (mortar) mortar.onClosed -= HandleClosedMortar;
        if (snakeStation) snakeStation.onClosed -= HandleClosedStation;
        if (snakeWorld) snakeWorld.onClosed -= HandleClosedWorld;
        if (alembic) alembic.onClosed.RemoveListener(HandleClosedAlembic);

        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        if (dimmer) dimmer.Hide();
        if (disableWhileOpen != null)
            foreach (var mb in disableWhileOpen) if (mb) mb.enabled = true;

        isOpen = false;
    }
}
