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

    [Header("Single Use")]
    [Tooltip("If true, this workstation can only be used once (after success).")]
    [SerializeField] private bool onceOnly = true;
    [Tooltip("Optional PlayerPrefs key to persist 'consumed' state across sessions. Leave empty to keep runtime-only.")]
    [SerializeField] private string persistKey;
    [Tooltip("Extra colliders to disable once consumed (e.g., trigger around the workstation).")]
    [SerializeField] private Collider2D[] collidersToDisableOnConsume;
    [Tooltip("Extra behaviours to disable once consumed (e.g., outline shader controller, hover highlighter).")]
    [SerializeField] private Behaviour[] behavioursToDisableOnConsume;

    // controllers (one of these will be found)
    private MortarPestleMinigame mortar;
    private SnakeStationMinigame snakeStation;
    private SnakeWorldMinigame snakeWorld;
    private PhilosophersAlembicMinigame alembic;

    private bool isOpen;
    private bool consumed;

    private void Awake()
    {
        if (!string.IsNullOrEmpty(persistKey))
            consumed = PlayerPrefs.GetInt(persistKey, 0) == 1;

        if (consumed)
            DisableForever();
    }

    public void Launch()
    {
        if (!minigameRoot)
        {
            Debug.LogError("[Launcher] minigameRoot not assigned.");
            return;
        }
        if (isOpen) return;

        // Block if already consumed and onceOnly
        if (onceOnly && consumed)
        {
            // Optional: play a denied SFX or show a tooltip here
            return;
        }

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
            mortar.onSucceeded += HandleSucceeded; // 🔔 listen for success
            mortar.BeginSession();
            Debug.Log("[Launcher] Opened MortarPestleMinigame");
        }
        else if (snakeStation)
        {
            snakeStation.SetReuseMode(true, minigameRoot);
            snakeStation.onClosed += HandleClosedStation;
            // If you add onSucceeded to SnakeStationMinigame, subscribe here too.
            snakeStation.BeginSession();
            Debug.Log("[Launcher] Opened SnakeStationMinigame");
        }
        else if (snakeWorld)
        {
            snakeWorld.disableInsteadOfDestroy = true;
            if (snakeWorld.owningRoot == null) snakeWorld.owningRoot = minigameRoot;
            if (snakeWorld.owningCanvas == null) snakeWorld.owningCanvas = minigameRoot.GetComponent<Canvas>();

            snakeWorld.onClosed += HandleClosedWorld;
            // If you add onSucceeded to SnakeWorldMinigame, subscribe here too.
            snakeWorld.BeginSession();
            Debug.Log("[Launcher] Opened SnakeWorldMinigame");
        }
        else if (alembic)
        {
            if (alembic.owningRoot == null) alembic.owningRoot = minigameRoot;
            if (alembic.owningCanvas == null) alembic.owningCanvas = minigameRoot.GetComponent<Canvas>();

            alembic.onClosed.AddListener(HandleClosedAlembic);
            // If you add onSucceeded UnityEvent on Alembic, add a listener to HandleSucceeded as well.
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
        if (dimmer) dimmer.Show();
        if (disableWhileOpen != null)
            foreach (var mb in disableWhileOpen) if (mb) mb.enabled = false;

        isOpen = true;
    }

    private void HandleClosedCommon()
    {
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        if (dimmer) dimmer.Hide();
        if (disableWhileOpen != null)
            foreach (var mb in disableWhileOpen) if (mb) mb.enabled = true;

        isOpen = false;

        if (mortar) { mortar.onClosed -= HandleClosedMortar; mortar.onSucceeded -= HandleSucceeded; }
        if (snakeStation) snakeStation.onClosed -= HandleClosedStation;
        if (snakeWorld) snakeWorld.onClosed -= HandleClosedWorld;
        if (alembic) alembic.onClosed.RemoveListener(HandleClosedAlembic);

        mortar = null;
        snakeStation = null;
        snakeWorld = null;
        alembic = null;

        if (minigameRoot) minigameRoot.SetActive(false);
    }

    private void HandleClosedMortar() => HandleClosedCommon();
    private void HandleClosedStation() => HandleClosedCommon();
    private void HandleClosedWorld() => HandleClosedCommon();
    private void HandleClosedAlembic() => HandleClosedCommon();

    // 🔒 Called when the minigame reports success (result item awarded)
    private void HandleSucceeded()
    {
        if (!onceOnly) return;

        consumed = true;
        if (!string.IsNullOrEmpty(persistKey))
        {
            PlayerPrefs.SetInt(persistKey, 1);
            PlayerPrefs.Save();
        }

        DisableForever();
    }

    private void DisableForever()
    {
        // Turn off in-world interaction
        var myColliders = GetComponents<Collider2D>();
        foreach (var c in myColliders) if (c) c.enabled = false;
        if (collidersToDisableOnConsume != null)
            foreach (var c in collidersToDisableOnConsume) if (c) c.enabled = false;

        if (behavioursToDisableOnConsume != null)
            foreach (var b in behavioursToDisableOnConsume) if (b) b.enabled = false;

        // Optional: also hide any prompt/outline VFX you have here

        // If the minigame is currently open, let normal close flow restore HUD/dimmer etc.
    }

    private void OnDisable()
    {
        if (mortar) { mortar.onClosed -= HandleClosedMortar; mortar.onSucceeded -= HandleSucceeded; }
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
