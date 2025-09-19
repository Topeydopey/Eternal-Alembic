// Assets/Scripts/Player/PlayerDeathController.cs
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
    [SerializeField] private string deathVariantIntParam = "";    // e.g., "DeathIndex" (leave empty to ignore)
    [SerializeField, Min(0)] private int deathVariantCount = 0;   // number of variants if using the int param

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

    [Header("Events")]
    public UnityEvent onDeathStarted;
    public UnityEvent onDeathFinished;

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
            return;
        }

        // ---- Choose a random variant ----
        if (deathTriggerOptions != null && deathTriggerOptions.Length > 0)
        {
            // OPTION B: different triggers per variant
            int i = Random.Range(0, deathTriggerOptions.Length);
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
            int idx = Random.Range(0, deathVariantCount);
            animator.SetInteger(deathVariantIntParam, idx);

            if (!string.IsNullOrEmpty(deathTriggerName))
            {
                animator.ResetTrigger(deathTriggerName);
                animator.SetTrigger(deathTriggerName);
            }
        }
        else
        {
            // Fallback: just fire the single trigger
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
        bool started = false;

        // Try to wait until we're in a state tagged "Death" (if provided) and finished (normalizedTime >= 1)
        while (t < waitTimeoutSeconds)
        {
            if (!animator) break;

            var st = animator.GetCurrentAnimatorStateInfo(0);
            bool inDeathState = string.IsNullOrEmpty(deathStateTag) || st.IsTag(deathStateTag);

            if (inDeathState)
            {
                started = true;
                if (st.normalizedTime >= 1f && !animator.IsInTransition(0))
                    break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (!started)
        {
            // crude fallback if we never saw the state
            yield return new WaitForSeconds(0.8f);
        }

        FinishDeath();
    }

    private IEnumerator FinishAfterDelay(float d)
    {
        if (d > 0f) yield return new WaitForSeconds(d);
        onDeathFinished?.Invoke();
    }
}
