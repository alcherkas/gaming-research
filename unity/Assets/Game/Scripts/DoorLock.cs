using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// Networked door blocking progress until opened with a matching key.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class DoorLock : NetworkBehaviour
{
    [SerializeField] private KeyType _keyType = KeyType.Blue;
    private readonly SyncVar<bool> _isLocked = new(true);

    public KeyType KeyType
    {
        get => _keyType;
        set
        {
            _keyType = value;
            UpdateVisuals();
        }
    }

    private void Awake()
    {
        _isLocked.OnChange += OnLockedStateChanged;
    }

    private void OnDestroy()
    {
        _isLocked.OnChange -= OnLockedStateChanged;
    }

    private void Start()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = SpriteFactory.Square();
            sr.color = _isLocked.Value ? GetDoorColor(_keyType) : new Color(0f, 0f, 0f, 0f);
            sr.sortingOrder = 5;
        }

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.enabled = _isLocked.Value;
        }
    }

    private Color GetDoorColor(KeyType type)
    {
        return type switch
        {
            KeyType.Blue => new Color(0.2f, 0.5f, 1f, 0.9f),
            KeyType.Red => new Color(1f, 0.2f, 0.2f, 0.9f),
            KeyType.Yellow => new Color(1f, 0.9f, 0.2f, 0.9f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.9f)
        };
    }

    private void OnLockedStateChanged(bool prev, bool next, bool asServer)
    {
        UpdateVisuals();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsServer) return;
        if (!_isLocked.Value) return;

        var inventory = collision.gameObject.GetComponent<Inventory>();
        if (inventory != null && inventory.HasKey(_keyType))
        {
            _isLocked.Value = false;
            RpcPlayUnlockSFX(transform.position);
            Debug.Log($"[DoorLock] Unlocked door of type {_keyType}!");
        }
    }

    [ObserversRpc]
    private void RpcPlayUnlockSFX(Vector3 pos)
    {
        SFXManager.PlayPickup(pos); // Confirms unlock using pickup sound arpeggio
    }
}
