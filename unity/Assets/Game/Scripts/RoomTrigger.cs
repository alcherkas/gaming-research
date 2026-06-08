using UnityEngine;

// Interior trigger volume for a room. When the local player enters, it becomes the active room
// (drives the room-snap camera). In Phase P4 this will be gated to the owning/local player only.
[RequireComponent(typeof(BoxCollider2D))]
public class RoomTrigger : MonoBehaviour
{
    public Vector2 Center;

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[RoomTrigger] {gameObject.name} collided with {other.gameObject.name}");
        if (RoomManager.Instance == null)
        {
            Debug.Log("[RoomTrigger] RoomManager.Instance is null!");
            return;
        }
        
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;
        
        // With networking, only the player controller that has local control enabled drives the room-snap camera.
        var no = pc.GetComponent<FishNet.Object.NetworkObject>();
        if (no != null)
        {
            if (!no.IsOwner && !(no.Owner != null && no.Owner.IsLocalClient))
            {
                Debug.Log($"[RoomTrigger] PlayerController found, but is not owned by local client.");
                return;
            }
        }
        else if (!pc.ControlEnabled)
        {
            Debug.Log($"[RoomTrigger] PlayerController found, but ControlEnabled is false (not local player).");
            return;
        }

        Debug.Log($"[RoomTrigger] Setting active room to {Center}");
        RoomManager.Instance.SetActive(Center);
    }
}
