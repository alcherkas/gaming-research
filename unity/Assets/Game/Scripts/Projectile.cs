using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : NetworkBehaviour
{
    private readonly SyncVar<DamageType> _spellType = new(DamageType.Fire);
    public DamageType SpellType
    {
        get => _spellType.Value;
        set => _spellType.Value = value;
    }

    public Vector2 Direction = Vector2.right;
    public float Speed = 12f;
    public float Damage = 15f;
    public float Lifetime = 2.5f;
    public GameObject Owner { get; set; }

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        var col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        _spellType.OnChange += OnSpellTypeChanged;
    }

    private void OnDestroy()
    {
        _spellType.OnChange -= OnSpellTypeChanged;
    }

    private void OnSpellTypeChanged(DamageType prev, DamageType next, bool asServer)
    {
        UpdateVisuals();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Vary speed/lifetime by spell type
        switch (SpellType)
        {
            case DamageType.Lightning:
                Speed = 18f;
                Lifetime = 1.8f;
                break;
            case DamageType.Poison:
                Speed = 7f;
                Lifetime = 3.5f;
                break;
        }
        if (_rb != null)
        {
            _rb.linearVelocity = Direction * Speed;
        }
        Invoke(nameof(DespawnProjectile), Lifetime);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateVisuals();
        SFXManager.PlaySpellCast(transform.position, SpellType);
    }

    private void UpdateVisuals()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Disc();
        sr.sortingOrder = 12;

        switch (SpellType)
        {
            case DamageType.Fire:
                sr.color = new Color(1f, 0.35f, 0.15f);
                transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                break;
            case DamageType.Ice:
                sr.color = new Color(0.2f, 0.6f, 1f);
                transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                break;
            case DamageType.Lightning:
                sr.color = new Color(1f, 0.9f, 0.2f);
                transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                break;
            case DamageType.Poison:
                sr.color = new Color(0.3f, 0.8f, 0.2f);
                transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                break;
            default:
                sr.color = Color.white;
                transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                break;
        }
    }

    private void DespawnProjectile()
    {
        if (IsServer)
        {
            ServerManager.Despawn(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        Debug.Log($"[Projectile] Collision with {other.gameObject.name}, isTrigger={other.isTrigger}");

        // Ignore collisions with the spell caster
        if (Owner != null && (other.gameObject == Owner || other.transform.IsChildOf(Owner.transform)))
        {
            Debug.Log("[Projectile] Ignoring owner");
            return;
        }

        // Ignore triggers, like other rooms or triggers
        if (other.isTrigger)
        {
            Debug.Log("[Projectile] Ignoring trigger");
            return;
        }

        // Apply damage if target has Health component
        var health = other.GetComponent<Health>();
        if (health != null)
        {
            Vector2 knockback = Direction * 7f;
            Debug.Log($"[Projectile] Applying {Damage} damage to {other.gameObject.name}");
            health.TakeDamage(new DamageInfo(Damage, SpellType, knockback, Owner));
        }
        else
        {
            Debug.Log("[Projectile] Target does not have Health component");
        }

        // Despawn on any solid obstacle hit
        RpcSpawnImpact(transform.position);
        CancelInvoke(nameof(DespawnProjectile));
        DespawnProjectile();
    }

    [ObserversRpc]
    private void RpcSpawnImpact(Vector2 pos)
    {
        VFXSpawner.SpawnSpellImpact(pos, SpellType);
    }
}

