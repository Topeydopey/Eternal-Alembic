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

    private Vector2 _moveInput;   // raw input
    private Vector2 _moveDir;     // normalized direction

    private void Reset()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        deathController = GetComponent<PlayerDeathController>();
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
        // If we're in the death sequence, stop taking input but keep animator updated (Speed=0)
        if (deathController && deathController.IsDying)
        {
            UpdateAnimator(Vector2.zero);
            return;
        }

        // Read input every frame (render rate)
        _moveInput = move != null ? move.action.ReadValue<Vector2>() : Vector2.zero;

        // Normalize so diagonals aren't faster
        _moveDir = _moveInput.sqrMagnitude > 1e-4f ? _moveInput.normalized : Vector2.zero;

        // Visuals (anim & flip)
        UpdateAnimator(_moveDir);
        UpdateFlip(_moveDir);
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
}
