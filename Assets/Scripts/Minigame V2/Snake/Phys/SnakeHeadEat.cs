// SnakeHeadEat.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SnakeHeadEat : MonoBehaviour
{
    public SnakePhysicsController controller;

    // NEW: let the head notify the minigame so it can play the eat SFX
    [SerializeField] private SnakeWorldMinigame minigame;

    void Awake()
    {
        if (!controller) controller = GetComponent<SnakePhysicsController>();
        if (!minigame)
            minigame = FindAnyObjectByType<SnakeWorldMinigame>(FindObjectsInactive.Include);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!controller) return;

        var seed = other.GetComponent<SeedWorld>();
        if (seed)
        {
            // Existing logic
            controller.NotifyAteSeed(seed);

            // NEW: ping the minigame so it plays the eat clip
            if (minigame && minigame.isActiveAndEnabled)
                minigame.NotifySnakeAte();
        }
    }
}
