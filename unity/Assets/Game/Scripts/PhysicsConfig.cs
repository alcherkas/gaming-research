using UnityEngine;

// Top-down 2D physics tuning applied at startup.
// - No global gravity (top-down).
// - 50 Hz fixed step: plenty for token movement and cheaper than the 60 Hz default on a 15 W APU.
// Walls are static colliders (no Rigidbody2D); only players have bodies, so the 2D solver stays cheap.
public class PhysicsConfig : MonoBehaviour
{
    void Awake()
    {
        Physics2D.gravity = Vector2.zero;
        Time.fixedDeltaTime = 0.02f;
    }
}
