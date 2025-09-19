// Assets/Scripts/Utility/DeathFadeDriver.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DeathFadeDriver : MonoBehaviour
{
    [SerializeField] private PlayerDeathController death;

    [Header("Fade Timing")]
    [Tooltip("Wait this long (seconds) after death starts before beginning the fade out.")]
    [SerializeField] private float delayBeforeFadeOut = 0f;
    [Tooltip("Seconds to fade to black once death starts (after the delay).")]
    [SerializeField] private float fadeOutOnDeathStart = 0.6f;
    [Tooltip("How long to hold on black before the load/respawn action.")]
    [SerializeField] private float holdBlackBeforeRespawn = 0.25f;
    [Tooltip("Seconds to fade back in after the load/respawn action.")]
    [SerializeField] private float fadeInAfterRespawn = 0.6f;

    public enum RespawnMode
    {
        None,
        ReloadScene,
        TeleportToSpawnPoint,
        LoadSceneByName        // <- NEW: go to main menu (or any named scene)
    }

    [Header("What to do after death")]
    [SerializeField] private RespawnMode respawn = RespawnMode.LoadSceneByName;
    [Tooltip("Used only when RespawnMode = LoadSceneByName. Example: 'MainMenu'")]
    [SerializeField] private string sceneNameToLoad = "MainMenu";
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Scripts on the player that should be re-enabled after respawn/teleport/reload. Not used for LoadSceneByName.")]
    [SerializeField] private MonoBehaviour[] reEnableAfter;

    private void Awake()
    {
        if (!death) death = GetComponent<PlayerDeathController>();
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
        // Start fade out after an optional pre-delay
        StartCoroutine(FadeOutAfterDelay());
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
        if (holdBlackBeforeRespawn > 0f)
            yield return new WaitForSeconds(holdBlackBeforeRespawn);

        PlayerDeathController targetDeath = death;

        switch (respawn)
        {
            case RespawnMode.ReloadScene:
                {
                    var op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                    while (!op.isDone) yield return null;
                    // Give the new scene a frame to settle
                    yield return null;

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
                        // Let the scene initialize one frame
                        yield return null;
                    }
                    // We’re going to a different scene; don’t try to re-enable player scripts from the old one.
                    targetDeath = null;
                    break;
                }

            case RespawnMode.None:
            default:
                // Do nothing; just fade back in below.
                break;
        }

        // Force the animator back to idle and clear 'dead' flags (for same-scene flows)
        if (respawn == RespawnMode.ReloadScene || respawn == RespawnMode.TeleportToSpawnPoint)
        {
            if (targetDeath) targetDeath.ForceIdleNow();

            // Re-enable any scripts you want back on after respawn
            if (reEnableAfter != null)
                foreach (var mb in reEnableAfter) if (mb) mb.enabled = true;
        }

        // Fade back to game (if your new scene also does its own opening fade, this is harmless)
        ScreenFader.Instance?.FadeIn(fadeInAfterRespawn);
    }
}
