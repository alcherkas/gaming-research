using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public enum KeyType
{
    None = 0,
    Blue = 1,
    Red = 2,
    Yellow = 3
}

/// <summary>
/// Simple networked inventory attached to each player, tracking collected keys.
/// </summary>
public class Inventory : NetworkBehaviour
{
    private readonly SyncVar<int> _keysMask = new(0);

    public bool HasKey(KeyType key)
    {
        if (key == KeyType.None) return true;
        return (_keysMask.Value & (1 << (int)key)) != 0;
    }

    [Server]
    public void AddKey(KeyType key)
    {
        if (key == KeyType.None) return;
        _keysMask.Value |= (1 << (int)key);
        Debug.Log($"[Inventory] Key added: {key}. Mask value: {_keysMask.Value}");
    }
}
