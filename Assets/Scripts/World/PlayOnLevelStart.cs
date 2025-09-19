// Assets/Scripts/Utility/PlayOnLevelStart.cs
using System.Collections;
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

    [Header("Extras (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private ParticleSystem particles;

    [Header("Timing")]
    [SerializeField] private float delay = 0f;
    [SerializeField] private bool onlyOnce = true;

    [Header("Events")]
    public UnityEvent onIntro;

    bool fired;

    void Reset()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        particles = GetComponent<ParticleSystem>();
    }

    void OnEnable() => SceneIntro.OnLevelIntro += HandleIntro;
    void OnDisable() => SceneIntro.OnLevelIntro -= HandleIntro;

    void Start()
    {
        // Fallback: if there is no SceneIntro in the scene, still trigger once at Start.
        if (!FindObjectOfType<SceneIntro>(true)) HandleIntro();
    }

    void HandleIntro()
    {
        if (fired && onlyOnce) return;
        StartCoroutine(CoPlay());
        fired = true;
    }

    IEnumerator CoPlay()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

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

        if (audioSource) audioSource.Play();
        if (particles) particles.Play();

        onIntro?.Invoke();
    }
}
