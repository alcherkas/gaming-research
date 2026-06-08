using UnityEngine;

// Runtime entry point placed in the scene. Builds two connected rooms + (single-player Phase P2)
// one local player. Networking (P4) sets spawnLocalPlayer = false and lets FishNet spawn players.
public class GameBootstrap : MonoBehaviour
{
    // Networking spawns players per connection; ConnectBootstrap spawns a local one for Single Player.
    [SerializeField] bool spawnLocalPlayer = false;

    // Rooms sized ~screen aspect so each one fills the room-snap camera view.
    public const float RoomW = 19f, RoomH = 12f, Corridor = 2f;
    public static readonly Vector2 RoomA = new Vector2(-(RoomW + Corridor) * 0.5f, 0f);
    public static readonly Vector2 RoomB = new Vector2((RoomW + Corridor) * 0.5f, 0f);
    public static readonly Vector2 RoomC = RoomA + new Vector2(0f, RoomH + Corridor);
    public static readonly Vector2 RoomD = RoomB + new Vector2(0f, RoomH + Corridor);
    public static readonly Vector2 RoomE = RoomB + new Vector2(RoomW + Corridor, 0f);
    static readonly Vector2 RoomSize = new Vector2(RoomW, RoomH);

    void Start()
    {
        var world = new GameObject("World");
        var floorA = new Color(0.15f, 0.16f, 0.20f);
        var floorB = new Color(0.18f, 0.15f, 0.20f);
        var floorC = new Color(0.12f, 0.18f, 0.15f);
        var floorD = new Color(0.13f, 0.15f, 0.18f);
        var floorE = new Color(0.19f, 0.14f, 0.15f);
        var wall = new Color(0.45f, 0.42f, 0.38f);

        RoomBuilder.Build("RoomA", RoomA, RoomSize, RoomBuilder.Doors.E | RoomBuilder.Doors.N, floorA, wall, world.transform);
        RoomBuilder.Build("RoomB", RoomB, RoomSize, RoomBuilder.Doors.W | RoomBuilder.Doors.N | RoomBuilder.Doors.E, floorB, wall, world.transform);
        RoomBuilder.Build("RoomC", RoomC, RoomSize, RoomBuilder.Doors.S, floorC, wall, world.transform);
        RoomBuilder.Build("RoomD", RoomD, RoomSize, RoomBuilder.Doors.S, floorD, wall, world.transform);
        RoomBuilder.Build("RoomE", RoomE, RoomSize, RoomBuilder.Doors.W, floorE, wall, world.transform);

        BuildHorizontalCorridor(world.transform, Vector2.zero, wall); // A-B corridor
        BuildHorizontalCorridor(world.transform, RoomB + new Vector2(RoomW * 0.5f + Corridor * 0.5f, 0f), wall); // B-E corridor
        BuildVerticalCorridor(world.transform, RoomA + new Vector2(0f, RoomH * 0.5f + Corridor * 0.5f), wall); // A-C corridor
        BuildVerticalCorridor(world.transform, RoomB + new Vector2(0f, RoomH * 0.5f + Corridor * 0.5f), wall); // B-D corridor

        AddRoomTrigger(world.transform, "RoomA_Trigger", RoomA);
        AddRoomTrigger(world.transform, "RoomB_Trigger", RoomB);
        AddRoomTrigger(world.transform, "RoomC_Trigger", RoomC);
        AddRoomTrigger(world.transform, "RoomD_Trigger", RoomD);
        AddRoomTrigger(world.transform, "RoomE_Trigger", RoomE);

        if (RoomManager.Instance != null)
            RoomManager.Instance.SetActive(RoomA);

        // Dynamically configure PlayerSpawner spawn point at Room A's center
        var playerSpawner = FindFirstObjectByType<FishNet.Component.Spawning.PlayerSpawner>();
        if (playerSpawner != null)
        {
            var spawnGo = new GameObject("PlayerSpawnPoint");
            spawnGo.transform.position = new Vector3(RoomA.x, RoomA.y, 0f);
            playerSpawner.Spawns = new Transform[] { spawnGo.transform };
            Debug.Log($"[GameBootstrap] Dynamically assigned PlayerSpawner spawn point at {RoomA}");
        }

        var spawnerGo = new GameObject("EnemySpawner");
        var spawner = spawnerGo.AddComponent<EnemySpawner>();

        if (spawnLocalPlayer)
        {
            BuildPlayer(new Vector3(RoomA.x, RoomA.y, 0f), new Color(0.30f, 0.85f, 1f));
            spawner.SpawnOfflineEnemies();
        }

        var testGo = new GameObject("AutoTestManager");
        testGo.AddComponent<AutoTestManager>();
    }

    static void BuildHorizontalCorridor(Transform parent, Vector2 midpoint, Color wall)
    {
        Quad(parent, "Corridor_Floor", midpoint, new Vector2(Corridor + 0.2f, RoomBuilder.DoorWidth),
             new Color(0.16f, 0.16f, 0.19f), false, -10);
        float hy = RoomBuilder.DoorWidth * 0.5f;
        Quad(parent, "Corridor_N", midpoint + new Vector2(0f, hy), new Vector2(Corridor + 0.2f, 1f), wall, true, 0);
        Quad(parent, "Corridor_S", midpoint + new Vector2(0f, -hy), new Vector2(Corridor + 0.2f, 1f), wall, true, 0);
    }

    static void BuildVerticalCorridor(Transform parent, Vector2 midpoint, Color wall)
    {
        Quad(parent, "Corridor_Floor", midpoint, new Vector2(RoomBuilder.DoorWidth, Corridor + 0.2f),
             new Color(0.16f, 0.16f, 0.19f), false, -10);
        float hx = RoomBuilder.DoorWidth * 0.5f;
        Quad(parent, "Corridor_W", midpoint + new Vector2(-hx, 0f), new Vector2(1f, Corridor + 0.2f), wall, true, 0);
        Quad(parent, "Corridor_E", midpoint + new Vector2(hx, 0f), new Vector2(1f, Corridor + 0.2f), wall, true, 0);
    }

    static void AddRoomTrigger(Transform parent, string name, Vector2 roomCenter)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(roomCenter.x, roomCenter.y, 0f);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(RoomW + Corridor, RoomH + Corridor);
        go.AddComponent<RoomTrigger>().Center = roomCenter;
    }

    static void Quad(Transform parent, string name, Vector2 pos, Vector2 size, Color color, bool collider, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = color;
        sr.sortingOrder = order;
        if (collider) go.AddComponent<BoxCollider2D>();
    }

    // Builds a player token. Reused as the template for the networked prefab in Phase P4.
    public static GameObject BuildPlayer(Vector3 pos, Color color)
    {
        var go = new GameObject("Player");
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        go.AddComponent<SpriteRenderer>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;
        go.AddComponent<PlayerVisual>().SetColor(color);
        go.AddComponent<PlayerController>();
        go.AddComponent<Health>();
        go.AddComponent<StatusEffectManager>();
        go.AddComponent<SpellSystem>();
        go.AddComponent<PlayerHUD>();
        go.AddComponent<Inventory>();
        return go;
    }
}
