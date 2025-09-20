// Assets/Scripts/Player/PlayerDeathController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerDeathController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("OPTION A: If you use a single trigger and an Int parameter to choose a variant, set them here.")]
    [SerializeField] private string deathTriggerName = "Die";    // common trigger when using the int param route
    [SerializeField] private string deathVariantIntParam = "";   // e.g., "DeathIndex" (leave empty to ignore)
    [SerializeField, Min(0)] private int deathVariantCount = 0;  // number of variants if using the int param

    [Tooltip("OPTION B: If you prefer separate triggers per variant, list them here. If this has entries, it overrides the int-param route.")]
    [SerializeField] private string[] deathTriggerOptions;

    [Header("State Waiting (optional)")]
    [Tooltip("If set, we wait until layer-0 is in a state tagged with this before checking completion.")]
    [SerializeField] private string deathStateTag = "Death";
    [Tooltip("Timeout if we never enter a death state (seconds).")]
    [SerializeField, Min(0.1f)] private float waitTimeoutSeconds = 3f;
    [Tooltip("Extra small delay after state finishes before raising onDeathFinished.")]
    [SerializeField, Min(0f)] private float stateEndExtraWait = 0.1f;

    [Header("Control Lock")]
    [Tooltip("Movement/input scripts to disable during death (in addition to locking via your movement).")]
    [SerializeField] private MonoBehaviour[] disableOnDeath;

    [Header("Animator Idle Reset (for respawn/menu)")]
    [Tooltip("If set, this trigger will be fired by ForceIdleNow() to return to idle.")]
    [SerializeField] private string idleTriggerName = "";
    [Tooltip("If no idle trigger is provided, ForceIdleNow() will Play() this state by name.")]
    [SerializeField] private string idleStateName = "Idle";
    [Tooltip("Reset all triggers when forcing idle (helps avoid stuck transitions).")]
    [SerializeField] private bool resetAllTriggersOnForceIdle = true;

    [Header("Events")]
    public UnityEvent onDeathStarted;
    public UnityEvent onDeathFinished;

    // ------------------- AUDIO (NEW) -------------------
    [Serializable]
    private struct DeathSfxRule
    {
        [Tooltip("Exact Animator state short name to match (state name in the Animator). Optional.")]
        public string stateShortName;

        [Tooltip("Substring to find in the playing AnimationClip.name (case-insensitive). Optional.")]
        public string clipNameContains;

        [Tooltip("Sound to play when this rule matches.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume;
    }

    [Header("Death Audio (optional)")]
    [Tooltip("AudioSource to play the SFX on. If empty, a temporary 2D one-shot is spawned so it won't be cut off.")]
    [SerializeField] private AudioSource deathAudio;

    [Tooltip("Rules to pick a SFX based on state or clip name. First matching rule wins.")]
    [SerializeField] private DeathSfxRule[] deathSfxRules;

    [Tooltip("Fallback pool if no rule matches. Picks a random clip (no immediate repeat).")]
    [SerializeField] private AudioClip[] defaultDeathClips;

    [Range(0f, 1f)]
    [SerializeField] private float defaultDeathVolume = 1f;

    [Tooltip("Avoid repeating the same fallback clip twice in a row.")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    private bool deathSfxPlayed;
    private int lastDefaultIdx = -1;

    // ---------------------------------------------------

    public bool IsDying { get; private set; }

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    /// <summary>Called when the player drinks the Elixir. Locks control and plays a RANDOM death variant.</summary>
    public void DrinkElixirAndDie()
    {
        if (IsDying) return;
        IsDying = true;
        deathSfxPlayed = false;

        // disable listed scripts immediately (movement, click interactor, etc.)
        if (disableOnDeath != null)
        {
            foreach (var mb in disableOnDeath)
                if (mb) mb.enabled = false;
        }

        onDeathStarted?.Invoke();

        if (!animator)
        {
            // No animator? still finish after a short delay so game can progress.
            StartCoroutine(FinishAfterDelay(0.6f + stateEndExtraWait));
            // Play any fallback death sfx immediately if we want some audio
            TryPlayDeathSfxForCurrentState(forceFallbackIfNoMatch: true);
            return;
        }

        // ---- Choose and fire a variant ----
        if (deathTriggerOptions != null && deathTriggerOptions.Length > 0)
        {
            // OPTION B: different triggers per variant
            int i = UnityEngine.Random.Range(0, deathTriggerOptions.Length);
            var trig = deathTriggerOptions[i];

            if (!string.IsNullOrEmpty(trig))
            {
                animator.ResetTrigger(trig);
                animator.SetTrigger(trig);
            }
            else if (!string.IsNullOrEmpty(deathTriggerName))
            {
                animator.ResetTrigger(deathTriggerName);
                animator.SetTrigger(deathTriggerName);
            }
        }
        else if (!string.IsNullOrEmpty(deathVariantIntParam) && deathVariantCount > 0)
        {
            // OPTION A: one trigger + int variant parameter
            int idx = UnityEngine.Random.Range(0, deathVariantCount);
            animator.SetInteger(deathVariantIntParam, idx);

            if (!string.IsNullOrEmpty(deathTriggerName))
            {
                animator.ResetTrigger(deathTriggerName);
                animator.SetTrigger(deathTriggerName);
            }
        }
        else
        {
            // Fallback: just fire the single trigger (if any)
            if (!string.IsNullOrEmpty(deathTriggerName))
            {
                animator.ResetTrigger(deathTriggerName);
                animator.SetTrigger(deathTriggerName);
            }
        }

        // Wait for the animation/state to finish, then call FinishDeath()
        StartCoroutine(WaitForDeathAnim());
    }

    /// <summary>Call this with an Animation Event at the end of each death clip (optional).</summary>
    public void FinishDeath()
    {
        if (!IsDying) return;
        StartCoroutine(FinishAfterDelay(stateEndExtraWait));
        IsDying = false;
    }

    private IEnumerator WaitForDeathAnim()
    {
        // Let Animator update at least once
        yield return null;

        float t = 0f;
        bool sawTaggedDeath = false;

        while (t < waitTimeoutSeconds)
        {
            if (!animator) break;

            var st = animator.GetCurrentAnimatorStateInfo(0);

            // 🔊 NEW: try to play the rule-based SFX as soon as we see any state,
            // even if it's not tagged (only plays once because of deathSfxPlayed).
            if (!deathSfxPlayed)
                TryPlayDeathSfxForCurrentState(forceFallbackIfNoMatch: false);

            // Use the tag *only* to know when the death state is done
            bool inDeathState = string.IsNullOrEmpty(deathStateTag) || st.IsTag(deathStateTag);
            if (inDeathState)
            {
                sawTaggedDeath = true;
                if (st.normalizedTime >= 1f && !animator.IsInTransition(0))
                    break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // If we never matched a rule during the animation, fire a fallback once.
        if (!deathSfxPlayed)
            TryPlayDeathSfxForCurrentState(forceFallbackIfNoMatch: true);

        // If we never saw the tagged state at all, give a tiny grace wait (old behavior)
        if (!sawTaggedDeath)
            yield return new WaitForSeconds(0.1f);

        FinishDeath();
    }

    private IEnumerator FinishAfterDelay(float d)
    {
        if (d > 0f) yield return new WaitForSeconds(d);
        onDeathFinished?.Invoke();
    }

    /// <summary>Immediately force the controller back to Idle (used by respawn/menu flows).</summary>
    public void ForceIdleNow()
    {
        IsDying = false;

        if (!animator) return;

        if (resetAllTriggersOnForceIdle)
        {
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger)
                    animator.ResetTrigger(p.name);
        }

        if (!string.IsNullOrEmpty(deathTriggerName))
            animator.ResetTrigger(deathTriggerName);

        if (!string.IsNullOrEmpty(idleTriggerName))
        {
            animator.SetTrigger(idleTriggerName);
        }
        else if (!string.IsNullOrEmpty(idleStateName))
        {
            animator.Play(idleStateName, 0, 0f);
        }
    }

    // ------------------- AUDIO HELPERS -------------------

    private void TryPlayDeathSfxForCurrentState(bool forceFallbackIfNoMatch)
    {
        if (deathSfxPlayed) return;

        AudioClip clip = null;
        float vol = 1f;

        if (animator)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            string currentClipName = null;

            // Try to get the currently playing clip name on this state
            var infos = animator.GetCurrentAnimatorClipInfo(0);
            if (infos != null && infos.Length > 0 && infos[0].clip)
                currentClipName = infos[0].clip.name;

            // Find first matching rule
            if (deathSfxRules != null)
            {
                for (int i = 0; i < deathSfxRules.Length; i++)
                {
                    var r = deathSfxRules[i];
                    bool match = false;

                    if (!string.IsNullOrEmpty(r.stateShortName))
                    {
                        // Exact match on state name (shortNameHash)
                        if (Animator.StringToHash(r.stateShortName) == st.shortNameHash)
                            match = true;
                    }

                    if (!match && !string.IsNullOrEmpty(r.clipNameContains) && !string.IsNullOrEmpty(currentClipName))
                    {
                        if (currentClipName.IndexOf(r.clipNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                            match = true;
                    }

                    if (match && r.clip)
                    {
                        clip = r.clip;
                        vol = Mathf.Clamp01(r.volume <= 0f ? 1f : r.volume);
                        break;
                    }
                }
            }
        }

        // Fallback random
        if (!clip && forceFallbackIfNoMatch)
        {
            var pool = defaultDeathClips;
            if (pool != null && pool.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, pool.Length);
                if (avoidImmediateRepeat && pool.Length > 1 && idx == lastDefaultIdx)
                    idx = (idx + 1) % pool.Length;
                lastDefaultIdx = idx;

                clip = pool[idx];
                vol = defaultDeathVolume;
            }
        }

        if (clip)
        {
            PlayOneShotSafe(clip, vol);
            deathSfxPlayed = true;
        }
    }

    private void PlayOneShotSafe(AudioClip clip, float volume)
    {
        if (!clip) return;

        if (deathAudio)
        {
            deathAudio.PlayOneShot(clip, Mathf.Clamp01(volume));
            return;
        }

        // Detached temp 2D one-shot so it won’t be cut off if objects disable during fade/respawn
        var go = new GameObject("DeathOneShot2D");
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 0f;
        a.volume = Mathf.Clamp01(volume);
        a.clip = clip;
        a.Play();
        Destroy(go, Mathf.Max(0.02f, clip.length));
    }
}
