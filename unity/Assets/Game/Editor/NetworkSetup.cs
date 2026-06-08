using System.IO;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEngine;

// Headless setup of the FishNet networking objects: the networked Player prefab (in Resources so it
// auto-registers in FishNet's DefaultPrefabObjects) and a "Network" scene object wired with
// NetworkManager + TransportManager + Multipass[Tugboat, FishySteamworks] + PlayerSpawner + ConnectBootstrap.
public static class NetworkSetup
{
    const string ResourcesDir = "Assets/Game/Resources";
    const string PrefabPath = ResourcesDir + "/Player.prefab";

    [MenuItem("Game/Create Player Prefab")]
    public static void CreatePlayerPrefab()
    {
        Directory.CreateDirectory(ResourcesDir);

        var go = new GameObject("Player");
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        go.AddComponent<SpriteRenderer>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;
        go.AddComponent<PlayerVisual>();
        go.AddComponent<PlayerController>();
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<NetworkPlayer>();
        go.AddComponent<Health>();
        go.AddComponent<StatusEffectManager>();
        go.AddComponent<SpellSystem>();
        go.AddComponent<PlayerHUD>();
        go.AddComponent<Inventory>();

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NetworkSetup] Player prefab -> " + PrefabPath);
    }

    [MenuItem("Game/Create Ranged Enemy Prefab")]
    public static void CreateRangedEnemyPrefab()
    {
        Directory.CreateDirectory(ResourcesDir);
        const string rangedEnemyPath = ResourcesDir + "/RangedEnemy.prefab";

        var go = new GameObject("RangedEnemy");
        go.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = new Color(0.9f, 0.5f, 0.1f); // Orange-yellow
        sr.sortingOrder = 9;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        go.AddComponent<NetworkObject>();
        go.AddComponent<Health>();
        go.AddComponent<StatusEffectManager>();
        go.AddComponent<RangedEnemyAI>();

        PrefabUtility.SaveAsPrefabAsset(go, rangedEnemyPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NetworkSetup] Ranged enemy prefab -> " + rangedEnemyPath);
    }

    [MenuItem("Game/Create Locked Door Prefab")]
    public static void CreateLockedDoorPrefab()
    {
        Directory.CreateDirectory(ResourcesDir);
        const string path = ResourcesDir + "/LockedDoor.prefab";

        var go = new GameObject("LockedDoor");
        go.transform.localScale = new Vector3(3f, 0.5f, 1f);
        
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = new Color(0.2f, 0.5f, 1f, 0.9f);
        sr.sortingOrder = 5;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        go.AddComponent<NetworkObject>();
        go.AddComponent<DoorLock>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NetworkSetup] LockedDoor prefab -> " + path);
    }

    [MenuItem("Game/Create Key Pickup Prefab")]
    public static void CreateKeyPickupPrefab()
    {
        Directory.CreateDirectory(ResourcesDir);
        const string path = ResourcesDir + "/KeyPickup.prefab";

        var go = new GameObject("KeyPickup");
        go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
        
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = new Color(0.2f, 0.5f, 1f);
        sr.sortingOrder = 10;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.6f;

        go.AddComponent<NetworkObject>();
        go.AddComponent<KeyPickup>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NetworkSetup] KeyPickup prefab -> " + path);
    }

    [MenuItem("Game/Create Boss Prefab")]
    public static void CreateBossPrefab()
    {
        Directory.CreateDirectory(ResourcesDir);
        const string path = ResourcesDir + "/Boss.prefab";

        var go = new GameObject("Boss");
        go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = new Color(0.6f, 0.1f, 0.8f); // purple
        sr.sortingOrder = 9;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        go.AddComponent<NetworkObject>();
        go.AddComponent<Health>();
        go.AddComponent<StatusEffectManager>();
        go.AddComponent<BossAI>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NetworkSetup] Boss prefab -> " + path);
    }

    [MenuItem("Game/Create Item Pickup Prefab")]
    public static void CreateItemPickupPrefab()
    {
        Directory.CreateDirectory(ResourcesDir);
        const string path = ResourcesDir + "/ItemPickup.prefab";

        var go = new GameObject("ItemPickup");
        go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
        
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Disc();
        sr.color = new Color(1f, 0.2f, 0.4f);
        sr.sortingOrder = 8;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        go.AddComponent<NetworkObject>();
        go.AddComponent<ItemPickup>();

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NetworkSetup] ItemPickup prefab -> " + path);
    }


    // Adds the networking objects to the currently open scene (called from SceneBootstrap).
    public static void AddNetworkingToCurrentScene()
    {
        var go = new GameObject("Network");
        go.AddComponent<NetworkManager>();
        var tm = go.AddComponent<TransportManager>();
        var tugboat = go.AddComponent<Tugboat>();
        var fishy = go.AddComponent<FishySteamworks.FishySteamworks>();
        var mp = go.AddComponent<Multipass>();

        // Wire Multipass transports [0]=Tugboat (LAN), [1]=FishySteamworks (Steam).
        var so = new SerializedObject(mp);
        var list = so.FindProperty("_transports");
        list.arraySize = 2;
        list.GetArrayElementAtIndex(0).objectReferenceValue = tugboat;
        list.GetArrayElementAtIndex(1).objectReferenceValue = fishy;
        so.ApplyModifiedProperties();

        tm.Transport = mp;

        go.AddComponent<PlayerSpawner>();
        go.AddComponent<ConnectBootstrap>();
        Debug.Log("[NetworkSetup] Network object added to scene.");
    }
}
