using FishNet.Object;
using UnityEngine;

// Per-player network behaviour. Movement is owner-authoritative: only the local owner runs
// PlayerController; remote players are driven by the replicated NetworkTransform. Owner is tinted
// cyan, everyone else orange, so co-op presence is obvious at a glance.
public class NetworkPlayer : NetworkBehaviour
{
    static readonly Color OwnerColor = new Color(0.30f, 0.85f, 1f);
    static readonly Color OtherColor = new Color(1f, 0.55f, 0.30f);

    PlayerController _pc;
    PlayerVisual _visual;

    void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _visual = GetComponent<PlayerVisual>();
        // Disabled until ownership is known (set in OnStartClient) so non-owners never self-move.
        if (_pc != null) _pc.ControlEnabled = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_pc != null) _pc.ControlEnabled = IsOwner;
        if (_visual != null) _visual.SetColor(IsOwner ? OwnerColor : OtherColor);
    }
}
