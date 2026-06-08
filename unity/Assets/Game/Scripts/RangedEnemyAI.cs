using FishNet.Object;
using UnityEngine;

/// <summary>
/// Ranged variant of EnemyAI.
/// Keeps distance from the player and shoots fire projectiles.
/// </summary>
public class RangedEnemyAI : EnemyAI
{
    [SerializeField] private float _shootInterval = 1.5f;
    [SerializeField] private float _preferredRangeMin = 4f;
    [SerializeField] private float _preferredRangeMax = 6f;
    [SerializeField] private float _retreatRange = 3f;

    private float _nextShootTime = 0f;
    private GameObject _projectilePrefab;

    protected override void Awake()
    {
        base.Awake();
        _projectilePrefab = Resources.Load<GameObject>("Projectile");
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

        if (target != null)
        {
            Vector2 toPlayer = (Vector2)target.transform.position - Rb.position;
            float dist = toPlayer.magnitude;
            Vector2 chaseDir = toPlayer.normalized;

            if (dist < _retreatRange)
            {
                // Retreat from player if too close
                Rb.linearVelocity = -chaseDir * (Speed * multiplier);
            }
            else if (dist > _preferredRangeMax)
            {
                // Chase player to get into preferred range
                Rb.linearVelocity = chaseDir * (Speed * multiplier);
            }
            else
            {
                // In preferred range: stand still
                Rb.linearVelocity = Vector2.zero;
            }

            // Fire projectiles at player
            if (Time.time >= _nextShootTime)
            {
                _nextShootTime = Time.time + _shootInterval;
                ShootAtPlayer(target);
            }
        }
        else
        {
            // No player: run patrol logic directly without querying physics again
            UpdatePatrol(multiplier);
        }
    }

    private void ShootAtPlayer(PlayerController player)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogError("Projectile prefab not cached!");
            return;
        }

        Vector2 direction = ((Vector2)player.transform.position - Rb.position).normalized;
        Vector2 spawnPos = Rb.position + direction * 0.85f;
        var go = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);

        var proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.SpellType = DamageType.Fire;
            proj.Direction = direction;
            proj.Owner = gameObject;
            proj.Damage = 10f; // Slightly lower contact damage than sword/spells
        }

        FishNet.InstanceFinder.ServerManager.Spawn(go);
    }
}
