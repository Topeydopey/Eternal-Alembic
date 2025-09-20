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
        LoadSceneByName
    }

    [Header("What to do after death")]
    [SerializeField] private RespawnMode respawn = RespawnMode.LoadSceneByName;
    [Tooltip("Used only when RespawnMode = LoadSceneByName. Example: 'MainMenu'")]
    [SerializeField] private string sceneNameToLoad = "MainMenu";
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private MonoBehaviour[] reEnableAfter;

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
        // Begin fade to black
        StartCoroutine(FadeOutAfterDelay());

        // Begin quote (optional)
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
        // Wait for minimum black time and (if enabled) until the quote completes.
        float t = 0f;
        while (t < holdBlackBeforeRespawn || (showDeathQuote && !quoteSequenceDone))
        {
            t += Time.unscaledDeltaTime; // resilient to Time.timeScale changes
            yield return null;
        }

        // Hide quote UI before we leave this scene or fade back in
        if (quoteCanvasGroup) quoteCanvasGroup.alpha = 0f;

        // Branch by mode
        switch (respawn)
        {
            case RespawnMode.ReloadScene:
                {
                    // Make sure a fader exists and arrange a cross-scene fade-in.
                    EnsureCrossSceneFadeIn(fadeInAfterRespawn);

                    var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                    while (!op.isDone) yield return null;
                    // No more code after this point is guaranteed to run (this object is destroyed on load).
                    yield break;
                }

            case RespawnMode.LoadSceneByName:
                {
                    EnsureCrossSceneFadeIn(fadeInAfterRespawn);

                    if (!string.IsNullOrWhiteSpace(sceneNameToLoad))
                    {
                        var op = SceneManager.LoadSceneAsync(sceneNameToLoad);
                        while (!op.isDone) yield return null;
                    }
                    yield break;
                }

            case RespawnMode.TeleportToSpawnPoint:
                {
                    if (spawnPoint)
                    {
                        transform.position = spawnPoint.position;
                        transform.rotation = spawnPoint.rotation;
                    }

                    // Return the player's animator to idle and re-enable scripts (same-scene only)
                    if (death) death.ForceIdleNow();

                    if (reEnableAfter != null)
                        foreach (var mb in reEnableAfter) if (mb) mb.enabled = true;

                    ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
                    yield break;
                }

            case RespawnMode.None:
            default:
                {
                    ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
                    yield break;
                }
        }
    }

    // ---------------- helpers ----------------

    private IEnumerator PlayDeathQuoteSequence()
    {
        quoteSequenceDone = false;

        // Choose a line
        string line = deathLines[Mathf.Clamp(
            randomizeQuote ? Random.Range(0, deathLines.Length) : 0,
            0, deathLines.Length - 1)];

        // Wait for full black time + optional extra delay
        float waitToBlack = Mathf.Max(0f, delayBeforeFadeOut + fadeOutOnDeathStart + textDelayAfterBlack);
        float t = 0f;
        while (t < waitToBlack)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade in text
        quoteText.text = line;
        yield return FadeCanvasGroup(quoteCanvasGroup, 0f, 1f, textFadeIn);

        // Hold
        t = 0f;
        while (t < textHold)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out text
        yield return FadeCanvasGroup(quoteCanvasGroup, 1f, 0f, textFadeOut);

        // Clear
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

    /// <summary>
    /// Ensures there's a ScreenFader, and spawns a tiny helper that will call FadeIn in the *next* scene.
    /// </summary>
    private static void EnsureCrossSceneFadeIn(float fadeInDuration)
    {
        ScreenFader.CreateDefault(); // ensure exists now (and persists)

        var go = new GameObject("CrossSceneFadeCarrier");
        var carrier = go.AddComponent<CrossSceneFadeCarrier>();
        carrier.fadeInDuration = fadeInDuration;
        DontDestroyOnLoad(go);
    }

    private sealed class CrossSceneFadeCarrier : MonoBehaviour
    {
        [HideInInspector] public float fadeInDuration = 0.6f;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnLoaded;
        }

        private void OnLoaded(Scene s, LoadSceneMode m)
        {
            // Fade the carried black away in the new scene
            ScreenFader.CreateDefault(); // if new scene didn't have one yet
            ScreenFader.Instance?.FadeIn(fadeInDuration);
            Destroy(gameObject); // one-shot
        }
    }
}
