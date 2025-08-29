using UnityEngine;

public class InteractPrompt : MonoBehaviour
{
    public GameObject promptVisual;   // e.g., a small "E" bubble
    public float showRadius = 1.25f;
    public LayerMask playerMask;

    private void Update()
    {
        bool near = Physics2D.OverlapCircle(transform.position, showRadius, playerMask);
        if (promptVisual && promptVisual.activeSelf != near) promptVisual.SetActive(near);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, showRadius);
    }
}
