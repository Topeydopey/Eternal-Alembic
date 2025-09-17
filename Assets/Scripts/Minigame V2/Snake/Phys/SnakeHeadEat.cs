// SnakeHeadEat.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SnakeHeadEat : MonoBehaviour
{
    public SnakePhysicsController controller;

    void Awake()
    {
        if (!controller) controller = GetComponent<SnakePhysicsController>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!controller) return;
        var seed = other.GetComponent<SeedWorld>();
        if (seed) controller.NotifyAteSeed(seed);
    }
}
