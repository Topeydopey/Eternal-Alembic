// Assets/Scripts/Utility/DeathFadeDriver.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class DeathFadeDriver : MonoBehaviour
{
    [SerializeField] private PlayerDeathController death;

    [Header("Fade Timing")]
    [SerializeField] private float delayBeforeFadeOut = 0f;
    [SerializeField] private float fadeOutOnDeathStart = 0.6f;
    [SerializeField] private float holdBlackBeforeRespawn = 0.25f;
    [SerializeField] private float fadeInAfterRespawn = 0.6f;

    public enum RespawnMode
    {
        None,
        ReloadScene,
        TeleportToSpawnPoint,
        LoadSceneByName,
        HardRestartApp           // <— fully quit and relaunch the game
    }

    [Header("What to do after death")]
    [SerializeField] private RespawnMode respawn = RespawnMode.HardRestartApp; // default to restart
    [Tooltip("Used only when RespawnMode = LoadSceneByName. Example: 'MainMenu'")]
    [SerializeField] private string sceneNameToLoad = "MainMenu";
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private MonoBehaviour[] reEnableAfter;

    // --- NEW: Disable objects on death start ---
    [Header("Disable Objects on Death Start")]
    [Tooltip("All of these GameObjects will be SetActive(false) immediately when death starts (e.g., a stubborn Canvas root).")]
    [SerializeField] private GameObject[] disableOnDeathStart;

    /// <summary>Add a target at runtime to be disabled when death starts.</summary>
    public void RegisterDisableOnDeath(GameObject go)
    {
        if (!go) return;
        if (disableOnDeathStart == null)
        {
            disableOnDeathStart = new[] { go };
            return;
        }
        // expand array
        var old = disableOnDeathStart;
        var arr = new GameObject[old.Length + 1];
        for (int i = 0; i < old.Length; i++) arr[i] = old[i];
        arr[arr.Length - 1] = go;
        disableOnDeathStart = arr;
    }

    // ---------------- Death Quote overlay ----------------
    [Header("Death Quote (optional)")]
    [SerializeField] private bool showDeathQuote = true;
    [SerializeField] private CanvasGroup quoteCanvasGroup;
    [SerializeField] private TMP_Text quoteText;

    [TextArea(2, 3)]
    [SerializeField]
    private string[] deathLines = new string[]
    {
        "What eats is what is eaten.",
        "The end coils into the beginning.",
        "To consume is to become.",
        "Ash to seed, seed to ash."
    };

    [Header("Quote Timing")]
    [SerializeField] private float textDelayAfterBlack = 0.0f;
    [SerializeField] private float textFadeIn = 0.25f;
    [SerializeField] private float textHold = 1.25f;
    [SerializeField] private float textFadeOut = 0.25f;
    [SerializeField] private bool randomizeQuote = true;

    private bool quoteSequenceDone = true;
    private Coroutine quoteRoutine;

    private void Awake()
    {
        if (!death) death = GetComponent<PlayerDeathController>();

        if (quoteCanvasGroup)
        {
            quoteCanvasGroup.alpha = 0f;
            quoteCanvasGroup.interactable = false;
            quoteCanvasGroup.blocksRaycasts = false;
        }
        if (quoteText) quoteText.text = string.Empty;
    }

    private void OnEnable()
    {
        if (!death) return;
        death.onDeathStarted.AddListener(OnDeathStarted);
        death.onDeathFinished.AddListener(OnDeathFinished);
    }

    private void OnDisable()
    {
        if (!death) return;
        death.onDeathStarted.RemoveListener(OnDeathStarted);
        death.onDeathFinished.RemoveListener(OnDeathFinished);
    }

    private void OnDeathStarted()
    {
        // NEW: hard-disable any stubborn UIs/canvases immediately
        DisableTargetsNow();

        // Fade to black
        StartCoroutine(FadeOutAfterDelay());

        // Optional quote
        if (showDeathQuote && quoteCanvasGroup && quoteText && deathLines != null && deathLines.Length > 0)
        {
            if (quoteRoutine != null) StopCoroutine(quoteRoutine);
            quoteRoutine = StartCoroutine(PlayDeathQuoteSequence());
        }
        else
        {
            quoteSequenceDone = true;
        }
    }

    private void DisableTargetsNow()
    {
        if (disableOnDeathStart == null) return;
        for (int i = 0; i < disableOnDeathStart.Length; i++)
        {
            var go = disableOnDeathStart[i];
            if (!go) continue;

            // Full off (covers all raycast blockers, canvases, etc.)
            if (go.activeSelf) go.SetActive(false);

            // Extra safety: if they left a CanvasGroup enabled on a still-active parent, nuke its raycasts.
            var cg = go.GetComponent<CanvasGroup>();
            if (cg)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
        }
    }

    private IEnumerator FadeOutAfterDelay()
    {
        if (delayBeforeFadeOut > 0f)
            yield return new WaitForSeconds(delayBeforeFadeOut);

        var fader = ScreenFader.CreateDefault();
        fader.FadeOut(fadeOutOnDeathStart);
    }

    private void OnDeathFinished()
    {
        StartCoroutine(RespawnAndFadeInOrRestart());
    }

    private IEnumerator RespawnAndFadeInOrRestart()
    {
        // Wait minimum on black AND (if enabled) for quote to finish
        float t = 0f;
        while (t < holdBlackBeforeRespawn || (showDeathQuote && !quoteSequenceDone))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Ensure quote UI is hidden before proceeding
        if (quoteCanvasGroup) quoteCanvasGroup.alpha = 0f;

        switch (respawn)
        {
            case RespawnMode.HardRestartApp:
                AppRelauncher.Relaunch(0f);
                yield break;

            case RespawnMode.ReloadScene:
                {
                    var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                    while (!op.isDone) yield return null;
                    yield return null; // settle one frame
                    var targetDeath = Object.FindFirstObjectByType<PlayerDeathController>();
                    if (targetDeath) targetDeath.ForceIdleNow();
                    if (reEnableAfter != null)
                        foreach (var mb in reEnableAfter) if (mb) mb.enabled = true;
                    ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
                    yield break;
                }

            case RespawnMode.LoadSceneByName:
                {
                    if (!string.IsNullOrWhiteSpace(sceneNameToLoad))
                    {
                        var op = SceneManager.LoadSceneAsync(sceneNameToLoad);
                        while (!op.isDone) yield return null;
                        yield return null;
                    }
                    ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
                    yield break;
                }

            case RespawnMode.TeleportToSpawnPoint:
                {
                    if (spawnPoint)
                    {
                        transform.position = spawnPoint.position;
                        transform.rotation = spawnPoint.rotation;
                    }
                    if (death) death.ForceIdleNow();
                    if (reEnableAfter != null)
                        foreach (var mb in reEnableAfter) if (mb) mb.enabled = true;

                    ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
                    yield break;
                }

            case RespawnMode.None:
            default:
                ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
                yield break;
        }
    }

    // ---------------- helpers ----------------

    private IEnumerator PlayDeathQuoteSequence()
    {
        quoteSequenceDone = false;

        // Pick a line
        string line = deathLines[Mathf.Clamp(
            randomizeQuote ? Random.Range(0, deathLines.Length) : 0,
            0, deathLines.Length - 1)];

        // Wait until fully black (+ optional extra)
        float waitToBlack = Mathf.Max(0f, delayBeforeFadeOut + fadeOutOnDeathStart + textDelayAfterBlack);
        float t = 0f;
        while (t < waitToBlack)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade text in, hold, out
        quoteText.text = line;
        yield return FadeCanvasGroup(quoteCanvasGroup, 0f, 1f, textFadeIn);

        t = 0f;
        while (t < textHold)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeCanvasGroup(quoteCanvasGroup, 1f, 0f, textFadeOut);

        quoteText.text = string.Empty;
        quoteSequenceDone = true;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (!cg) yield break;

        cg.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            cg.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }
        cg.alpha = to;
    }
}
