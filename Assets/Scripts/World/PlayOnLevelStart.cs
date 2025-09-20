// Assets/Scripts/Utility/PlayOnLevelStart.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayOnLevelStart : MonoBehaviour
{
    [Header("Animator (optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool useTrigger = true;
    [SerializeField] private string triggerName = "Play";
    [SerializeField] private string stateName = ""; // if not using trigger, play this state name

    [Header("VFX (optional)")]
    [SerializeField] private ParticleSystem particles;

    [Header("Audio (Primary Random Variant)")]
    [Tooltip("AudioSource used for playback. If empty, one will be added on this object.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Randomly chooses one of these for the main sound.")]
    [SerializeField] private AudioClip[] variants;
    [Tooltip("Use PlayOneShot so the clip doesn't replace what's already on the AudioSource.")]
    [SerializeField] private bool usePlayOneShot = true;

    [Header("Primary Randomization")]
    [Tooltip("Volume applied to the primary clip before jitter.")]
    [Range(0f, 1f)][SerializeField] private float baseVolume = 1f;
    [Tooltip("Multiplier range applied to baseVolume.")]
    [SerializeField] private Vector2 volumeJitter = new Vector2(0.95f, 1.05f);
    [Tooltip("Pitch range for the primary clip.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);

    [Header("Repeat Control")]
    [Tooltip("Avoid playing the exact same clip twice in a row.")]
    [SerializeField] private bool avoidImmediateRepeat = true;
    [Tooltip("Use a 'bag' so all clips are played before any repeats.")]
    [SerializeField] private bool exhaustBeforeRepeat = false;

    [Header("Optional Accent Layer (plays on top)")]
    [Tooltip("If provided, one will be chosen at random to play as a layered accent.")]
    [SerializeField] private AudioClip[] accentClips;
    [Tooltip("0..1 chance to also play an accent on top of the primary.")]
    [Range(0f, 1f)][SerializeField] private float accentChance = 0.45f;
    [Tooltip("Delay window for the accent (seconds).")]
    [SerializeField] private Vector2 accentDelayRange = new Vector2(0.02f, 0.12f);
    [Tooltip("Accent volume multiplier range (applied to baseVolume).")]
    [SerializeField] private Vector2 accentVolumeMulRange = new Vector2(0.6f, 0.9f);
    [Tooltip("Accent pitch range.")]
    [SerializeField] private Vector2 accentPitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("Optional separate AudioSource for accent. If null, reuses the primary source (requires PlayOneShot).")]
    [SerializeField] private AudioSource accentSourceOverride;

    [Header("Timing")]
    [Tooltip("Base delay before the animation + SFX start.")]
    [SerializeField] private float delay = 0f;
    [Tooltip("Extra random delay added on top (min..max).")]
    [SerializeField] private Vector2 delayJitter = new Vector2(0f, 0.15f);
    [SerializeField] private bool onlyOnce = true;
    [Tooltip("Use unscaled time for all waits (safer if timescale is 0 at boot).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Intro Hook")]
    [Tooltip("Wait for SceneIntro.OnLevelIntro; if missed/no intro, auto-fire after a timeout.")]
    [SerializeField] private bool startAfterSceneIntro = true;
    [Tooltip("Seconds to wait for the intro event before auto-firing anyway.")]
    [SerializeField] private float introWaitTimeout = 1.0f;
    [Tooltip("Verbose debug logs in builds.")]
    [SerializeField] private bool verbose = false;

    [Header("Events")]
    public UnityEvent onIntro;

    // ---- runtime ----
    private bool fired;
    private bool armed;                    // we started waiting for intro or timeout
    private int lastVariantIndex = -1;
    private List<int> bag;                 // used when exhaustBeforeRepeat = true
    private Coroutine waitIntroCo;

    void Reset()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        particles = GetComponent<ParticleSystem>();
    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!audioSource)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        if (accentSourceOverride) accentSourceOverride.playOnAwake = false;

        // Sanitize ranges
        if (volumeJitter.x <= 0f) volumeJitter.x = 0.01f;
        if (volumeJitter.y <= 0f) volumeJitter.y = 0.01f;
        if (pitchRange.x <= 0f) pitchRange.x = 0.01f;
        if (pitchRange.y <= 0f) pitchRange.y = 0.01f;

        if (accentPitchRange.x <= 0f) accentPitchRange.x = 0.01f;
        if (accentPitchRange.y <= 0f) accentPitchRange.y = 0.01f;

        if (delayJitter.x > delayJitter.y) (delayJitter.x, delayJitter.y) = (delayJitter.y, delayJitter.x);
        if (accentDelayRange.x > accentDelayRange.y) (accentDelayRange.x, accentDelayRange.y) = (accentDelayRange.y, accentDelayRange.x);
    }

    void OnEnable()
    {
        if (startAfterSceneIntro)
        {
            SceneIntro.OnLevelIntro += HandleIntro;

            // If SceneIntro is missing or has already fired before we subscribed, fallback via timeout.
            if (waitIntroCo != null) StopCoroutine(waitIntroCo);
            waitIntroCo = StartCoroutine(CoWaitIntroOrTimeout());
        }
        else
        {
            // No intro dependency: fire immediately via our normal flow
            HandleIntro();
        }
    }

    void OnDisable()
    {
        SceneIntro.OnLevelIntro -= HandleIntro;
        if (waitIntroCo != null) StopCoroutine(waitIntroCo);
        waitIntroCo = null;
    }

    private IEnumerator CoWaitIntroOrTimeout()
    {
        armed = true;

        // If there is no SceneIntro in the scene, just wait the timeout then run.
        var intro = FindObjectOfType<SceneIntro>(true);
        float t = 0f;

        while (!fired && t < introWaitTimeout)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (!fired)
        {
            if (verbose) Debug.Log("[PlayOnLevelStart] Intro event missed/absent — auto-firing.");
            HandleIntro();
        }

        waitIntroCo = null;
        armed = false;
    }

    void HandleIntro()
    {
        if (fired && onlyOnce) return;
        if (verbose) Debug.Log("[PlayOnLevelStart] HandleIntro");
        StartCoroutine(CoPlay());
        fired = true;
    }

    IEnumerator CoPlay()
    {
        float extra = (delayJitter.y > 0f) ? Random.Range(delayJitter.x, delayJitter.y) : 0f;
        float wait = Mathf.Max(0f, delay + extra);
        if (wait > 0f)
        {
            if (useUnscaledTime)
            {
                float t = 0f; while (t < wait) { t += Time.unscaledDeltaTime; yield return null; }
            }
            else
            {
                yield return new WaitForSeconds(wait);
            }
        }

        // Animation
        if (animator)
        {
            if (useTrigger && !string.IsNullOrEmpty(triggerName))
            {
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
            }
            else if (!string.IsNullOrEmpty(stateName))
            {
                animator.Play(stateName, 0, 0f);
            }
        }

        // SFX (primary)
        PlayPrimaryVariant();

        // Accent (optional)
        TryPlayAccent();

        // VFX
        if (particles) particles.Play();

        // Event
        onIntro?.Invoke();
    }

    // --------------------- AUDIO CORE ---------------------
    private void PlayPrimaryVariant()
    {
        if (!audioSource || variants == null || variants.Length == 0) return;

        var clip = PickVariant();
        if (!clip) return;

        float volMul = Random.Range(volumeJitter.x, volumeJitter.y);
        float pitch = Random.Range(pitchRange.x, pitchRange.y);
        float volume = Mathf.Clamp01(baseVolume * volMul);

        if (usePlayOneShot)
        {
            float originalPitch = audioSource.pitch;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
            audioSource.pitch = originalPitch;
        }
        else
        {
            audioSource.clip = clip;
            audioSource.pitch = pitch;
            audioSource.volume = volume;
            audioSource.Play();
        }
    }

    private void TryPlayAccent()
    {
        if (accentClips == null || accentClips.Length == 0) return;
        if (Random.value > accentChance) return;

        var accentClip = accentClips[Random.Range(0, accentClips.Length)];
        if (!accentClip) return;

        float delay = Random.Range(accentDelayRange.x, accentDelayRange.y);
        float volMul = Random.Range(accentVolumeMulRange.x, accentVolumeMulRange.y);
        float pitch = Random.Range(accentPitchRange.x, accentPitchRange.y);
        float volume = Mathf.Clamp01(baseVolume * volMul);

        var src = accentSourceOverride ? accentSourceOverride : audioSource;
        if (!src) return;

        if (!usePlayOneShot && src == audioSource && accentSourceOverride == null)
            return; // would stomp the primary

        StartCoroutine(PlayAccentDelayed(src, accentClip, volume, pitch, delay));
    }

    private IEnumerator PlayAccentDelayed(AudioSource src, AudioClip clip, float volume, float pitch, float delay)
    {
        if (delay > 0f)
        {
            if (useUnscaledTime)
            {
                float t = 0f; while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
            }
            else
            {
                yield return new WaitForSeconds(delay);
            }
        }
        float old = src.pitch;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
        src.pitch = old;
    }

    // --------------------- RANDOM SELECTION ---------------------
    private AudioClip PickVariant()
    {
        if (variants == null || variants.Length == 0) return null;
        int n = variants.Length;

        if (exhaustBeforeRepeat)
        {
            if (bag == null) bag = new List<int>(n);
            if (bag.Count == 0)
            {
                bag.Clear();
                for (int i = 0; i < n; i++) bag.Add(i);
                if (avoidImmediateRepeat && n > 1 && lastVariantIndex >= 0)
                    bag.Remove(lastVariantIndex);
            }

            int pick = Random.Range(0, bag.Count);
            int index = bag[pick];
            bag.RemoveAt(pick);

            lastVariantIndex = index;
            return variants[index];
        }
        else
        {
            int index;
            if (avoidImmediateRepeat && n > 1)
            {
                do { index = Random.Range(0, n); }
                while (index == lastVariantIndex);
            }
            else
            {
                index = Random.Range(0, n);
            }

            lastVariantIndex = index;
            return variants[index];
        }
    }
}
