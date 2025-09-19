// Assets/Scripts/Utility/DeathFadeDriver.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;   // <-- NEW

[DisallowMultipleComponent]
public class DeathFadeDriver : MonoBehaviour
{
    [SerializeField] private PlayerDeathController death;

    [Header("Fade Timing")]
    [Tooltip("Wait this long (seconds) after death starts before beginning the fade out.")]
    [SerializeField] private float delayBeforeFadeOut = 0f;
    [Tooltip("Seconds to fade to black once death starts (after the delay).")]
    [SerializeField] private float fadeOutOnDeathStart = 0.6f;
    [Tooltip("How long to hold on black before the load/respawn action (minimum).")]
    [SerializeField] private float holdBlackBeforeRespawn = 0.25f;
    [Tooltip("Seconds to fade back in after the load/respawn action.")]
    [SerializeField] private float fadeInAfterRespawn = 0.6f;

    public enum RespawnMode
    {
        None,
        ReloadScene,
        TeleportToSpawnPoint,
        LoadSceneByName
    }

    [Header("What to do after death")]
    [SerializeField] private RespawnMode respawn = RespawnMode.LoadSceneByName;
    [Tooltip("Used only when RespawnMode = LoadSceneByName. Example: 'MainMenu'")]
    [SerializeField] private string sceneNameToLoad = "MainMenu";
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Scripts on the player that should be re-enabled after respawn/teleport/reload. Not used for LoadSceneByName.")]
    [SerializeField] private MonoBehaviour[] reEnableAfter;

    // ---------------- NEW: Death Quote overlay ----------------
    [Header("Death Quote (optional)")]
    [SerializeField] private bool showDeathQuote = true;
    [Tooltip("CanvasGroup on the DeathQuoteOverlay UI.")]
    [SerializeField] private CanvasGroup quoteCanvasGroup;
    [Tooltip("TMP text that displays the line.")]
    [SerializeField] private TMP_Text quoteText;

    [Tooltip("Lines to pick from. If empty, feature is skipped even if enabled.")]
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
    [Tooltip("Start showing the quote this long after we finish fading to black.")]
    [SerializeField] private float textDelayAfterBlack = 0.0f;
    [Tooltip("Seconds to fade the quote in.")]
    [SerializeField] private float textFadeIn = 0.25f;
    [Tooltip("How long the quote stays fully visible.")]
    [SerializeField] private float textHold = 1.25f;
    [Tooltip("Seconds to fade the quote out (before respawn).")]
    [SerializeField] private float textFadeOut = 0.25f;
    [Tooltip("Pick a random line when true; otherwise use index 0.")]
    [SerializeField] private bool randomizeQuote = true;

    private bool quoteSequenceDone = true;
    private Coroutine quoteRoutine;

    private void Awake()
    {
        if (!death) death = GetComponent<PlayerDeathController>();

        // Ensure quote overlay starts hidden if assigned
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
        // Kick the fade to black
        StartCoroutine(FadeOutAfterDelay());

        // Kick the quote sequence (it will internally wait for black timing)
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

    private IEnumerator FadeOutAfterDelay()
    {
        if (delayBeforeFadeOut > 0f)
            yield return new WaitForSeconds(delayBeforeFadeOut);

        var fader = ScreenFader.CreateDefault();
        fader.FadeOut(fadeOutOnDeathStart);
    }

    private void OnDeathFinished()
    {
        StartCoroutine(RespawnAndFadeIn());
    }

    private IEnumerator RespawnAndFadeIn()
    {
        // Wait at least the configured black-hold AND until the quote has finished (if enabled)
        float t = 0f;
        while (t < holdBlackBeforeRespawn || (showDeathQuote && !quoteSequenceDone))
        {
            t += Time.unscaledDeltaTime; // be resilient to timescale changes
            yield return null;
        }

        PlayerDeathController targetDeath = death;

        switch (respawn)
        {
            case RespawnMode.ReloadScene:
                {
                    var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                    while (!op.isDone) yield return null;
                    yield return null; // settle a frame
                    targetDeath = Object.FindFirstObjectByType<PlayerDeathController>();
                    break;
                }

            case RespawnMode.TeleportToSpawnPoint:
                {
                    if (spawnPoint)
                    {
                        transform.position = spawnPoint.position;
                        transform.rotation = spawnPoint.rotation;
                    }
                    break;
                }

            case RespawnMode.LoadSceneByName:
                {
                    if (!string.IsNullOrWhiteSpace(sceneNameToLoad))
                    {
                        var op = SceneManager.LoadSceneAsync(sceneNameToLoad);
                        while (!op.isDone) yield return null;
                        yield return null;
                    }
                    targetDeath = null; // different scene; don't touch old components
                    break;
                }

            case RespawnMode.None:
            default:
                // Just continue to fade in below.
                break;
        }

        if (respawn == RespawnMode.ReloadScene || respawn == RespawnMode.TeleportToSpawnPoint)
        {
            if (targetDeath) targetDeath.ForceIdleNow();

            if (reEnableAfter != null)
                foreach (var mb in reEnableAfter) if (mb) mb.enabled = true;
        }

        // Safety: ensure quote overlay is hidden before fade-in
        if (quoteCanvasGroup) quoteCanvasGroup.alpha = 0f;

        ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
    }

    // ---------------- helpers ----------------

    private IEnumerator PlayDeathQuoteSequence()
    {
        quoteSequenceDone = false;

        // Choose a line
        string line = deathLines[Mathf.Clamp(
            randomizeQuote ? Random.Range(0, deathLines.Length) : 0,
            0, deathLines.Length - 1)];

        // Wait for full black moment: (delayBeforeFadeOut + fadeOutOnDeathStart) + optional extra delay
        float waitToBlack = Mathf.Max(0f, delayBeforeFadeOut + fadeOutOnDeathStart + textDelayAfterBlack);
        float t = 0f;
        while (t < waitToBlack)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade in text
        quoteText.text = line;
        yield return FadeCanvasGroup(quoteCanvasGroup, from: 0f, to: 1f, duration: textFadeIn);

        // Hold
        t = 0f;
        while (t < textHold)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out text
        yield return FadeCanvasGroup(quoteCanvasGroup, from: 1f, to: 0f, duration: textFadeOut);

        // Clear
        quoteText.text = string.Empty;
        quoteSequenceDone = true;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (!cg)
        {
            yield break;
        }

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
