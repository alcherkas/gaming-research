using FishNet.Object;
using UnityEngine;

/// <summary>
/// Boss AI with 3-phase combat behaviour.
/// Phase 1: Chases player and performs a high-knockback melee slam.
/// Phase 2: Alternates between charging at the player and shooting fire spreads.
/// Phase 3: Enrages with fast movement, shoots lightning bolts, and releases poison AoE circles.
/// </summary>
public class BossAI : EnemyAI
{
    private float _nextSlamTime = 0f;
    private float _slamInterval = 1.2f;

    // Phase 2 timers/state
    private float _phase2ActionTimer = 0f;
    private bool _isCharging = false;
    private Vector2 _chargeDir;
    private float _chargeDurationTimer = 0f;

    // Phase 3 timers
    private float _lightningTimer = 0f;
    private float _poisonAoETimer = 0f;

    private GameObject _projectilePrefab;
    private GameObject _itemPickupPrefab;

    protected override void Awake()
    {
        base.Awake();
        _projectilePrefab = Resources.Load<GameObject>("Projectile");
        _itemPickupPrefab = Resources.Load<GameObject>("ItemPickup");
    }

    private void Start()
    {
        transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.6f, 0.1f, 0.8f); // Purple boss tint
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        HealthComponent.SetMaxHealth(300f);
    }

    protected override void FixedUpdate()
    {
        if (!IsServer) return;

        if (KnockbackTimer > 0f)
        {
            KnockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        // Find nearest player
        PlayerController target = FindNearestPlayer();

        // Freeze or Shock stops all active AI movement
        if (Sem != null && (Sem.IsFrozen || Sem.IsShocked))
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        float multiplier = Sem != null ? Sem.SpeedMultiplier : 1f;
        float currentHp = HealthComponent.CurrentHealth;

        if (target == null)
        {
            // Fall back to patrol without duplicate physics query
            _isCharging = false;
            UpdatePatrol(multiplier);
            return;
        }

        if (currentHp > 200f)
        {
            // Phase 1: Standard chase, collision slam handled by OnCollisionStay2D
            _isCharging = false;
            Vector2 toPlayer = (Vector2)target.transform.position - Rb.position;
            Rb.linearVelocity = toPlayer.normalized * (Speed * multiplier);
        }
        else if (currentHp > 100f)
        {
            // Phase 2: Alternates between charging and shooting fire spread
            if (_isCharging)
            {
                _chargeDurationTimer -= Time.fixedDeltaTime;
                Rb.linearVelocity = _chargeDir * (Speed * 2.8f * multiplier);
                if (_chargeDurationTimer <= 0f)
                {
                    _isCharging = false;
                }
                return;
            }

            _phase2ActionTimer -= Time.fixedDeltaTime;
            if (_phase2ActionTimer <= 0f)
            {
                _phase2ActionTimer = 3.0f; // action cycle every 3 seconds
                if (Random.value < 0.5f)
                {
                    // Start a charge
                    _isCharging = true;
                    _chargeDurationTimer = 1.0f;
                    _chargeDir = ((Vector2)target.transform.position - Rb.position).normalized;
                    Rb.linearVelocity = _chargeDir * (Speed * 2.8f * multiplier);
                    RpcPlayDashEffects(transform.position);
                }
                else
                {
                    // Shoot fire spread
                    ShootFireSpread(target);
                    Rb.linearVelocity = Vector2.zero;
                }
            }
            else
            {
                // Slow walk towards target while waiting
                Vector2 toPlayer = (Vector2)target.transform.position - Rb.position;
                Rb.linearVelocity = toPlayer.normalized * (Speed * 0.5f * multiplier);
            }
        }
        else
        {
            // Phase 3: Enraged chase + lightning attacks + poison AoE
            _isCharging = false;

            // Faster movement speed (1.5x)
            Vector2 toPlayer = (Vector2)target.transform.position - Rb.position;
            Rb.linearVelocity = toPlayer.normalized * (Speed * 1.5f * multiplier);

            // Lightning attack
            _lightningTimer -= Time.fixedDeltaTime;
            if (_lightningTimer <= 0f)
            {
                _lightningTimer = 1.8f;
                ShootLightning(target);
            }

            // Poison AoE
            _poisonAoETimer -= Time.fixedDeltaTime;
            if (_poisonAoETimer <= 0f)
            {
                _poisonAoETimer = 2.5f;
                SpawnPoisonAoE();
            }
        }
    }

    private void ShootFireSpread(PlayerController target)
    {
        if (_projectilePrefab == null) return;

        Vector2 baseDir = ((Vector2)target.transform.position - Rb.position).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        float[] angles = new float[] { -20f, 0f, 20f };
        foreach (float offset in angles)
        {
            float targetAngle = (baseAngle + offset) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(targetAngle), Mathf.Sin(targetAngle));
            Vector3 spawnPos = Rb.position + dir * 1.2f;

            var go = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.SpellType = DamageType.Fire;
                proj.Direction = dir;
                proj.Owner = gameObject;
                proj.Damage = 12f;
            }
            FishNet.InstanceFinder.ServerManager.Spawn(go);
        }
        RpcPlayFireCastEffects(transform.position);
    }

    private void ShootLightning(PlayerController target)
    {
        if (_projectilePrefab == null) return;

        Vector2 direction = ((Vector2)target.transform.position - Rb.position).normalized;
        Vector3 spawnPos = Rb.position + direction * 1.2f;

        var go = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.SpellType = DamageType.Lightning;
            proj.Direction = direction;
            proj.Owner = gameObject;
            proj.Damage = 14f;
        }
        FishNet.InstanceFinder.ServerManager.Spawn(go);
        RpcPlayLightningCastEffects(transform.position);
    }

    private void SpawnPoisonAoE()
    {
        if (_projectilePrefab == null) return;

        int count = 6;
        for (int i = 0; i < count; i++)
        {
            float angle = (i * 360f / count) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 spawnPos = Rb.position + dir * 1.0f;

            var go = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.SpellType = DamageType.Poison;
                proj.Direction = dir;
                proj.Owner = gameObject;
                proj.Damage = 8f;
                proj.Speed = 5f;      // slow expanding
                proj.Lifetime = 2.2f;  // short lifetime acting as AoE
            }
            FishNet.InstanceFinder.ServerManager.Spawn(go);
        }
        RpcPlayPoisonAoEEffect(transform.position);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsServer) return;
        if (Sem != null && (Sem.IsFrozen || Sem.IsShocked)) return;

        var pc = collision.gameObject.GetComponent<PlayerController>();
        var playerHealth = collision.gameObject.GetComponent<Health>();

        if (pc != null && playerHealth != null)
        {
            if (Time.time >= _nextSlamTime)
            {
                _nextSlamTime = Time.time + _slamInterval;
                Vector2 pushDir = ((Vector2)collision.transform.position - Rb.position).normalized;

                float currentHp = HealthComponent.CurrentHealth;
                float damage = 18f;
                float knockbackForce = 14f; // high knockback
                DamageType dmgType = DamageType.Physical;

                if (currentHp <= 100f)
                {
                    damage = 22f;
                    knockbackForce = 8f;
                    dmgType = DamageType.Lightning;
                }

                playerHealth.TakeDamage(new DamageInfo(damage, dmgType, pushDir * knockbackForce, gameObject));
            }
        }
    }

    protected override void HandleDeath()
    {
        if (IsServer)
        {
            // Spawns a SpeedBoost item pickup as reward at boss location
            if (_itemPickupPrefab != null)
            {
                var go = Instantiate(_itemPickupPrefab, transform.position, Quaternion.identity);
                var item = go.GetComponent<ItemPickup>();
                if (item != null)
                {
                    item.ItemType = ItemType.SpeedBoost;
                }
                FishNet.InstanceFinder.ServerManager.Spawn(go);
                Debug.Log("[BossAI] Boss defeated! Spawning SpeedBoost pickup reward.");
            }

            base.HandleDeath();
        }
    }

    [ObserversRpc]
    private void RpcPlayDashEffects(Vector3 pos)
    {
        SFXManager.PlayDash(pos);
    }

    [ObserversRpc]
    private void RpcPlayFireCastEffects(Vector3 pos)
    {
        SFXManager.PlaySpellCast(pos, DamageType.Fire);
    }

    [ObserversRpc]
    private void RpcPlayLightningCastEffects(Vector3 pos)
    {
        SFXManager.PlaySpellCast(pos, DamageType.Lightning);
    }

    [ObserversRpc]
    private void RpcPlayPoisonAoEEffect(Vector3 pos)
    {
        VFXSpawner.SpawnSpellImpact(pos, DamageType.Poison);
        SFXManager.PlaySpellCast(pos, DamageType.Poison);
    }
}
