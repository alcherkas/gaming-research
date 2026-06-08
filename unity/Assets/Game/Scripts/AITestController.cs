using UnityEngine;

public enum TestState
{
    GoToRoomC,
    GoToRoomA,
    GoToRoomB_First,
    GoToRoomD,
    GoToRoomB_Second,
    GoToRoomE,
    FightBoss,
    Done
}

public class AITestController : MonoBehaviour
{
    private TestState _state = TestState.GoToRoomC;

    private bool _firedSpell = false;
    private bool _icedSpell = false;
    private bool _lightningSpell = false;
    private bool _poisonSpell = false;
    private bool _attacked = false;
    private bool _dashed = false;
    private bool _dashPendingReset = false;
    private bool _attackPendingReset = false;
    private bool _firePendingReset = false;
    private bool _icePendingReset = false;
    private bool _lightningPendingReset = false;
    private bool _poisonPendingReset = false;
    private float _timer = 0f;
    private float _lastSpellCastTime = -10f;

    private Inventory _inventory;

    private void Start()
    {
        Debug.Log("[AITestController] Starting automated gameplay sequence...");
        InputBridge.ResetMockInputs();
        _inventory = GetComponent<Inventory>();
    }

    private Vector2 GetCurrentWaypoint()
    {
        return _state switch
        {
            TestState.GoToRoomC => GameBootstrap.RoomC,
            TestState.GoToRoomA => GameBootstrap.RoomA,
            TestState.GoToRoomB_First => GameBootstrap.RoomB,
            TestState.GoToRoomD => GameBootstrap.RoomD,
            TestState.GoToRoomB_Second => GameBootstrap.RoomB,
            TestState.GoToRoomE => GameBootstrap.RoomE,
            TestState.FightBoss => FindFirstObjectByType<BossAI>() != null ? (Vector2)FindFirstObjectByType<BossAI>().transform.position : GameBootstrap.RoomE,
            _ => (Vector2)transform.position
        };
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // Reset single-frame action triggers on the frame after they were set
        if (_dashPendingReset) { InputBridge.MockDashValue = false; _dashPendingReset = false; }
        if (_attackPendingReset) { InputBridge.MockAttackValue = false; _attackPendingReset = false; }
        if (_firePendingReset) { InputBridge.MockCastFireValue = false; _firePendingReset = false; }
        if (_icePendingReset) { InputBridge.MockCastIceValue = false; _icePendingReset = false; }
        if (_lightningPendingReset) { InputBridge.MockCastLightningValue = false; _lightningPendingReset = false; }
        if (_poisonPendingReset) { InputBridge.MockCastPoisonValue = false; _poisonPendingReset = false; }

        // Wait until player control is enabled before driving simulated inputs
        var pc = GetComponent<PlayerController>();
        if (pc == null || !pc.ControlEnabled)
        {
            return;
        }

        // Handle State Transitions
        Vector2 pos = transform.position;
        switch (_state)
        {
            case TestState.GoToRoomC:
                // Require the blue key to actually be collected in inventory before moving back
                if (_inventory != null && _inventory.HasKey(KeyType.Blue))
                {
                    _state = TestState.GoToRoomA;
                    Debug.Log("[AITestController] Blue key collected! State -> GoToRoomA");
                }
                break;

            case TestState.GoToRoomA:
                if (pos.y <= 1f)
                {
                    _state = TestState.GoToRoomB_First;
                    Debug.Log("[AITestController] Returned to Room A! State -> GoToRoomB_First");
                }
                break;

            case TestState.GoToRoomB_First:
                if (pos.x >= 9f)
                {
                    _state = TestState.GoToRoomD;
                    Debug.Log("[AITestController] Entered Room B! State -> GoToRoomD");
                }
                break;

            case TestState.GoToRoomD:
                // Require reaching close to Room D center to guarantee ManaPotion collection
                if (Vector2.Distance(pos, GameBootstrap.RoomD) < 0.4f)
                {
                    _state = TestState.GoToRoomB_Second;
                    Debug.Log("[AITestController] Reached Room D center! State -> GoToRoomB_Second");
                }
                break;

            case TestState.GoToRoomB_Second:
                if (pos.y <= 1f)
                {
                    _state = TestState.GoToRoomE;
                    Debug.Log("[AITestController] Returned to Room B! State -> GoToRoomE");
                }
                break;

            case TestState.GoToRoomE:
                if (pos.x >= 28f)
                {
                    _state = TestState.FightBoss;
                    Debug.Log("[AITestController] Entered Room E (Boss Arena)! State -> FightBoss");
                }
                break;

            case TestState.FightBoss:
                // If boss is dead, we are done
                if (FindFirstObjectByType<BossAI>() == null)
                {
                    // Look for SpeedBoost pickup if spawned
                    var boost = FindFirstObjectByType<ItemPickup>();
                    if (boost != null)
                    {
                        // Walk to the speed boost pickup
                        Vector2 target = boost.transform.position;
                        Vector2 toItem = (target - pos);
                        if (toItem.magnitude > 0.4f)
                        {
                            InputBridge.MockMoveValue = toItem.normalized;
                            return;
                        }
                    }
                    else
                    {
                        _state = TestState.Done;
                        Debug.Log("[AITestController] Boss defeated and loot collected! State -> Done");
                    }
                }
                break;

            case TestState.Done:
                InputBridge.MockMoveValue = Vector2.zero;
                return;
        }

        // Get movement towards current target waypoint using axis-aligned pathing
        Vector2 moveDir = GetDirectionToWaypoint();

        if (pc.IsDashing)
        {
            InputBridge.MockMoveValue = moveDir;
            return;
        }

        // Find nearest enemy
        EnemyAI closest = null;
        float minDist = float.MaxValue;
        foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = e;
            }
        }
        var enemy = closest;

        // Face and target enemy if close, otherwise walk towards current waypoint
        if (enemy != null && minDist < 3.5f)
        {
            moveDir = ((Vector2)enemy.transform.position - pos).normalized;
        }

        InputBridge.MockMoveValue = moveDir;

        // Combat / Spellcasting AI
        if (enemy != null && minDist < 3.8f)
        {
            var spell = GetComponent<SpellSystem>();
            bool hasManaForFireball = spell != null && spell.CurrentMana >= 20f;

            if ((!_firedSpell || (enemy is BossAI && hasManaForFireball)) && _timer - _lastSpellCastTime >= 0.45f)
            {
                InputBridge.MockCastFireValue = true;
                _firePendingReset = true;
                _firedSpell = true;
                _lastSpellCastTime = _timer;
                Debug.Log($"[AITestController] Casting Fireball at {enemy.gameObject.name}.");
            }
            else if (!_lightningSpell && _timer - _lastSpellCastTime >= 0.45f)
            {
                InputBridge.MockCastLightningValue = true;
                _lightningPendingReset = true;
                _lightningSpell = true;
                _lastSpellCastTime = _timer;
                Debug.Log($"[AITestController] Casting Lightning at {enemy.gameObject.name}.");
            }
            else if (!_poisonSpell && _timer - _lastSpellCastTime >= 0.45f)
            {
                InputBridge.MockCastPoisonValue = true;
                _poisonPendingReset = true;
                _poisonSpell = true;
                _lastSpellCastTime = _timer;
                Debug.Log($"[AITestController] Casting Poison at {enemy.gameObject.name}.");
            }
            else if (!_icedSpell && _timer - _lastSpellCastTime >= 0.45f)
            {
                InputBridge.MockCastIceValue = true;
                _icePendingReset = true;
                _icedSpell = true;
                _lastSpellCastTime = _timer;
                Debug.Log($"[AITestController] Casting Ice at {enemy.gameObject.name}.");
            }
            else if (minDist <= 1.5f)
            {
                if (!_dashed)
                {
                    InputBridge.MockDashValue = true;
                    _dashPendingReset = true;
                    _dashed = true;
                    Debug.Log("[AITestController] Dashing in close combat.");
                }
                else
                {
                    InputBridge.MockAttackValue = true;
                    _attackPendingReset = true;
                    if (!_attacked)
                    {
                        _attacked = true;
                        Debug.Log("[AITestController] Starting repeated melee attacks.");
                    }
                }
            }
        }
        else
        {
            // Reset single-use action flags when no enemy is nearby
            _firedSpell = false;
            _icedSpell = false;
            _lightningSpell = false;
            _poisonSpell = false;
            _attacked = false;
            _dashed = false;
        }
    }

    private Vector2 GetMoveInput(Vector2 targetPos, bool preferXFirst)
    {
        Vector2 pos = transform.position;
        if (preferXFirst)
        {
            // First match X coordinate
            if (Mathf.Abs(pos.x - targetPos.x) > 0.2f)
            {
                return new Vector2(Mathf.Sign(targetPos.x - pos.x), 0f);
            }
            // Then match Y coordinate
            else
            {
                return new Vector2(0f, Mathf.Sign(targetPos.y - pos.y));
            }
        }
        else
        {
            // First match Y coordinate
            if (Mathf.Abs(pos.y - targetPos.y) > 0.2f)
            {
                return new Vector2(0f, Mathf.Sign(targetPos.y - pos.y));
            }
            // Then match X coordinate
            else
            {
                return new Vector2(Mathf.Sign(targetPos.x - pos.x), 0f);
            }
        }
    }

    private Vector2 GetDirectionToWaypoint()
    {
        Vector2 target = GetCurrentWaypoint();
        bool preferXFirst = true;

        if (_state == TestState.GoToRoomB_First || _state == TestState.GoToRoomE)
        {
            preferXFirst = false; // align Y first (to 0f), then walk horizontally
        }

        return GetMoveInput(target, preferXFirst);
    }

    private void OnDestroy()
    {
        InputBridge.ResetMockInputs();
    }
}
