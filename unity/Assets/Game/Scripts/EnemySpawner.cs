using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;
        }
    }

    private void OnDestroy()
    {
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("[EnemySpawner] Server started. Spawning networked enemies...");
            SpawnAllEnemies(true);
        }
    }

    public void SpawnOfflineEnemies()
    {
        Debug.Log("[EnemySpawner] Spawning offline enemies...");
        SpawnAllEnemies(false);
    }

    private struct EnemySpawnInfo
    {
        public GameObject Prefab;
        public Vector2 Position;
        public bool IsPoison;
    }

    private void SpawnAllEnemies(bool networkSpawn)
    {
        var enemyPrefab = Resources.Load<GameObject>("Enemy");
        var rangedPrefab = Resources.Load<GameObject>("RangedEnemy");

        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab not found in Resources!");
            return;
        }

        // Configuration for enemies:
        // - Melee poison elemental enemy in Room A
        // - Standard melee enemy directly in player path in Room A
        // - Standard melee enemy in Room B
        // - Ranged enemy in Room B
        var spawns = new EnemySpawnInfo[]
        {
            new EnemySpawnInfo { Prefab = enemyPrefab, Position = GameBootstrap.RoomA + new Vector2(-3f, -2f), IsPoison = true },
            new EnemySpawnInfo { Prefab = enemyPrefab, Position = GameBootstrap.RoomA + new Vector2(4.5f, 0.4f), IsPoison = false },
            new EnemySpawnInfo { Prefab = enemyPrefab, Position = GameBootstrap.RoomB + new Vector2(-2f, 2f), IsPoison = false },
            new EnemySpawnInfo { Prefab = rangedPrefab != null ? rangedPrefab : enemyPrefab, Position = GameBootstrap.RoomB + new Vector2(4f, -3f), IsPoison = false }
        };

        for (int i = 0; i < spawns.Length; i++)
        {
            var s = spawns[i];
            var go = Instantiate(s.Prefab, new Vector3(s.Position.x, s.Position.y, 0f), Quaternion.identity);

            // Configure elemental contact damage and color
            var ai = go.GetComponent<EnemyAI>();
            if (ai != null)
            {
                if (s.IsPoison)
                {
                    ai.ContactDamageType = DamageType.Poison;
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = new Color(0.2f, 0.75f, 0.2f); // Green tint for poison elemental
                    }
                }
            }

            if (networkSpawn)
            {
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.Spawn(go);
                }
            }
        }

        // Spawn Level progression objects: Blue key in Room C, Blue door blocking Room D
        var doorPrefab = Resources.Load<GameObject>("LockedDoor");
        var keyPrefab = Resources.Load<GameObject>("KeyPickup");
        var bossPrefab = Resources.Load<GameObject>("Boss");
        var itemPrefab = Resources.Load<GameObject>("ItemPickup");

        if (doorPrefab != null && keyPrefab != null)
        {
            // 1. Blue Key Pickup in Room C center
            Vector2 keyPos = GameBootstrap.RoomC;
            var keyGo = Instantiate(keyPrefab, new Vector3(keyPos.x, keyPos.y, 0f), Quaternion.identity);
            var kp = keyGo.GetComponent<KeyPickup>();
            if (kp != null) kp.KeyType = KeyType.Blue;

            // 2. Blue Locked Door between Room B and Room D (corridor midpoint)
            Vector2 doorPos = new Vector2(GameBootstrap.RoomB.x, 7.0f);
            var doorGo = Instantiate(doorPrefab, new Vector3(doorPos.x, doorPos.y, 0f), Quaternion.identity);
            var dl = doorGo.GetComponent<DoorLock>();
            if (dl != null) dl.KeyType = KeyType.Blue;

            if (networkSpawn)
            {
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.Spawn(keyGo);
                    InstanceFinder.ServerManager.Spawn(doorGo);
                }
            }
        }

        // Spawn Boss in Room E, items in Room C and Room D
        if (bossPrefab != null)
        {
            Vector2 bossPos = GameBootstrap.RoomE;
            var bossGo = Instantiate(bossPrefab, new Vector3(bossPos.x, bossPos.y, 0f), Quaternion.identity);
            if (networkSpawn && InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.Spawn(bossGo);
            }
        }

        if (itemPrefab != null)
        {
            // Health potion in Room C (offset from Key)
            Vector2 hpPos = GameBootstrap.RoomC + new Vector2(2f, 0f);
            var hpGo = Instantiate(itemPrefab, new Vector3(hpPos.x, hpPos.y, 0f), Quaternion.identity);
            var hpItem = hpGo.GetComponent<ItemPickup>();
            if (hpItem != null) hpItem.ItemType = ItemType.HealthPotion;

            // Mana potion in Room D center
            Vector2 mpPos = GameBootstrap.RoomD;
            var mpGo = Instantiate(itemPrefab, new Vector3(mpPos.x, mpPos.y, 0f), Quaternion.identity);
            var mpItem = mpGo.GetComponent<ItemPickup>();
            if (mpItem != null) mpItem.ItemType = ItemType.ManaPotion;

            if (networkSpawn && InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.Spawn(hpGo);
                InstanceFinder.ServerManager.Spawn(mpGo);
            }
        }
    }
}
