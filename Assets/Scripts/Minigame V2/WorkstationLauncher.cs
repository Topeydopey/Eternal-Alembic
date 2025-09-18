// Assets/Scripts/Minigame V2/Common/WorkstationLauncher.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class WorkstationLauncher : MonoBehaviour
{
    public enum LockScope { PerRun, Permanent }

    [Header("Scene Objects")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private GameObject minigameRoot;

    [Header("Modal Dimmer")]
    [SerializeField] private MinigameDimmer dimmer;

    [Header("Disable While Open (optional)")]
    [SerializeField] private MonoBehaviour[] disableWhileOpen;

    [Header("While Open: Other Stations")]
    [SerializeField] private bool disableOtherStationsColliders = true;
    [SerializeField] private bool deactivateOtherStationsObjects = false;

    [Header("Options")]
    public bool hideHudWhileOpen = true;

    [Header("Single Use")]
    [SerializeField] private bool onceOnly = true;
    [SerializeField] private LockScope lockScope = LockScope.PerRun;
    [Tooltip("Station id used for locking. Use the SAME id across openers if you have multiple.")]
    [SerializeField] private string stationId = "workstation_default";
    [Tooltip("Only used if scope=Permanent; legacy local key (optional).")]
    [SerializeField] private string persistKey;

    [Header("Hands Gating (no pockets)")]
    [Tooltip("If true, player must have an empty active hand to open this station.")]
    [SerializeField] private bool requireEmptyHandToLaunch = true;
    [Tooltip("Optional: show a floating text hint above the player when blocked.")]
    [SerializeField] private bool showHintWhenBlocked = true;
    [Tooltip("Hint text to display when the player is holding an item.")]
    [SerializeField] private string blockedHintText = "Put your ingredient in the cauldron first";
    [Tooltip("Player transform used to position the hint. If empty, we'll try tag 'Player'.")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("World-space local offset above player's head for the hint.")]
    [SerializeField] private Vector3 hintLocalOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float hintFadeIn = 0.12f;
    [SerializeField] private float hintHold = 1.25f;
    [SerializeField] private float hintFadeOut = 0.25f;

    [Header("Optional: hard-disable when consumed")]
    [SerializeField] private bool disableCollidersOnConsume = false;
    [SerializeField] private Collider2D[] collidersToDisableOnConsume;
    [SerializeField] private Behaviour[] behavioursToDisableOnConsume;
    [SerializeField] private bool disableHighlightsOnConsume = true;

    [Header("Debug")]
    [SerializeField] private bool verbose = false;

    // controllers
    private MortarPestleMinigame mortar;
    private SnakeStationMinigame snakeStation;
    private SnakeWorldMinigame snakeWorld;
    private PhilosophersAlembicMinigame alembic;

    private bool isOpen;
    private bool consumed;

    private readonly List<Collider2D> _othersCollidersDisabled = new();
    private readonly List<GameObject> _othersObjectsDeactivated = new();

    private void Awake()
    {
        // Init consumed flag
        if (lockScope == LockScope.Permanent)
        {
            if (!string.IsNullOrEmpty(persistKey))
                consumed = PlayerPrefs.GetInt(persistKey, 0) == 1;
            if (!consumed && !string.IsNullOrEmpty(stationId) && WorkstationOnce.IsUsed(stationId))
                consumed = true;
        }
        else // PerRun
        {
            if (!string.IsNullOrEmpty(stationId) && RunOnce.IsUsed(stationId))
                consumed = true;
        }

        if (consumed)
        {
            if (disableHighlightsOnConsume) DisableConsumedHighlights();
            if (disableCollidersOnConsume) DisableForever();
        }

        // Try to cache player by tag if not set
        TryCachePlayer();
    }

    public void Launch()
    {
        if (!minigameRoot) { Debug.LogError("[Launcher] minigameRoot not assigned."); return; }
        if (isOpen) return;

        // Single-use gate
        if (onceOnly && !string.IsNullOrEmpty(stationId))
        {
            bool used = (lockScope == LockScope.PerRun)
                ? RunOnce.IsUsed(stationId)
                : (WorkstationOnce.IsUsed(stationId) ||
                   (!string.IsNullOrEmpty(persistKey) && PlayerPrefs.GetInt(persistKey, 0) == 1));
            if (used) return;
        }

        // Hands gate (no pockets). If holding something -> block & hint.
        if (requireEmptyHandToLaunch && !PlayerHandIsEmpty())
        {
            if (verbose) Debug.Log($"[Launcher:{name}] Launch blocked: hands full.");
            if (showHintWhenBlocked && playerTransform)
            {
                FloatingWorldHint.Show(playerTransform, blockedHintText, hintLocalOffset, hintFadeIn, hintHold, hintFadeOut);
            }
            return;
        }

        // Activate UI
        minigameRoot.SetActive(true);

        // Find controllers
        mortar = minigameRoot.GetComponentInChildren<MortarPestleMinigame>(true);
        snakeStation = minigameRoot.GetComponentInChildren<SnakeStationMinigame>(true);
        snakeWorld = minigameRoot.GetComponentInChildren<SnakeWorldMinigame>(true);
        alembic = minigameRoot.GetComponentInChildren<PhilosophersAlembicMinigame>(true);

        if (mortar)
        {
            mortar.SetReuseMode(true, minigameRoot);
            mortar.onClosed += HandleClosedMortar;
            mortar.onSucceeded += HandleSucceeded;
            mortar.BeginSession();
        }
        else if (snakeStation)
        {
            snakeStation.SetReuseMode(true, minigameRoot);
            snakeStation.onClosed += HandleClosedStation;
            snakeStation.BeginSession();
        }
        else if (snakeWorld)
        {
            snakeWorld.disableInsteadOfDestroy = true;
            if (!snakeWorld.owningRoot) snakeWorld.owningRoot = minigameRoot;
            if (!snakeWorld.owningCanvas) snakeWorld.owningCanvas = minigameRoot.GetComponent<Canvas>();

            snakeWorld.onClosed += HandleClosedWorld;
            snakeWorld.onSuccess += HandleSucceeded;
            snakeWorld.BeginSession();
        }
        else if (alembic)
        {
            if (!alembic.owningRoot) alembic.owningRoot = minigameRoot;
            if (!alembic.owningCanvas) alembic.owningCanvas = minigameRoot.GetComponent<Canvas>();

            alembic.onClosed.AddListener(HandleClosedAlembic);
            alembic.onSuccess += HandleSucceeded;
            alembic.BeginSession();
        }
        else
        {
            Debug.LogError("[Launcher] No known minigame under the canvas.");
            minigameRoot.SetActive(false);
            return;
        }

        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(false);
        if (dimmer) dimmer.Show();
        if (disableWhileOpen != null) foreach (var mb in disableWhileOpen) if (mb) mb.enabled = false;

        DisableOtherStations();
        isOpen = true;
    }

    // --- success path: mark used in selected scope ---
    private void HandleSucceeded()
    {
        if (!onceOnly || string.IsNullOrEmpty(stationId)) return;

        if (lockScope == LockScope.PerRun) RunOnce.MarkUsed(stationId);
        else
        {
            WorkstationOnce.MarkUsed(stationId);
            if (!string.IsNullOrEmpty(persistKey)) { PlayerPrefs.SetInt(persistKey, 1); PlayerPrefs.Save(); }
        }

        consumed = true;
        if (disableHighlightsOnConsume) DisableConsumedHighlights();
        if (disableCollidersOnConsume) DisableForever();
    }

    // --- common close cleanup ---
    private void HandleClosedCommon()
    {
        if (hideHudWhileOpen && hudCanvas) hudCanvas.gameObject.SetActive(true);
        if (dimmer) dimmer.Hide();
        if (disableWhileOpen != null) foreach (var mb in disableWhileOpen) if (mb) mb.enabled = true;

        RestoreOtherStations();
        isOpen = false;

        if (mortar) { mortar.onClosed -= HandleClosedMortar; mortar.onSucceeded -= HandleSucceeded; }
        if (snakeStation) { snakeStation.onClosed -= HandleClosedStation; }
        if (snakeWorld) { snakeWorld.onClosed -= HandleClosedWorld; snakeWorld.onSuccess -= HandleSucceeded; }
        if (alembic) { alembic.onClosed.RemoveListener(HandleClosedAlembic); alembic.onSuccess -= HandleSucceeded; }

        mortar = null; snakeStation = null; snakeWorld = null; alembic = null;
        if (minigameRoot) minigameRoot.SetActive(false);
    }

    private void HandleClosedMortar() => HandleClosedCommon();
    private void HandleClosedStation() => HandleClosedCommon();
    private void HandleClosedWorld() => HandleClosedCommon();
    private void HandleClosedAlembic() => HandleClosedCommon();

    // --- others gating ---
    private void DisableOtherStations()
    {
        _othersCollidersDisabled.Clear();
        _othersObjectsDeactivated.Clear();

        var all = FindObjectsOfType<WorkstationLauncher>(true);
        foreach (var ws in all)
        {
            if (ws == this) continue;
            if (!ws.gameObject.activeInHierarchy) continue;

            if (disableOtherStationsColliders)
            {
                var cols = ws.GetComponentsInChildren<Collider2D>(true);
                foreach (var c in cols) { if (c && c.enabled) { c.enabled = false; _othersCollidersDisabled.Add(c); } }
            }
            if (deactivateOtherStationsObjects && ws.gameObject.activeSelf)
            {
                ws.gameObject.SetActive(false);
                _othersObjectsDeactivated.Add(ws.gameObject);
            }
        }
    }

    private void RestoreOtherStations()
    {
        foreach (var c in _othersCollidersDisabled) if (c) c.enabled = true;
        _othersCollidersDisabled.Clear();

        foreach (var go in _othersObjectsDeactivated) if (go) go.SetActive(true);
        _othersObjectsDeactivated.Clear();
    }

    private void DisableForever()
    {
        var myColliders = GetComponents<Collider2D>();
        foreach (var c in myColliders) if (c) c.enabled = false;
        if (collidersToDisableOnConsume != null) foreach (var c in collidersToDisableOnConsume) if (c) c.enabled = false;

        DisableConsumedHighlights();
    }

    private void DisableConsumedHighlights()
    {
        if (!disableHighlightsOnConsume) return;
        if (behavioursToDisableOnConsume == null) return;
        foreach (var b in behavioursToDisableOnConsume) if (b) b.EnabledIfExists(false);
    }

    // --- helpers -------------------------------------------------------------

    // Replaces missing EquipmentInventory.IsActiveHandEmpty()
    private bool PlayerHandIsEmpty()
    {
        var eq = EquipmentInventory.Instance;
        if (!eq) return true;
        var slot = eq.Get(eq.activeHand);
        return slot == null || slot.IsEmpty;
    }

    private void TryCachePlayer()
    {
        if (playerTransform) return;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTransform = p.transform;
    }
}

// Small extension helper to safely enable/disable behaviours if present
static class BehaviourExt
{
    public static void EnabledIfExists(this Behaviour b, bool on)
    {
        if (b) b.enabled = on;
    }
}
