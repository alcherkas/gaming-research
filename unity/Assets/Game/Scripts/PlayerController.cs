using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float dashSpeed = 16f;
    [SerializeField] float dashDuration = 0.25f;
    [SerializeField] float dashCooldown = 0.8f;

    // Gated by ownership once networking is added (Phase 2D-C); true for local single-player.
    public bool ControlEnabled = true;

    Rigidbody2D _rb;
    Vector2 _facing = Vector2.down;
    Health _health;
    StatusEffectManager _sem;

    bool _isDashing = false;
    float _dashTimer = 0f;
    float _cooldownTimer = 0f;
    Vector2 _dashDir;
    float _knockbackTimer = 0f;

    public Vector2 Facing => _facing;
    public bool IsDashing => _isDashing;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _health = GetComponent<Health>();
        _sem = GetComponent<StatusEffectManager>();
    }

    void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (!ControlEnabled) return;

        // Check if frozen or shocked
        bool frozenOrShocked = _sem != null && (_sem.IsFrozen || _sem.IsShocked);

        // Check dash trigger
        if (!frozenOrShocked && !_isDashing && _cooldownTimer <= 0f && InputBridge.Dash())
        {
            Vector2 inputDir = InputBridge.Move();
            if (inputDir.sqrMagnitude < 0.01f)
                inputDir = _facing; // Dash in facing direction if stationary

            StartDash(inputDir.normalized);
        }
    }

    void StartDash(Vector2 direction)
    {
        _isDashing = true;
        _dashDir = direction;
        _dashTimer = dashDuration;
        _cooldownTimer = dashCooldown;
        _facing = direction;

        if (_health != null)
        {
            // Grant invincibility for the duration of the dash
            _health.SetInvincibility(dashDuration);
        }

        // Spawn dash trail VFX matching player's visual color
        Color trailColor = new Color(0.30f, 0.85f, 1f); // default fallback cyan
        var visual = GetComponent<PlayerVisual>();
        if (visual != null)
        {
            trailColor = visual.Color;
        }
        VFXSpawner.SpawnDashTrail(transform.position, direction, trailColor);
        SFXManager.PlayDash(transform.position);
    }

    public void ApplyKnockback(float duration)
    {
        _knockbackTimer = duration;
        _isDashing = false; // Cancel any active dash
    }

    void FixedUpdate()
    {
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        if (_sem != null && _sem.IsShocked)
        {
            _isDashing = false;
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_isDashing)
        {
            _rb.linearVelocity = _dashDir * dashSpeed;
            _dashTimer -= Time.fixedDeltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                _rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        Vector2 input = ControlEnabled ? InputBridge.Move() : Vector2.zero;

        // Fallback that always works on the Deck: hold left mouse / trackpad / touch to move toward
        // the cursor. Guarantees control even when Steam Input doesn't expose the gamepad to Unity.
        if (ControlEnabled && input.sqrMagnitude < 0.01f)
            input = MouseMove();

        float multiplier = _sem != null ? _sem.SpeedMultiplier : 1f;

        _rb.linearVelocity = input * (moveSpeed * multiplier);
        if (input.sqrMagnitude > 0.01f)
            _facing = input.normalized;
    }

    Vector2 MouseMove()
    {
        var m = Mouse.current;
        var cam = Camera.main;
        if (m == null || cam == null || !m.leftButton.isPressed)
            return Vector2.zero;

        Vector3 world = cam.ScreenToWorldPoint(m.position.ReadValue());
        Vector2 dir = (Vector2)world - _rb.position;
        return dir.sqrMagnitude > 0.04f ? dir.normalized : Vector2.zero;
    }
}
