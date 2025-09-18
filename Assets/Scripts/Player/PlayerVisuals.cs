using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerVisuals : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void UpdateVisuals(Vector2 move)
    {
        float speed = move.magnitude;

        // Set animator parameters
        animator.SetFloat("MoveX", move.x);
        animator.SetFloat("MoveY", move.y);
        animator.SetFloat("Speed", speed);

        // Flip sprite for left/right
        if (Mathf.Abs(move.x) > 0.01f)
            sr.flipX = move.x < 0f;
    }

    public void PlayDeath()
    {
        animator.SetTrigger("Die");
    }
}
