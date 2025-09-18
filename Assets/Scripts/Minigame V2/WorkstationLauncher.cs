using UnityEngine;

[DisallowMultipleComponent]
public class WorkstationLauncher : MonoBehaviour
{
    [Header("Scene Objects (no prefabs here)")]
    [SerializeField] private Canvas hudCanvas;          // your top HUD/slots canvas
    [SerializeField] private GameObject minigameRoot;   // the minigame Canvas root (set inactive at start)

    [Header("Options")]
    public bool hideHudWhileOpen = true;

    // controllers (one of these will be found)
    private MortarPestleMinigame mortar;
    private SnakeStationMinigame snakeStation;
    private SnakeWorldMinigame snakeWorld;
    private PhilosophersAlembicMinigame alembic; // NEW

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
        alembic = minigameRoot.GetComponentInChildren<PhilosophersAlembicMinigame>(true); // NEW

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
        else if (alembic) // NEW
        {
            // Make sure its owning refs are set for consistent close behavior
            if (alembic.owningRoot == null) alembic.owningRoot = minigameRoot;
            if (alembic.owningCanvas == null) alembic.owningCanvas = minigameRoot.GetComponent<Canvas>();

            // Alembic uses a UnityEvent onClosed
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

        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(false);
        isOpen = true;
    }

    private void HandleClosedCommon()
    {
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        isOpen = false;

        // clear refs
        mortar = null;
        snakeStation = null;
        snakeWorld = null;
        alembic = null;
    }

    private void HandleClosedMortar()
    {
        if (mortar) mortar.onClosed -= HandleClosedMortar;
        HandleClosedCommon();
    }

    private void HandleClosedStation()
    {
        if (snakeStation) snakeStation.onClosed -= HandleClosedStation;
        HandleClosedCommon();
    }

    private void HandleClosedWorld()
    {
        if (snakeWorld) snakeWorld.onClosed -= HandleClosedWorld;
        HandleClosedCommon();
    }

    // NEW: UnityEvent callback (no args)
    private void HandleClosedAlembic()
    {
        if (alembic) alembic.onClosed.RemoveListener(HandleClosedAlembic);
        HandleClosedCommon();
    }

    private void OnDisable()
    {
        if (mortar) mortar.onClosed -= HandleClosedMortar;
        if (snakeStation) snakeStation.onClosed -= HandleClosedStation;
        if (snakeWorld) snakeWorld.onClosed -= HandleClosedWorld;
        if (alembic) alembic.onClosed.RemoveListener(HandleClosedAlembic); // NEW

        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        isOpen = false;
    }
}
