using FishNet.Object;
using UnityEngine;

/// <summary>
/// Networked pickup that grants a specific key type to players.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class KeyPickup : NetworkBehaviour
{
    [SerializeField] private KeyType _keyType = KeyType.Blue;

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
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = SpriteFactory.Square();
            sr.color = GetKeyColor(_keyType);
            sr.sortingOrder = 10;
        }
        transform.localScale = new Vector3(0.35f, 0.35f, 1f);

        var col = GetComponent<CircleCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = 0.6f;
        }
    }

    private Color GetKeyColor(KeyType type)
    {
        return type switch
        {
            KeyType.Blue => new Color(0.2f, 0.5f, 1f),
            KeyType.Red => new Color(1f, 0.2f, 0.2f),
            KeyType.Yellow => new Color(1f, 0.9f, 0.2f),
            _ => Color.white
        };
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[KeyPickup] OnTriggerEnter2D called with other={other.gameObject.name}, tag={other.gameObject.tag}, layer={other.gameObject.layer}. IsServer={IsServer}, IsServerInitialized={IsServerInitialized}");
        if (!IsServer) return;

        var inventory = other.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.AddKey(_keyType);
            RpcPlayPickupSFX(transform.position);
            ServerManager.Despawn(gameObject);
        }
        else
        {
            Debug.Log($"[KeyPickup] Inventory component NOT found on {other.gameObject.name}!");
        }
    }

    [ObserversRpc]
    private void RpcPlayPickupSFX(Vector3 pos)
    {
        SFXManager.PlayPickup(pos);
    }
}
