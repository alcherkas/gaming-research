using System.IO;
using FishNet;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Connection front-end + Steam lifecycle.
//
// Menu (IMGUI, no UI assets): Single Player / Host / Join LAN / Join Steam.
// - Host starts a server on ALL Multipass transports (Tugboat for LAN + FishySteamworks for Steam)
//   and a local client over Tugboat; share the printed SteamID for remote Steam joins.
// - Join LAN connects over Tugboat to an IP (great for Mac-host <-> Deck-client testing without a
//   Steam account). Join Steam connects over FishySteamworks to a host SteamID.
// - On the Deck, if a "host_steamid.txt" sits next to the binary it auto-joins that host over Steam.
//
// Steam runs under appID 480 (steam_appid.txt). If Steam isn't available the LAN paths still work.
public class ConnectBootstrap : MonoBehaviour
{
    NetworkManager _nm;
    Multipass _mp;
    PlayerSpawner _spawner;

    bool _steamReady;
    bool _menu = true;
    int _selected;
    int _logSamples;
    float _nextLog;
    static readonly string[] MenuItems =
        { "Single Player", "Host (LAN + Steam)", "Join LAN", "Join Steam", "DOTS Stress Test" };
    string _localSteamId = "-";
    string _joinIp = "127.0.0.1";
    string _joinSteamId = "";

    void Awake()
    {
        try
        {
            if (SteamAPI.Init())
            {
                _steamReady = true;
                _localSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Steam] init unavailable (LAN still works): " + e.Message);
        }

        _nm = InstanceFinder.NetworkManager;
        if (_nm != null)
        {
            _mp = _nm.TransportManager.GetTransport<Multipass>();
            
            // If Steam is not initialized, remove FishySteamworks to prevent NullReferenceExceptions during updates
            if (_mp != null && !_steamReady)
            {
                var field = typeof(Multipass).GetField("_transports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var list = field.GetValue(_mp) as System.Collections.Generic.List<Transport>;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] != null && list[i].GetType().Name == "FishySteamworks")
                            {
                                Debug.Log("[ConnectBootstrap] Steam not initialized. Disabling and removing FishySteamworks from Multipass dynamically.");
                                list[i].enabled = false;
                                list.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            _spawner = _nm.GetComponent<PlayerSpawner>();
            var prefab = Resources.Load<NetworkObject>("Player");
            if (_spawner != null && prefab != null) _spawner.SetPlayerPrefab(prefab);
        }

        // Deck convenience: auto-join a host whose SteamID is provided next to the binary.
        string sidFile = Path.Combine(Application.dataPath, "..", "host_steamid.txt");
        if (_steamReady && File.Exists(sidFile))
        {
            _joinSteamId = File.ReadAllText(sidFile).Trim();
            if (_joinSteamId.Length > 0) JoinSteam();
        }

        // Headless test automation trigger
        bool isHeadlessTest = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-headlessTest") >= 0;
        if (isHeadlessTest)
        {
            Debug.Log("[ConnectBootstrap] Automated headless test detected. Scheduling Host mode...");
            Invoke(nameof(AutoHostTest), 0.5f);
        }
    }

    private void AutoHostTest()
    {
        Debug.Log("[ConnectBootstrap] Executing automated test Host...");
        Host();
    }

    void Update()
    {
        if (_steamReady) SteamAPI.RunCallbacks();

        // Log what each input backend sees for the first few seconds (after Steam Input settles),
        // so input issues are diagnosable from Player.log.
        if (_logSamples < 6 && Time.unscaledTime >= _nextLog)
        {
            _nextLog = Time.unscaledTime + 1f;
            _logSamples++;
            Debug.Log("[Input] " + InputBridge.Status());
        }

        if (_menu) MenuInput();
    }

    void MenuInput()
    {
        int nav = InputBridge.MenuNav();
        if (nav > 0) _selected = (_selected + 1) % MenuItems.Length;
        if (nav < 0) _selected = (_selected + MenuItems.Length - 1) % MenuItems.Length;
        if (InputBridge.Submit()) Activate(_selected);
    }

    void Activate(int i)
    {
        switch (i)
        {
            case 0: SinglePlayer(); break;
            case 1: Host(); break;
            case 2: JoinLan(); break;
            case 3: JoinSteam(); break;
            case 4: SceneManager.LoadScene("Stress"); break;
        }
    }

    void ClearLocalPlayers()
    {
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.GetComponent<NetworkObject>() == null)
                Destroy(pc.gameObject);
    }

    void SinglePlayer()
    {
        GameBootstrap.BuildPlayer(new Vector3(GameBootstrap.RoomA.x, GameBootstrap.RoomA.y, 0f),
                                  new Color(0.30f, 0.85f, 1f));
        if (EnemySpawner.Instance != null)
            EnemySpawner.Instance.SpawnOfflineEnemies();
        _menu = false;
    }

    void Host()
    {
        if (_nm == null) return;
        ClearLocalPlayers();
        InstanceFinder.ServerManager.StartConnection();
        _mp.SetClientTransport<Tugboat>();
        InstanceFinder.ClientManager.StartConnection("127.0.0.1");
        _menu = false;
    }

    void JoinLan()
    {
        if (_nm == null) return;
        ClearLocalPlayers();
        _mp.SetClientTransport<Tugboat>();
        InstanceFinder.ClientManager.StartConnection(_joinIp);
        _menu = false;
    }

    void JoinSteam()
    {
        if (_nm == null || !_steamReady || _joinSteamId.Length == 0) return;
        ClearLocalPlayers();
        _mp.SetClientTransport<FishySteamworks.FishySteamworks>();
        InstanceFinder.ClientManager.StartConnection(_joinSteamId);
        _menu = false;
    }

    void OnGUI()
    {
        if (!_menu) return;

        float w = 440f, h = 360f;
        var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUILayout.BeginArea(rect, GUI.skin.box);

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.032f),
            fontStyle = FontStyle.Bold,
        };
        title.normal.textColor = Color.white;
        GUILayout.Label("2D Co-op", title);
        GUILayout.Label("Move: D-pad / Left-stick / WASD    Select: A / Enter");
        GUILayout.Label("Steam: " + (_steamReady ? _localSteamId : "off"));
        GUILayout.Label("input: " + InputBridge.Status());
        GUILayout.Space(8);

        var btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * 0.026f) };
        for (int i = 0; i < MenuItems.Length; i++)
        {
            string label = (i == _selected ? "\u25B6 " : "    ") + MenuItems[i];
            var prev = GUI.backgroundColor;
            if (i == _selected) GUI.backgroundColor = new Color(0.30f, 0.70f, 1f);
            if (GUILayout.Button(label, btn)) { _selected = i; Activate(i); }
            GUI.backgroundColor = prev;
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Label("LAN IP:", GUILayout.Width(80));
        _joinIp = GUILayout.TextField(_joinIp);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("SteamID:", GUILayout.Width(80));
        _joinSteamId = GUILayout.TextField(_joinSteamId);
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    void OnApplicationQuit()
    {
        // Graceful teardown on EVERY exit path (also the native Steam exit, via the signal handler).
        if (InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
            InstanceFinder.ServerManager.StopConnection(true);
        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)
            InstanceFinder.ClientManager.StopConnection();
        if (_steamReady) SteamAPI.Shutdown();
    }
}
