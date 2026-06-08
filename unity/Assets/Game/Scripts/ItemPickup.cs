using FishNet.Object;
using UnityEngine;

public enum ItemType
{
    HealthPotion,
    ManaPotion,
    SpeedBoost
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class ItemPickup : NetworkBehaviour
{
    [SerializeField] private ItemType _itemType = ItemType.HealthPotion;
    private Vector3 _basePosition;
    private bool _hasBasePosition = false;

    public ItemType ItemType
    {
        get => _itemType;
        set
        {
            _itemType = value;
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
            sr.sprite = SpriteFactory.Disc();
            sr.color = GetItemColor(_itemType);
            sr.sortingOrder = 8;
        }
        transform.localScale = new Vector3(0.35f, 0.35f, 1f);

        var col = GetComponent<CircleCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            col.radius = 0.5f;
        }
    }

    private Color GetItemColor(ItemType type)
    {
        return type switch
        {
            ItemType.HealthPotion => new Color(1f, 0.2f, 0.4f), // Pinkish red
            ItemType.ManaPotion => new Color(0.2f, 0.6f, 1f), // Mana blue
            ItemType.SpeedBoost => new Color(0.9f, 0.8f, 0.1f), // Yellow/gold speed boost
            _ => Color.white
        };
    }

    private void Update()
    {
        if (!_hasBasePosition)
        {
            _basePosition = transform.position;
            _hasBasePosition = true;
        }
        // Floating/bobbing animation (sine wave Y offset)
        float bounce = Mathf.Sin(Time.time * 4f) * 0.12f;
        transform.position = _basePosition + new Vector3(0f, bounce, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[ItemPickup] OnTriggerEnter2D called with other={other.gameObject.name}, tag={other.gameObject.tag}, layer={other.gameObject.layer}. IsServer={IsServer}, IsServerInitialized={IsServerInitialized}");
        if (!IsServer) return;

        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            bool applied = ApplyEffect(other.gameObject);
            Debug.Log($"[ItemPickup] ApplyEffect result: {applied} for itemType={_itemType} on player={other.gameObject.name}");
            if (applied)
            {
                RpcPlayPickupEffects(transform.position);
                ServerManager.Despawn(gameObject);
            }
        }
        else
        {
            Debug.Log($"[ItemPickup] PlayerController NOT found on {other.gameObject.name}");
        }
    }

    private bool ApplyEffect(GameObject player)
    {
        switch (_itemType)
        {
            case ItemType.HealthPotion:
                var health = player.GetComponent<Health>();
                if (health != null && health.CurrentHealth < health.MaxHealth)
                {
                    health.Heal(40f);
                    return true;
                }
                break;

            case ItemType.ManaPotion:
                var spellSys = player.GetComponent<SpellSystem>();
                if (spellSys != null && spellSys.CurrentMana < spellSys.MaxMana)
                {
                    spellSys.RestoreMana(50f);
                    return true;
                }
                break;

            case ItemType.SpeedBoost:
                var sem = player.GetComponent<StatusEffectManager>();
                if (sem != null)
                {
                    sem.ApplyEffect(StatusEffectType.SpeedBoost, 8f);
                    return true;
                }
                break;
        }
        return false;
    }

    [ObserversRpc]
    private void RpcPlayPickupEffects(Vector3 pos)
    {
        SFXManager.PlayPickup(pos);
    }
}
