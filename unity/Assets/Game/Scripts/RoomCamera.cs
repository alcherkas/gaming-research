using UnityEngine;

// Classic Zelda-1 room-snap camera: slides the orthographic camera to the active room's center
// when the player crosses into a new room. Polls RoomManager (no event wiring needed).
public class RoomCamera : MonoBehaviour
{
    [SerializeField] float slide = 12f;

    void Update()
    {
        var rm = RoomManager.Instance;
        if (rm == null || !rm.HasActive) return;

        Vector2 c = rm.ActiveCenter;
        var target = new Vector3(c.x, c.y, transform.position.z);
        // Exponential smoothing -> fast, framerate-independent slide.
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-slide * Time.deltaTime));
    }
}
