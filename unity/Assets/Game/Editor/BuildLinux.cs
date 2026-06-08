using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using FishNet.Object;
using FishNet.Managing.Object;

// Headless Linux x86_64 / IL2CPP / Vulkan-first build for the Steam Deck.
// Run: Unity -batchmode -quit -projectPath unity -executeMethod BuildLinux.Build
public static class BuildLinux
{
    // Release build (default).
    public static void Build() => Run(false);

    // Development build: profiler auto-connect + script debugging, so the Unity Profiler can be
    // attached remotely from the Mac and the in-game overlay's ProfilerRecorder render stats populate.
    public static void BuildDev() => Run(true);

    static void Run(bool development)
    {
        const string outDir = "Build/Linux";
        const string exe = "PhysicsProto.x86_64";
        Directory.CreateDirectory(outDir);

        SetupPrefabs();

        PlayerSettings.companyName = "GamingResearch";
        PlayerSettings.productName = "PhysicsProto";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,
            new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore });
        // Required for FrameTimingManager (CPU/GPU ms in the debug overlay).
        PlayerSettings.enableFrameTimingStats = true;
        PlayerSettings.gcIncremental = true;

        // P3 hardening (release): "Faster runtime" codegen, aggressive managed stripping, engine
        // code stripping -> smaller binary, lower startup + memory footprint. Dev builds stay
        // light-stripped so profiler/debugging and ProfilerRecorder stats keep working.
        var nbt = NamedBuildTarget.Standalone;
        PlayerSettings.SetIl2CppCompilerConfiguration(nbt,
            development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetIl2CppCodeGeneration(nbt,
            development ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetManagedStrippingLevel(nbt,
            development ? ManagedStrippingLevel.Minimal : ManagedStrippingLevel.High);
        PlayerSettings.stripEngineCode = !development;

        var options = BuildOptions.None;
        if (development)
            // Development enables the overlay's ProfilerRecorder stats. We deliberately do NOT add
            // ConnectWithProfiler/AllowDebugging — autoconnect makes the player stall at startup
            // trying to reach the editor. Attach the profiler manually if needed.
            options |= BuildOptions.Development;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Game/Scenes/Main.unity" },
            locationPathName = Path.Combine(outDir, exe),
            target = BuildTarget.StandaloneLinux64,
            targetGroup = BuildTargetGroup.Standalone,
            options = options,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log("[BuildLinux] dev=" + development + " result=" + s.result +
                  " sizeBytes=" + s.totalSize + " errors=" + s.totalErrors);

        // steam_appid.txt next to the binary lets Steam init out-of-Steam (480 = Spacewar test app).
        if (s.result == BuildResult.Succeeded && File.Exists("steam_appid.txt"))
            File.Copy("steam_appid.txt", Path.Combine(outDir, "steam_appid.txt"), true);

        if (s.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    [InitializeOnLoadMethod]
    public static void SetupPrefabs()
    {
        const string prefabPath = "Assets/Game/Resources/Projectile.prefab";
        const string playerPath = "Assets/Game/Resources/Player.prefab";
        const string dpoPath = "Assets/DefaultPrefabObjects.asset";

        // Check if Projectile prefab already exists
        GameObject prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabGo == null)
        {
            Debug.Log("[BuildLinux] Creating Projectile prefab dynamically...");
            Directory.CreateDirectory("Assets/Game/Resources");
            
            var go = new GameObject("Projectile");
            go.AddComponent<SpriteRenderer>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            
            go.AddComponent<NetworkObject>();
            go.AddComponent<Projectile>();
            
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            
            prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // Check if Player prefab exists and needs components injected
        GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (playerAsset != null)
        {
            bool changed = false;
            if (playerAsset.GetComponent<Health>() == null) changed = true;
            if (playerAsset.GetComponent<StatusEffectManager>() == null) changed = true;
            if (playerAsset.GetComponent<SpellSystem>() == null) changed = true;
            if (playerAsset.GetComponent<PlayerHUD>() == null) changed = true;
            if (playerAsset.GetComponent<Inventory>() == null) changed = true;

            if (changed)
            {
                Debug.Log("[BuildLinux] Injecting missing components (Health, StatusEffectManager, SpellSystem, PlayerHUD, Inventory) into Player prefab...");
                // Open prefab in context of instance
                GameObject instance = PrefabUtility.InstantiatePrefab(playerAsset) as GameObject;
                if (instance != null)
                {
                    if (instance.GetComponent<Health>() == null) instance.AddComponent<Health>();
                    if (instance.GetComponent<StatusEffectManager>() == null) instance.AddComponent<StatusEffectManager>();
                    if (instance.GetComponent<SpellSystem>() == null) instance.AddComponent<SpellSystem>();
                    if (instance.GetComponent<PlayerHUD>() == null) instance.AddComponent<PlayerHUD>();
                    if (instance.GetComponent<Inventory>() == null) instance.AddComponent<Inventory>();
                    
                    PrefabUtility.SaveAsPrefabAsset(instance, playerPath);
                    Object.DestroyImmediate(instance);
                }
            }
        }

        const string enemyPath = "Assets/Game/Resources/Enemy.prefab";
        // Check if Enemy prefab already exists
        GameObject enemyGo = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
        if (enemyGo == null)
        {
            Debug.Log("[BuildLinux] Creating Enemy prefab dynamically...");
            var go = new GameObject("Enemy");
            go.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square();
            sr.color = new Color(0.85f, 0.2f, 0.2f); // Red tint
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
            go.AddComponent<EnemyAI>();

            PrefabUtility.SaveAsPrefabAsset(go, enemyPath);
            Object.DestroyImmediate(go);
            
            enemyGo = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
        }

        const string rangedEnemyPath = "Assets/Game/Resources/RangedEnemy.prefab";
        GameObject rangedEnemyGo = AssetDatabase.LoadAssetAtPath<GameObject>(rangedEnemyPath);
        if (rangedEnemyGo == null)
        {
            Debug.Log("[BuildLinux] Creating RangedEnemy prefab dynamically...");
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
            
            rangedEnemyGo = AssetDatabase.LoadAssetAtPath<GameObject>(rangedEnemyPath);
        }

        const string doorPath = "Assets/Game/Resources/LockedDoor.prefab";
        GameObject doorGo = AssetDatabase.LoadAssetAtPath<GameObject>(doorPath);
        if (doorGo == null)
        {
            Debug.Log("[BuildLinux] Creating LockedDoor prefab dynamically...");
            var go = new GameObject("LockedDoor");
            go.transform.localScale = new Vector3(3f, 0.5f, 1f);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square();
            sr.color = new Color(0.2f, 0.5f, 1f, 0.9f); // Blue door default
            sr.sortingOrder = 5;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);

            go.AddComponent<NetworkObject>();
            go.AddComponent<DoorLock>();

            PrefabUtility.SaveAsPrefabAsset(go, doorPath);
            Object.DestroyImmediate(go);
            
            doorGo = AssetDatabase.LoadAssetAtPath<GameObject>(doorPath);
        }

        const string pickupPath = "Assets/Game/Resources/KeyPickup.prefab";
        GameObject pickupGo = AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath);
        if (pickupGo == null)
        {
            Debug.Log("[BuildLinux] Creating KeyPickup prefab dynamically...");
            var go = new GameObject("KeyPickup");
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square();
            sr.color = new Color(0.2f, 0.5f, 1f); // Blue key default
            sr.sortingOrder = 10;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.6f;

            go.AddComponent<NetworkObject>();
            go.AddComponent<KeyPickup>();

            PrefabUtility.SaveAsPrefabAsset(go, pickupPath);
            Object.DestroyImmediate(go);
            
            pickupGo = AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath);
        }

        const string bossPath = "Assets/Game/Resources/Boss.prefab";
        GameObject bossGo = AssetDatabase.LoadAssetAtPath<GameObject>(bossPath);
        if (bossGo == null)
        {
            Debug.Log("[BuildLinux] Creating Boss prefab dynamically...");
            var go = new GameObject("Boss");
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square();
            sr.color = new Color(0.6f, 0.1f, 0.8f);
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

            PrefabUtility.SaveAsPrefabAsset(go, bossPath);
            Object.DestroyImmediate(go);
            
            bossGo = AssetDatabase.LoadAssetAtPath<GameObject>(bossPath);
        }

        const string itemPath = "Assets/Game/Resources/ItemPickup.prefab";
        GameObject itemGo = AssetDatabase.LoadAssetAtPath<GameObject>(itemPath);
        if (itemGo == null)
        {
            Debug.Log("[BuildLinux] Creating ItemPickup prefab dynamically...");
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

            PrefabUtility.SaveAsPrefabAsset(go, itemPath);
            Object.DestroyImmediate(go);
            
            itemGo = AssetDatabase.LoadAssetAtPath<GameObject>(itemPath);
        }

        // Add to DefaultPrefabObjects if missing
        var dpo = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(dpoPath);
        if (dpo != null)
        {
            if (prefabGo != null)
            {
                var nob = prefabGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding Projectile prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (enemyGo != null)
            {
                var nob = enemyGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding Enemy prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (rangedEnemyGo != null)
            {
                var nob = rangedEnemyGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding RangedEnemy prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (doorGo != null)
            {
                var nob = doorGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding LockedDoor prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (pickupGo != null)
            {
                var nob = pickupGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding KeyPickup prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (bossGo != null)
            {
                var nob = bossGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding Boss prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (itemGo != null)
            {
                var nob = itemGo.GetComponent<NetworkObject>();
                if (nob != null)
                {
                    bool contains = false;
                    for (int i = 0; i < dpo.GetObjectCount(); i++)
                    {
                        if (dpo.GetObject(true, i) == nob)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        Debug.Log("[BuildLinux] Adding ItemPickup prefab to DefaultPrefabObjects...");
                        dpo.AddObject(nob, true, true);
                        EditorUtility.SetDirty(dpo);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }
    }
}
