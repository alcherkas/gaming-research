using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class StatusEffectManager : NetworkBehaviour
{
    private const int EffectBurn = 1 << 0;
    private const int EffectFreeze = 1 << 1;
    private const int EffectPoison = 1 << 2;
    private const int EffectShock = 1 << 3;
    private const int EffectSpeedBoost = 1 << 4;

    private readonly SyncVar<int> _activeEffectsMask = new(0);

    private float _burnTimer = 0f;
    private float _freezeTimer = 0f;
    private float _poisonTimer = 0f;
    private float _shockTimer = 0f;
    private float _speedBoostTimer = 0f;

    private float _nextBurnTick = 0f;
    private float _nextPoisonTick = 0f;

    private Health _health;
    private SpriteRenderer _sr;
    private Color _baselineColor = Color.white;
    private bool _hasBaseline = false;

    public bool IsFrozen => (_activeEffectsMask.Value & EffectFreeze) != 0;
    public bool IsPoisoned => (_activeEffectsMask.Value & EffectPoison) != 0;
    public bool IsBurning => (_activeEffectsMask.Value & EffectBurn) != 0;
    public bool IsShocked => (_activeEffectsMask.Value & EffectShock) != 0;
    public bool IsSpeedBoosted => (_activeEffectsMask.Value & EffectSpeedBoost) != 0;

    public float SpeedMultiplier
    {
        get
        {
            if (IsFrozen) return 0f;
            if (IsShocked) return 0f;
            float mult = 1f;
            if (IsPoisoned) mult *= 0.65f;
            if (IsSpeedBoosted) mult *= 1.5f;
            return mult;
        }
    }

    private void Awake()
    {
        _health = GetComponent<Health>();
        _sr = GetComponent<SpriteRenderer>();
        _activeEffectsMask.OnChange += OnEffectsMaskChanged;
    }

    private void Start()
    {
        CacheBaselineColor();
    }

    private void CacheBaselineColor()
    {
        if (_hasBaseline) return;
        var visual = GetComponent<PlayerVisual>();
        if (visual != null)
        {
            _baselineColor = visual.Color;
        }
        else if (_sr != null)
        {
            _baselineColor = _sr.color;
        }
        _hasBaseline = true;
    }

    [Server]
    public void ApplyEffect(StatusEffectType type, float duration)
    {
        if (type == StatusEffectType.None) return;

        switch (type)
        {
            case StatusEffectType.Burn:
                _burnTimer = Mathf.Max(_burnTimer, duration);
                _activeEffectsMask.Value |= EffectBurn;
                break;
            case StatusEffectType.Freeze:
                _freezeTimer = Mathf.Max(_freezeTimer, duration);
                _activeEffectsMask.Value |= EffectFreeze;
                break;
            case StatusEffectType.Poison:
                _poisonTimer = Mathf.Max(_poisonTimer, duration);
                _activeEffectsMask.Value |= EffectPoison;
                break;
            case StatusEffectType.Shock:
                _shockTimer = Mathf.Max(_shockTimer, duration);
                _activeEffectsMask.Value |= EffectShock;
                break;
            case StatusEffectType.SpeedBoost:
                _speedBoostTimer = Mathf.Max(_speedBoostTimer, duration);
                _activeEffectsMask.Value |= EffectSpeedBoost;
                break;
        }
    }

    private void Update()
    {
        if (!IsServerInitialized) return;

        // Process active status timers and damage ticks on server
        if (IsBurning)
        {
            _burnTimer -= Time.deltaTime;
            if (Time.time >= _nextBurnTick)
            {
                _nextBurnTick = Time.time + 0.5f;
                if (_health != null)
                {
                    _health.TakeDamage(new DamageInfo(4f, DamageType.Fire, Vector2.zero, gameObject));
                }
            }
            if (_burnTimer <= 0f)
            {
                _activeEffectsMask.Value &= ~EffectBurn;
            }
        }

        if (IsFrozen)
        {
            _freezeTimer -= Time.deltaTime;
            if (_freezeTimer <= 0f)
            {
                _activeEffectsMask.Value &= ~EffectFreeze;
            }
        }

        if (IsPoisoned)
        {
            _poisonTimer -= Time.deltaTime;
            if (Time.time >= _nextPoisonTick)
            {
                _nextPoisonTick = Time.time + 1.5f;
                if (_health != null)
                {
                    _health.TakeDamage(new DamageInfo(3f, DamageType.Poison, Vector2.zero, gameObject));
                }
            }
            if (_poisonTimer <= 0f)
            {
                _activeEffectsMask.Value &= ~EffectPoison;
            }
        }

        if (IsShocked)
        {
            _shockTimer -= Time.deltaTime;
            if (_shockTimer <= 0f)
            {
                _activeEffectsMask.Value &= ~EffectShock;
            }
        }

        if (IsSpeedBoosted)
        {
            _speedBoostTimer -= Time.deltaTime;
            if (_speedBoostTimer <= 0f)
            {
                _activeEffectsMask.Value &= ~EffectSpeedBoost;
            }
        }
    }

    private void OnEffectsMaskChanged(int prev, int next, bool asServer)
    {
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (_sr == null) return;
        CacheBaselineColor();

        // Check effects in order of severity
        if (IsFrozen)
        {
            _sr.color = new Color(0.2f, 0.6f, 1f); // Blue freeze tint
        }
        else if (IsBurning)
        {
            _sr.color = new Color(1f, 0.3f, 0.1f); // Burning red/orange tint
        }
        else if (IsPoisoned)
        {
            _sr.color = new Color(0.3f, 0.8f, 0.3f); // Poison green tint
        }
        else if (IsShocked)
        {
            _sr.color = new Color(1f, 0.9f, 0.2f); // Shock yellow tint
        }
        else if (IsSpeedBoosted)
        {
            _sr.color = new Color(0.5f, 1f, 1f); // Cyan/Light-blue speed boost tint
        }
        else
        {
            _sr.color = _baselineColor; // Restore baseline color
        }
    }

    public void SetBaselineColor(Color color)
    {
        _baselineColor = color;
        _hasBaseline = true;
        UpdateVisuals();
    }
}
