using UnityEngine;

public class AutoTestManager : MonoBehaviour
{
    private bool _testActive = false;
    private Vector3 _startPos;
    private bool _startedMonitoring = false;

    // Checkpoints
    private bool _playerMoved = false;
    private bool _playerDashed = false;
    private bool _spellCast = false;
    private bool _lightningSpellCast = false;
    private bool _poisonSpellCast = false;
    private bool _enemyDamaged = false;
    private bool _roomTransitioned = false;
    private bool _bossSpawned = false;
    private bool _bossDefeated = false;
    private bool _speedBoostCollected = false;

    private float _timeoutTimer = 35f; // 35 seconds to complete all phases

    private void Awake()
    {
        // Only run if the -headlessTest flag is present
        bool isHeadlessTest = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-headlessTest") >= 0;
        if (!isHeadlessTest)
        {
            Destroy(gameObject);
            return;
        }

        _testActive = true;
        Debug.Log("[AutoTestManager] Headless automation test manager initialized.");
    }

    private void Update()
    {
        if (!_testActive) return;

        _timeoutTimer -= Time.deltaTime;
        if (_timeoutTimer <= 0f)
        {
            FailTest("Timeout reached before all checkpoints passed.");
            return;
        }

        // Find local player
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        if (!_startedMonitoring)
        {
            _startPos = player.transform.position;
            // Inject AI Controller to drive inputs
            player.gameObject.AddComponent<AITestController>();
            _startedMonitoring = true;
            Debug.Log("[AutoTestManager] Player detected. Monitoring started.");
        }

        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[AutoTestManager] Player Position: {player.transform.position}");
        }

        // 1. Verify movement
        if (!_playerMoved && Vector3.Distance(player.transform.position, _startPos) > 1.5f)
        {
            _playerMoved = true;
            Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Player moved.");
        }

        // 2. Verify dash
        if (!_playerDashed && player.IsDashing)
        {
            _playerDashed = true;
            Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Player dashed.");
        }

        // 3. Verify spell cast (projectile spawned)
        if (!_spellCast && FindFirstObjectByType<Projectile>() != null)
        {
            _spellCast = true;
            Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Spell projectile spawned.");
        }

        // Verify lightning spell cast (projectile of type Lightning spawned)
        if (!_lightningSpellCast)
        {
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (p.SpellType == DamageType.Lightning)
                {
                    _lightningSpellCast = true;
                    Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Lightning spell projectile spawned.");
                    break;
                }
            }
        }

        // Verify poison spell cast (projectile of type Poison spawned)
        if (!_poisonSpellCast)
        {
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (p.SpellType == DamageType.Poison)
                {
                    _poisonSpellCast = true;
                    Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Poison spell projectile spawned.");
                    break;
                }
            }
        }

        // 4. Verify combat damage dealt to an enemy
        if (!_enemyDamaged)
        {
            foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                var health = enemy.GetComponent<Health>();
                if (health != null && health.CurrentHealth < health.MaxHealth)
                {
                    _enemyDamaged = true;
                    Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Enemy damaged. Current HP: " + health.CurrentHealth);
                    break;
                }
            }
        }

        // 5. Verify room transition (camera snap to Room B center)
        if (!_roomTransitioned && RoomManager.Instance != null && RoomManager.Instance.ActiveCenter == GameBootstrap.RoomB)
        {
            _roomTransitioned = true;
            Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Room transitioned to Room B.");
        }

        // Verify Boss spawned and defeated
        if (!_bossSpawned && FindFirstObjectByType<BossAI>() != null)
        {
            _bossSpawned = true;
            Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Boss spawned.");
        }

        if (_bossSpawned && !_bossDefeated && FindFirstObjectByType<BossAI>() == null)
        {
            _bossDefeated = true;
            Debug.Log("[AutoTestManager] CHECKPOINT PASSED: Boss defeated.");
        }

        // Verify SpeedBoost collected
        if (!_speedBoostCollected)
        {
            var sem = player.GetComponent<StatusEffectManager>();
            if (sem != null && sem.IsSpeedBoosted)
            {
                _speedBoostCollected = true;
                Debug.Log("[AutoTestManager] CHECKPOINT PASSED: SpeedBoost status effect active on player.");
            }
        }

        // Check if all passed
        if (_playerMoved && _playerDashed && _spellCast && _lightningSpellCast && _poisonSpellCast && _enemyDamaged && _roomTransitioned && _bossDefeated && _speedBoostCollected)
        {
            PassTest();
        }
    }

    private void PassTest()
    {
        _testActive = false;
        Debug.Log("\n==========================================");
        Debug.Log("[AUTOTEST] RESULT: PASSED");
        Debug.Log("All checkpoints verified successfully!");
        Debug.Log("==========================================\n");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#else
        Application.Quit(0);
#endif
    }

    private void FailTest(string reason)
    {
        _testActive = false;
        Debug.LogError("\n==========================================");
        Debug.LogError("[AUTOTEST] RESULT: FAILED");
        Debug.LogError("Reason: " + reason);
        Debug.LogError($"Checkpoints: \n" +
                       $"  - Player Moved: {_playerMoved}\n" +
                       $"  - Player Dashed: {_playerDashed}\n" +
                       $"  - Spell Cast: {_spellCast}\n" +
                       $"  - Lightning Spell Cast: {_lightningSpellCast}\n" +
                       $"  - Poison Spell Cast: {_poisonSpellCast}\n" +
                       $"  - Enemy Damaged: {_enemyDamaged}\n" +
                       $"  - Room Transitioned: {_roomTransitioned}\n" +
                       $"  - Boss Spawned: {_bossSpawned}\n" +
                       $"  - Boss Defeated: {_bossDefeated}\n" +
                       $"  - SpeedBoost Collected: {_speedBoostCollected}");
        Debug.LogError("==========================================\n");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(1);
#else
        Application.Quit(1);
#endif
    }
}
