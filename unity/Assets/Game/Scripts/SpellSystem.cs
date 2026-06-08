using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class SpellSystem : NetworkBehaviour
{
    private readonly SyncVar<float> _currentMana = new(100f);
    private readonly SyncVar<float> _maxMana = new(100f);

    [SerializeField] private float _manaRegen = 15f;
    [SerializeField] private float _swordDamage = 25f;
    [SerializeField] private float _fireCost = 20f;
    [SerializeField] private float _iceCost = 25f;
    [SerializeField] private float _lightningCost = 30f;
    [SerializeField] private float _poisonCost = 15f;
    [SerializeField] private float _attackCooldown = 0.3f;
    [SerializeField] private float _castCooldown = 0.4f;

    private float _attackCooldownTimer = 0f;
    private float _castCooldownTimer = 0f;

    private PlayerController _pc;
    private StatusEffectManager _sem;
    private GameObject _projectilePrefab;

    public float CurrentMana => _currentMana.Value;
    public float MaxMana => _maxMana.Value;

    public delegate void ManaChangedHandler(float current, float max);
    public event ManaChangedHandler OnManaChangedEvent;

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _sem = GetComponent<StatusEffectManager>();
        _currentMana.OnChange += OnManaChanged;
        _projectilePrefab = Resources.Load<GameObject>("Projectile");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentMana.Value = _maxMana.Value;
    }

    private void Update()
    {
        if (IsServerInitialized)
        {
            _currentMana.Value = Mathf.Min(_maxMana.Value, _currentMana.Value + _manaRegen * Time.deltaTime);
        }

        if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;
        if (_castCooldownTimer > 0f) _castCooldownTimer -= Time.deltaTime;



        if (!IsOwner) return;
        if (_pc == null || !_pc.ControlEnabled || _pc.IsDashing) return;
        if (_sem != null && (_sem.IsFrozen || _sem.IsShocked)) return;

        // Check melee attack
        if (InputBridge.Attack() && _attackCooldownTimer <= 0f)
        {
            _attackCooldownTimer = _attackCooldown;
            CmdAttack(transform.position, _pc.Facing);
        }

        // Check spells
        if (_castCooldownTimer <= 0f)
        {
            if (InputBridge.CastFire())
            {
                _castCooldownTimer = _castCooldown;
                CmdCastSpell(DamageType.Fire, transform.position, _pc.Facing);
            }
            else if (InputBridge.CastIce())
            {
                _castCooldownTimer = _castCooldown;
                CmdCastSpell(DamageType.Ice, transform.position, _pc.Facing);
            }
            else if (InputBridge.CastLightning())
            {
                _castCooldownTimer = _castCooldown;
                CmdCastSpell(DamageType.Lightning, transform.position, _pc.Facing);
            }
            else if (InputBridge.CastPoison())
            {
                _castCooldownTimer = _castCooldown;
                CmdCastSpell(DamageType.Poison, transform.position, _pc.Facing);
            }
        }
    }

    private static readonly Collider2D[] _attackHits = new Collider2D[32];

    [ServerRpc]
    private void CmdAttack(Vector2 origin, Vector2 direction)
    {
        if (_sem != null && (_sem.IsFrozen || _sem.IsShocked)) return;
        Debug.Log($"[SpellSystem] CmdAttack called: origin={origin}, direction={direction}");
        Vector2 hitboxCenter = origin + direction * 0.85f;
        float attackRadius = 0.8f;
        int count = Physics2D.OverlapCircleNonAlloc(hitboxCenter, attackRadius, _attackHits);

        for (int i = 0; i < count; i++)
        {
            var hit = _attackHits[i];
            if (hit != null)
            {
                if (hit.gameObject != gameObject)
                {
                    var health = hit.GetComponent<Health>();
                    if (health != null)
                    {
                        Vector2 knockback = direction * 9f;
                        health.TakeDamage(new DamageInfo(_swordDamage, DamageType.Physical, knockback, gameObject));
                    }
                }
                _attackHits[i] = null; // Clear reference
            }
        }

        RpcShowSlash(hitboxCenter, direction);
    }

    [ObserversRpc]
    private void RpcShowSlash(Vector2 position, Vector2 direction)
    {
        var slash = new GameObject("SlashVisual");
        slash.transform.position = position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        slash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        slash.transform.localScale = new Vector3(0.6f, 0.15f, 1f);

        var sr = slash.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = new Color(1f, 1f, 1f, 0.75f);
        sr.sortingOrder = 15;

        Destroy(slash, 0.1f);
        
        SFXManager.PlaySwordSwing(position);
    }

    [ServerRpc]
    private void CmdCastSpell(DamageType spellType, Vector2 origin, Vector2 direction)
    {
        if (_sem != null && (_sem.IsFrozen || _sem.IsShocked)) return;
        Debug.Log($"[SpellSystem] CmdCastSpell called: type={spellType}, origin={origin}, direction={direction}, currentMana={_currentMana.Value}");
        float cost = spellType switch
        {
            DamageType.Fire => _fireCost,
            DamageType.Ice => _iceCost,
            DamageType.Lightning => _lightningCost,
            DamageType.Poison => _poisonCost,
            _ => _fireCost
        };
        if (_currentMana.Value < cost) return;

        _currentMana.Value -= cost;

        if (_projectilePrefab == null)
        {
            Debug.LogError("Projectile prefab not cached!");
            return;
        }

        Vector2 spawnPos = origin + direction * 0.85f;
        var go = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);

        var proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.SpellType = spellType;
            proj.Direction = direction;
            proj.Owner = gameObject;
            proj.Damage = (spellType == DamageType.Fire) ? 22f : 15f;
        }

        InstanceFinder.ServerManager.Spawn(go);
    }

    private void OnManaChanged(float prev, float next, bool asServer)
    {
        OnManaChangedEvent?.Invoke(next, _maxMana.Value);
    }

    [Server]
    public void RestoreMana(float amount)
    {
        _currentMana.Value = Mathf.Min(_maxMana.Value, _currentMana.Value + amount);
        Debug.Log($"[SpellSystem] {gameObject.name} mana restored by {amount}. Current Mana: {_currentMana.Value}");
    }
}
