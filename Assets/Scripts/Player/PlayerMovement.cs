using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("World units per second.")]
    public float MoveSpeed = 3.5f;
    public Rigidbody2D body;

    [Header("Input (New Input System)")]
    [Tooltip("Bind to your 'Move' Vector2 action.")]
    public InputActionReference move;

    [Header("Visuals")]
    [SerializeField] private Animator animator;          // drives Idle/Walk + Die trigger later
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Dependencies")]
    [SerializeField] private PlayerDeathController deathController; // freezes movement while dying

    // ------------------ FOOTSTEPS ------------------
    [Header("Footsteps")]
    [Tooltip("AudioSource used for steps. If empty, one will be added here.")]
    [SerializeField] private AudioSource footstepSource;

    [Tooltip("Footstep variants to pick from at runtime.")]
    [SerializeField] private AudioClip[] footstepClips;

    [Tooltip("Base volume for each step (before jitter).")]
    [Range(0f, 1f)][SerializeField] private float stepBaseVolume = 0.85f;

    [Tooltip("Random pitch range per step.")]
    [SerializeField] private Vector2 stepPitchRange = new Vector2(0.96f, 1.04f);

    [Tooltip("Step interval (seconds) when moving at full stick input (Speed=1). Lower = faster cadence.")]
    [SerializeField, Min(0.05f)] private float stepIntervalAtFullSpeed = 0.42f;

    [Tooltip("Don't start footsteps unless movement input magnitude exceeds this.")]
    [SerializeField, Range(0f, 1f)] private float minSpeedToStep = 0.05f;

    [Tooltip("Delay before the first step when you start moving, as a fraction of the computed interval (0..1).")]
    [SerializeField, Range(0f, 1f)] private float firstStepDelayFraction = 0.5f;

    [Tooltip("Use PlayOneShot so steps don't replace each other if they overlap slightly.")]
    [SerializeField] private bool stepUsePlayOneShot = true;

    [Tooltip("Avoid playing the exact same clip twice in a row.")]
    [SerializeField] private bool stepAvoidImmediateRepeat = true;

    // ------------------------------------------------

    private Vector2 _moveInput;   // raw input
    private Vector2 _moveDir;     // normalized direction

    // Footstep runtime
    private float _stepTimer;
    private bool _wasMoving;
    private int _lastStepIndex = -1;

    private void Reset()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        deathController = GetComponent<PlayerDeathController>();
    }

    private void Awake()
    {
        // Ensure we have an AudioSource for footsteps
        if (!footstepSource)
        {
            footstepSource = GetComponent<AudioSource>();
            if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();
        }
        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
        footstepSource.spatialBlend = 0f; // 2D by default; set >0 for 3D if you want
    }

    private void OnEnable()
    {
        move?.action?.Enable();
        SceneManager.activeSceneChanged += OnSceneChangedEnableInput;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChangedEnableInput;
        move?.action?.Disable();
    }

    private void OnSceneChangedEnableInput(Scene _, Scene __)
    {
        move?.action?.Enable();
    }

    private void Update()
    {
        // If we're in the death sequence, stop taking input and prevent steps
        if (deathController && deathController.IsDying)
        {
            UpdateAnimator(Vector2.zero);
            UpdateFootsteps(0f);
            return;
        }

        // Read input every frame (render rate)
        _moveInput = move != null ? move.action.ReadValue<Vector2>() : Vector2.zero;

        // Normalize so diagonals aren't faster
        _moveDir = _moveInput.sqrMagnitude > 1e-4f ? _moveInput.normalized : Vector2.zero;

        // Visuals (anim & flip)
        UpdateAnimator(_moveDir);
        UpdateFlip(_moveDir);

        // Footsteps (speed is 0..1 from input magnitude)
        UpdateFootsteps(_moveDir.magnitude);
    }

    private void FixedUpdate()
    {
        if (!body) return;

        if (deathController && deathController.IsDying)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        // Physics step movement
        body.linearVelocity = _moveDir * MoveSpeed;
    }

    private void UpdateAnimator(Vector2 dir)
    {
        if (!animator) return;

        float speed = dir.magnitude; // 0..1
        animator.SetFloat("MoveX", dir.x);
        animator.SetFloat("MoveY", dir.y);
        animator.SetFloat("Speed", speed);
    }

    private void UpdateFlip(Vector2 dir)
    {
        if (!spriteRenderer) return;

        // Only flip when there is meaningful horizontal input
        if (Mathf.Abs(dir.x) > 0.01f)
            spriteRenderer.flipX = dir.x < 0f;
    }

    // ------------------ Footstep logic ------------------

    private void UpdateFootsteps(float speed01)
    {
        // Not moving enough to step?
        if (speed01 < minSpeedToStep || footstepClips == null || footstepClips.Length == 0)
        {
            _wasMoving = false;
            _stepTimer = 0f; // reset so we get a first-step delay next time
            return;
        }

        // Just started moving? Prime timer for a "first step" delay.
        if (!_wasMoving)
        {
            float interval = ComputeStepInterval(speed01);
            _stepTimer = interval * Mathf.Clamp01(firstStepDelayFraction);
            _wasMoving = true;
        }

        // Countdown and fire steps
        _stepTimer -= Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            PlayFootstep();
            // Schedule next step; scale by current input magnitude so slower movement = slower cadence
            float nextInterval = ComputeStepInterval(speed01);
            _stepTimer += nextInterval;
        }
    }

    private float ComputeStepInterval(float speed01)
    {
        // Avoid div-by-zero; if speed is tiny treat as minSpeedToStep
        float s = Mathf.Max(minSpeedToStep, speed01);
        // Faster input = shorter interval
        return stepIntervalAtFullSpeed / s;
    }

    private void PlayFootstep()
    {
        if (!footstepSource || footstepClips == null || footstepClips.Length == 0) return;

        // Pick a random clip (avoid immediate repeat if requested)
        int idx;
        if (stepAvoidImmediateRepeat && footstepClips.Length > 1)
        {
            do { idx = Random.Range(0, footstepClips.Length); }
            while (idx == _lastStepIndex);
        }
        else
        {
            idx = Random.Range(0, footstepClips.Length);
        }
        _lastStepIndex = idx;

        var clip = footstepClips[idx];
        if (!clip) return;

        float pitch = Random.Range(stepPitchRange.x, stepPitchRange.y);
        float vol = Mathf.Clamp01(stepBaseVolume);

        if (stepUsePlayOneShot)
        {
            // Temporarily adjust pitch for the one-shot
            float originalPitch = footstepSource.pitch;
            footstepSource.pitch = pitch;
            footstepSource.PlayOneShot(clip, vol);
            footstepSource.pitch = originalPitch;
        }
        else
        {
            // Replace the clip on the source
            footstepSource.clip = clip;
            footstepSource.pitch = pitch;
            footstepSource.volume = vol;
            footstepSource.Play();
        }
    }
}
