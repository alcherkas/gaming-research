using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(StatusEffectManager))]
public class EnemyAI : NetworkBehaviour
{
    [SerializeField] private float _speed = 2.8f;
    [SerializeField] private float _detectionRadius = 6.5f;
    [SerializeField] private float _attackDamage = 8f;
    [SerializeField] private float _attackInterval = 1f;
    [SerializeField] private DamageType _contactDamageType = DamageType.Physical;

    private Rigidbody2D _rb;
    private StatusEffectManager _sem;
    private Health _health;

    private float _patrolTimer = 0f;
    private Vector2 _patrolDir = Vector2.zero;
    private float _nextAttackTime = 0f;
    private float _knockbackTimer = 0f;

    protected Rigidbody2D Rb => _rb;
    protected StatusEffectManager Sem => _sem;
    protected Health HealthComponent => _health;
    protected float Speed => _speed;
    protected float DetectionRadius => _detectionRadius;
    protected float KnockbackTimer { get => _knockbackTimer; set => _knockbackTimer = value; }
    public DamageType ContactDamageType { get => _contactDamageType; set => _contactDamageType = value; }

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        _sem = GetComponent<StatusEffectManager>();
        _health = GetComponent<Health>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _health.OnDeath += HandleDeath;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        _health.OnDeath -= HandleDeath;
    }

    public void ApplyKnockback(float duration)
    {
        _knockbackTimer = duration;
    }

    protected void UpdatePatrol(float multiplier)
    {
        _patrolTimer -= Time.fixedDeltaTime;
        if (_patrolTimer <= 0f)
        {
            _patrolTimer = Random.Range(1.5f, 4f);
            if (Random.value < 0.25f)
            {
                _patrolDir = Vector2.zero; // Rest
            }
            else
            {
                _patrolDir = Random.insideUnitCircle.normalized;
            }
        }
        _rb.linearVelocity = _patrolDir * (_speed * 0.4f * multiplier);
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer) return;

        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        // Find nearest player
        PlayerController target = FindNearestPlayer();

        // Freeze or Shock stops all active AI movement
        if (_sem != null && (_sem.IsFrozen || _sem.IsShocked))
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        float multiplier = _sem != null ? _sem.SpeedMultiplier : 1f;

        if (target != null)
        {
            // Chase target player
            Vector2 toPlayer = (Vector2)target.transform.position - _rb.position;
            Vector2 chaseDir = toPlayer.normalized;
            _rb.linearVelocity = chaseDir * (_speed * multiplier);
        }
        else
        {
            UpdatePatrol(multiplier);
        }
    }

    private static readonly Collider2D[] _detectionHits = new Collider2D[32];

    protected PlayerController FindNearestPlayer()
    {
        int count = Physics2D.OverlapCircleNonAlloc(_rb.position, _detectionRadius, _detectionHits);
        PlayerController nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var hit = _detectionHits[i];
            if (hit != null)
            {
                var pc = hit.GetComponent<PlayerController>();
                if (pc != null && pc.ControlEnabled)
                {
                    float dist = Vector2.Distance(_rb.position, hit.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = pc;
                    }
                }
                _detectionHits[i] = null; // Clear reference
            }
        }
        return nearest;
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsServer) return;
        if (_sem != null && (_sem.IsFrozen || _sem.IsShocked)) return;

        // Check if collision is a player
        var pc = collision.gameObject.GetComponent<PlayerController>();
        var playerHealth = collision.gameObject.GetComponent<Health>();
        
        if (pc != null && playerHealth != null)
        {
            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _attackInterval;
                Vector2 pushDir = ((Vector2)collision.transform.position - _rb.position).normalized;
                Vector2 knockback = pushDir * 6f;

                playerHealth.TakeDamage(new DamageInfo(_attackDamage, _contactDamageType, knockback, gameObject));
            }
        }
    }

    protected virtual void HandleDeath()
    {
        if (IsServer)
        {
            ServerManager.Despawn(gameObject);
        }
    }
}
