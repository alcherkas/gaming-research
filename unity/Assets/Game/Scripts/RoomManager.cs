using UnityEngine;

// Tracks which room the local player is currently in. The room-snap camera polls ActiveCenter.
// Set per local client; with networking, only the local player's RoomTrigger drives this.
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    public Vector2 ActiveCenter { get; private set; }
    public bool HasActive { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetActive(Vector2 center)
    {
        ActiveCenter = center;
        HasActive = true;
    }
}
