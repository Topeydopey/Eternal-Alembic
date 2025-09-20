// Assets/Scripts/Audio/AmbientMusicFader.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class AmbientMusicFader : MonoBehaviour
{
    [Header("Start Timing")]
    [Tooltip("Ignored for gating; ambience always starts on enable. Delay still applies below.")]
    [SerializeField] private bool startOnEnable = true;          // kept for backwards-compat (no gating)
    [Tooltip("IGNORED: SceneIntro is not used anymore; ambience starts unconditionally.")]
    [SerializeField] private bool startAfterSceneIntro = false;  // kept so old scenes don't break
    [SerializeField, Min(0f)] private float delayBeforeStart = 0f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Volume / Fades")]
    [SerializeField, Range(0f, 1f)] private float targetVolume = 0.8f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 2f;      // used for ambient or when crossfade==0
    [SerializeField, Min(0f)] private float crossfadeSeconds = 1.5f; // if > 0, next track crossfades in before end

    [Header("Ambient (single clip mode)")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private bool ambientLoop = true;

    [Header("Soundtrack (playlist mode)")]
    [SerializeField] private AudioClip[] playlist;
    [SerializeField] private bool loopPlaylist = true;
    [SerializeField] private bool shufflePlaylist = false;

    [Tooltip("Extra silent time AFTER a track ends BEFORE the next one begins. Only used when Crossfade Seconds = 0.")]
    [SerializeField, Min(0f)] private float intervalBetweenTracks = 0f;

    [Header("Random Advance (playlist mode)")]
    [Tooltip("If true, each advance picks a random track (optionally avoiding immediate repeats).")]
    [SerializeField] private bool randomEachAdvance = false;
    [SerializeField] private bool avoidImmediateRepeatRandom = true;

    [Header("AudioSource setup")]
    [SerializeField] private bool persistAcrossScenes = false; // DontDestroyOnLoad
    [SerializeField] private AudioMixerGroup outputMixerGroup; // optional
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
    [Tooltip("IGNORED: was part of SceneIntro gating.")]
    [SerializeField] private bool fallbackStartIfNoIntro = true; // kept for compat; no effect now
    [SerializeField] private bool verbose = false;

    // runtime
    private AudioSource a, b;       // dual sources for crossfades
    private bool usingA;            // which source is currently active
    private Coroutine startCo, advanceCo, xfCo;
    private int nowIndex = -1;
    private List<int> bag;

    public AudioClip NowPlayingClip => Current()?.clip;
    public int NowPlayingIndex => nowIndex;

    void Awake()
    {
        EnsureSources();
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // 🚫 No SceneIntro or any other gating. Just begin.
        Begin();
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    public void Begin()
    {
        if (startCo != null) StopCoroutine(startCo);
        startCo = StartCoroutine(CoBegin());
    }

    private IEnumerator CoBegin()
    {
        float t = 0f;
        while (t < delayBeforeStart)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (playlist != null && playlist.Length > 0)
        {
            if (shufflePlaylist) BuildShuffleBag();
            int first = randomEachAdvance ? PickRandomIndex() : (shufflePlaylist ? DrawFromBag() : 0);
            if (first >= 0)
                PlayIndex(first, firstFadeIn: fadeInSeconds);
        }
        else if (ambientClip)
        {
            PlayAmbient(ambientClip, fadeInSeconds);
        }

        startCo = null;
    }

    // ---------- public controls ----------
    public void PlayAmbient(AudioClip clip, float fadeSeconds)
    {
        if (!clip) return;
        StopAutoAdvance();

        var s = SwapToNextSource();
        s.volume = 0f;
        s.loop = ambientLoop;
        s.clip = clip;
        s.Play();

        if (xfCo != null) StopCoroutine(xfCo);
        xfCo = StartCoroutine(VolumeLerp(s, 0f, targetVolume, Mathf.Max(0f, fadeSeconds)));

        Other(s)?.Stop();
    }

    public void PlayIndex(int index, float firstFadeIn = -1f)
    {
        if (playlist == null || playlist.Length == 0) return;
        index = Mathf.Clamp(index, 0, playlist.Length - 1);
        nowIndex = index;

        var clip = playlist[nowIndex];
        var s = SwapToNextSource();
        s.loop = false;
        s.clip = clip;
        s.volume = 0f;
        s.Play();

        float fadeIn = (crossfadeSeconds > 0f) ? crossfadeSeconds : (firstFadeIn >= 0f ? firstFadeIn : fadeInSeconds);

        if (xfCo != null) StopCoroutine(xfCo);
        xfCo = StartCoroutine(CrossfadeOrFadeIn(Current(), Other(Current()), fadeIn, targetVolume));

        StartAutoAdvance(clip.length);
    }

    public void NextTrack()
    {
        if (playlist == null || playlist.Length == 0) return;

        int next;
        if (randomEachAdvance)
        {
            next = PickRandomIndex();
            if (next < 0) return;
        }
        else if (shufflePlaylist)
        {
            next = DrawFromBag();
        }
        else
        {
            next = (nowIndex + 1) % playlist.Length;
            if (!loopPlaylist && next <= nowIndex) return; // end without loop
        }

        PlayIndex(next, firstFadeIn: (crossfadeSeconds > 0f ? crossfadeSeconds : fadeInSeconds));
    }

    public void PlayRandomTrack(float fadeInOverride = -1f)
    {
        if (playlist == null || playlist.Length == 0) return;
        int idx = PickRandomIndex();
        if (idx >= 0)
            PlayIndex(idx, firstFadeIn: (fadeInOverride >= 0f ? fadeInOverride : (crossfadeSeconds > 0f ? crossfadeSeconds : fadeInSeconds)));
    }

    public void FadeOutAndStop(float seconds)
    {
        StopAutoAdvance();
        var cur = Current();
        if (!cur || !cur.isPlaying) return;
        if (xfCo != null) StopCoroutine(xfCo);
        xfCo = StartCoroutine(VolumeLerp(cur, cur.volume, 0f, Mathf.Max(0.0001f, seconds), stopAtEnd: true));
    }

    // ---------- internals ----------
    private void EnsureSources()
    {
        if (!a) a = CreateChildSource("Music A");
        if (!b) b = CreateChildSource("Music B");
        usingA = false;
    }

    private AudioSource CreateChildSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.rolloffMode = rolloffMode;
        s.outputAudioMixerGroup = outputMixerGroup;
        s.volume = 0f;
        return s;
    }

    private AudioSource Current() => usingA ? a : b;
    private AudioSource Other(AudioSource src) => (src == a) ? b : a;
    private AudioSource Other() => usingA ? b : a;

    private AudioSource SwapToNextSource()
    {
        usingA = !usingA;
        return Current();
    }

    private IEnumerator CrossfadeOrFadeIn(AudioSource fadeInSrc, AudioSource fadeOutSrc, float seconds, float toVolume)
    {
        seconds = Mathf.Max(0f, seconds);

        if (crossfadeSeconds > 0f)
        {
            if (fadeOutSrc && fadeOutSrc.isPlaying)
                StartCoroutine(VolumeLerp(fadeOutSrc, fadeOutSrc.volume, 0f, seconds, stopAtEnd: true));

            yield return VolumeLerp(fadeInSrc, 0f, Mathf.Clamp01(toVolume), seconds);
        }
        else
        {
            yield return VolumeLerp(fadeInSrc, 0f, Mathf.Clamp01(toVolume), seconds);
        }
    }

    private IEnumerator VolumeLerp(AudioSource src, float from, float to, float seconds, bool stopAtEnd = false)
    {
        if (!src) yield break;

        float t = 0f;
        src.volume = from;

        if (seconds <= 0f)
        {
            src.volume = to;
        }
        else
        {
            while (t < seconds)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                src.volume = Mathf.Lerp(from, to, k);
                yield return null;
            }
            src.volume = to;
        }

        if (stopAtEnd && Mathf.Approximately(to, 0f)) src.Stop();
    }

    private void StartAutoAdvance(float clipLength)
    {
        StopAutoAdvance();
        advanceCo = StartCoroutine(CoAutoAdvance(clipLength));
    }

    private void StopAutoAdvance()
    {
        if (advanceCo != null) StopCoroutine(advanceCo);
        advanceCo = null;
    }

    private IEnumerator CoAutoAdvance(float clipLength)
    {
        if (crossfadeSeconds > 0f)
        {
            float wait = Mathf.Max(0f, clipLength - crossfadeSeconds);
            yield return WaitSeconds(wait);
            NextTrack();
        }
        else
        {
            yield return WaitSeconds(clipLength);

            var cur = Current();
            if (cur && cur.isPlaying) cur.Stop();

            if (intervalBetweenTracks > 0f)
                yield return WaitSeconds(intervalBetweenTracks);

            if (playlist == null || playlist.Length == 0) { advanceCo = null; yield break; }

            int next;
            if (randomEachAdvance)
            {
                next = PickRandomIndex();
                if (next < 0) { advanceCo = null; yield break; }
            }
            else if (shufflePlaylist)
            {
                next = DrawFromBag();
            }
            else
            {
                next = (nowIndex + 1) % playlist.Length;
                if (!loopPlaylist && next <= nowIndex) { advanceCo = null; yield break; }
            }

            PlayIndex(next, firstFadeIn: fadeInSeconds);
        }
    }

    private IEnumerator WaitSeconds(float s)
    {
        float t = 0f;
        while (t < s)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    // -------- random & shuffle helpers --------
    private int PickRandomIndex()
    {
        if (playlist == null || playlist.Length == 0) return -1;
        if (playlist.Length == 1) return 0;

        if (avoidImmediateRepeatRandom && nowIndex >= 0 && playlist.Length > 1)
        {
            int r = Random.Range(0, playlist.Length - 1);
            if (r >= nowIndex) r++;
            return r;
        }
        else
        {
            return Random.Range(0, playlist.Length);
        }
    }

    private void BuildShuffleBag()
    {
        bag = new List<int>(playlist.Length);
        for (int i = 0; i < playlist.Length; i++) bag.Add(i);

        // Fisher–Yates
        for (int i = 0; i < bag.Count; i++)
        {
            int j = Random.Range(i, bag.Count);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        if (nowIndex >= 0 && bag.Count > 1 && bag[0] == nowIndex)
        {
            int swap = Random.Range(1, bag.Count);
            (bag[0], bag[swap]) = (bag[swap], bag[0]);
        }
    }

    private int DrawFromBag()
    {
        if (bag == null || bag.Count == 0) BuildShuffleBag();
        int v = bag[0];
        bag.RemoveAt(0);
        if (bag.Count == 0 && loopPlaylist) BuildShuffleBag();
        return v;
    }
}
