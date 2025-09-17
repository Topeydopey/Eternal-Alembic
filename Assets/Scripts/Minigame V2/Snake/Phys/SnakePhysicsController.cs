using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SnakePhysicsController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float maxSpeed = 5f;
    [Min(0f)] public float maxForce = 30f;
    [Min(0f)] public float arriveRadius = 0.15f;
    [Min(0f)] public float slowRadius = 1.2f;
    [Min(0f)] public float faceTurnSpeed = 720f;
    [Tooltip("Rotate to face velocity while moving.")]
    public bool faceVelocity = true;

    [Header("Game Logic")]
    [Min(1)] public int seedsToWin = 5;
    [Tooltip("Freeze movement automatically when win condition is reached.")]
    public bool autoFreezeOnComplete = true;
    [Tooltip("Stop accepting & clear any queued targets when complete.")]
    public bool clearTargetsOnComplete = true;

    // Events
    public event Action<int> OnSeedsEaten;
    public event Action OnCompleted;

    // Runtime
    private readonly Queue<Vector2> targets = new();
    private Rigidbody2D rb;
    private Vector2? currentTarget;
    private float zKeep;

    private int seedsEaten = 0;
    private bool allowTargets = true;
    private bool frozen = false;
    private bool completedFired = false;

    // Public state
    public int SeedsEaten => seedsEaten;
    public bool IsFrozen => frozen;
    public bool IsComplete => seedsEaten >= Mathf.Max(1, seedsToWin);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
#if UNITY_6_0_OR_NEWER
        rb.linearDamping = 4f;
        rb.angularDamping = 5f;
#else
        rb.linearDamping = 4f;
        rb.angularDamping = 5f;
#endif
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        zKeep = transform.position.z;
        completedFired = false;
    }

    void FixedUpdate()
    {
        // Keep z flat
        var p2 = transform.position; p2.z = zKeep; transform.position = p2;

        if (frozen)
        {
#if UNITY_6_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.linearVelocity = Vector2.zero;
#endif
            return;
        }

        // Pull next target if needed
        if (currentTarget == null && targets.Count > 0)
            currentTarget = targets.Dequeue();

        if (currentTarget == null)
        {
            // idle damping
#if UNITY_6_0_OR_NEWER
            rb.linearVelocity *= 0.98f;
#else
            rb.linearVelocity *= 0.98f;
#endif
            return;
        }

        Vector2 pos = rb.position;
        Vector2 tgt = currentTarget.Value;
        Vector2 to = tgt - pos;
        float dist = to.magnitude;

        if (dist <= arriveRadius)
        {
            currentTarget = null;
#if UNITY_6_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.linearVelocity = Vector2.zero;
#endif
            return;
        }

        // Steering towards target with smooth slow-down
        Vector2 dir = (dist > 0.0001f) ? (to / dist) : Vector2.zero;
        float desiredSpeed = (dist < slowRadius)
            ? Mathf.Lerp(0.1f, maxSpeed, dist / Mathf.Max(0.0001f, slowRadius))
            : maxSpeed;

        Vector2 desiredVel = dir * desiredSpeed;
#if UNITY_6_0_OR_NEWER
        Vector2 curVel = rb.linearVelocity;
#else
        Vector2 curVel = rb.linearVelocity;
#endif
        Vector2 steer = Vector2.ClampMagnitude(desiredVel - curVel, maxForce);

        rb.AddForce(steer, ForceMode2D.Force);

        // Cap max speed
#if UNITY_6_0_OR_NEWER
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        Vector2 finalVel = rb.linearVelocity;
#else
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpeed);
        Vector2 finalVel = rb.linearVelocity;
#endif

        // Face velocity
        if (faceVelocity && finalVel.sqrMagnitude > 0.0001f)
        {
            float ang = Mathf.Atan2(finalVel.y, finalVel.x) * Mathf.Rad2Deg;
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, ang, faceTurnSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0, 0, z);
        }
    }

    /// <summary>Queue a world-space target for the head to travel toward.</summary>
    public void EnqueueTarget(Vector2 worldPos)
    {
        if (!allowTargets || frozen || IsComplete) return;
        targets.Enqueue(worldPos);
    }

    /// <summary>Called by SnakeHeadEat when the head enters a SeedWorld trigger.</summary>
    public void NotifyAteSeed(SeedWorld seed)
    {
        if (seed) Destroy(seed.gameObject);
        seedsEaten = Mathf.Max(0, seedsEaten + 1);
        OnSeedsEaten?.Invoke(seedsEaten);

        if (IsComplete && !completedFired)
        {
            completedFired = true;
            allowTargets = false;

            if (clearTargetsOnComplete)
            {
                currentTarget = null;
                targets.Clear();
            }

            if (autoFreezeOnComplete) SetFrozen(true);

            OnCompleted?.Invoke();
        }
    }

    /// <summary>Freeze/unfreeze snake motion.</summary>
    public void SetFrozen(bool on)
    {
        frozen = on;
#if UNITY_6_0_OR_NEWER
        if (on) rb.linearVelocity = Vector2.zero;
#else
        if (on) rb.linearVelocity = Vector2.zero;
#endif
    }

    /// <summary>Reset per-session state and (optionally) override seedsToWin.</summary>
    public void ResetSession(int newSeedsToWin = -1)
    {
        if (newSeedsToWin > 0) seedsToWin = newSeedsToWin;

        seedsEaten = 0;
        allowTargets = true;
        frozen = false;
        completedFired = false;

        currentTarget = null;
        targets.Clear();

#if UNITY_6_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.linearVelocity = Vector2.zero;
#endif
        // keep z flat & rotation reasonable on restart (optional)
        var p = transform.position; p.z = zKeep; transform.position = p;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        seedsToWin = Mathf.Max(1, seedsToWin);
        slowRadius = Mathf.Max(arriveRadius, slowRadius);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, slowRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, arriveRadius);
    }
#endif
}
