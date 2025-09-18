// Assets/Scripts/Player/PlayerDeathController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerDeathController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTriggerName = "Die"; // set this to your Animator trigger

    [Header("Control Lock")]
    [Tooltip("Movement/input scripts to disable during death (in addition to freezing movement in PlayerMovement).")]
    [SerializeField] private MonoBehaviour[] disableOnDeath;

    [Header("Optional")]
    [Tooltip("Extra delay after the death animation before onDeathFinished fires.")]
    [SerializeField] private float extraDelayAfterAnim = 0f;

    [Header("Events")]
    public UnityEvent onDeathStarted;
    public UnityEvent onDeathFinished;

    public bool IsDying { get; private set; }

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    /// <summary>Called when the player drinks the Elixir. Locks control and plays death anim.</summary>
    public void DrinkElixirAndDie()
    {
        if (IsDying) return;
        IsDying = true;

        // Disable any explicitly listed scripts (movement, click interactor, etc.)
        foreach (var mb in disableOnDeath)
            if (mb) mb.enabled = false;

        onDeathStarted?.Invoke();

        if (animator && !string.IsNullOrEmpty(deathTriggerName))
        {
            animator.ResetTrigger(deathTriggerName);
            animator.SetTrigger(deathTriggerName);
            // Wait for the animation to roughly complete
            StartCoroutine(WaitForDeathAnim());
        }
        else
        {
            // No animator? still finish after a short delay to proceed.
            StartCoroutine(FinishAfterDelay(0.5f + extraDelayAfterAnim));
        }
    }

    /// <summary>Call this from an Animation Event at the end of the death clip if you have one.</summary>
    public void FinishDeath()
    {
        if (!IsDying) return;
        StartCoroutine(FinishAfterDelay(extraDelayAfterAnim));
        IsDying = false;
    }

    private IEnumerator WaitForDeathAnim()
    {
        // Let Animator update once
        yield return null;

        float approxLen = 0.8f; // fallback
        if (animator)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            approxLen = Mathf.Max(0.5f, st.length);
        }

        yield return new WaitForSeconds(approxLen);
        FinishDeath();
    }

    private IEnumerator FinishAfterDelay(float d)
    {
        if (d > 0f) yield return new WaitForSeconds(d);
        onDeathFinished?.Invoke();
    }
}
