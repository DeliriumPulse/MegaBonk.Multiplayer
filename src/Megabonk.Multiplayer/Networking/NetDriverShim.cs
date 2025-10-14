// File: NetDriverShim.cs
using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.Transport;

namespace Megabonk.Multiplayer
{
    public class NetDriverShim : MonoBehaviour
    {
        public static NetDriverShim ShimInstance { get; private set; }

        private NetDriverCore _core;
        private ITransport _transport;
		public NetDriverCore DriverCore => _core; 
		
        // IMPORTANT: do NOT expose ITransport over IL2CPP. Expose concrete transport instead.
        public LiteNetTransport TransportLite { get; private set; }

        private string _transportName = "lite";
        private bool _showOverlay = true;

        private bool _isHost;
        private string _hostAddress;
        private int _port;
        private bool _autoReconnect;
        private bool _enableTypeDump;
        private ulong _hostSteamId;

        // Register types so AddComponent works under IL2CPP
        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            void Register(Type type)
            {
                try { ClassInjector.RegisterTypeInIl2Cpp(type); }
                catch (ArgumentException) { /* already injected */ }
            }

            Register(typeof(NetDriverShim));
            Register(typeof(ShimCoroutineRunner));
            Register(typeof(Megabonk.Multiplayer.Transport.LiteNetTransportRunner));
            Register(typeof(InputDriver));
            Register(typeof(HostPawnController));
            Register(typeof(CameraFollower));
            Register(typeof(AnimatorSyncSource));
            Register(typeof(RemoteAvatar));
            Register(typeof(NetDriverCoreUpdater));

            _registered = true;
        }

        private static bool _registered;

        private void Awake()
        {
            ShimInstance = this;
        }

        private void Start()
        {
            // Select transport
            if (_transport == null)
            {
                switch (_transportName)
                {
                    case "lite":
                    default:
                        _transport = new LiteNetTransport();
                        break;
                }
            }

            // Cache concrete transport to avoid IL2CPP interface-return shenanigans
            TransportLite = _transport as LiteNetTransport;

            // Core
            _core = new NetDriverCore(
                _transport,
                _isHost,
                _hostAddress,
                _port,
                _autoReconnect,
                _enableTypeDump,
                _transportName,
                _hostSteamId);

            _core.GetActiveSceneName = () => SceneManager.GetActiveScene().name;
            _core.RequestSceneLoad = (scene, seed) => LoadSceneNextFrame(scene, seed);

            _core.Initialize();
            MultiplayerPlugin.Driver = _core;
        }
        public void Initialize(
            bool isHost,
            string hostAddress,
            int port,
            bool autoReconnect,
            bool enableTypeDump,
            string transport,
            ulong hostSteamId)
        {
            _isHost = isHost;
            _hostAddress = string.IsNullOrWhiteSpace(hostAddress) ? "127.0.0.1" : hostAddress;
            _port = port;
            _autoReconnect = autoReconnect;
            _enableTypeDump = enableTypeDump;
            _transportName = string.IsNullOrWhiteSpace(transport) ? "lite" : transport.ToLowerInvariant();
            _hostSteamId = hostSteamId;
        }

        // Replace instance method with static handler
        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                var instance = NetDriverShim.ShimInstance;
                if (instance == null || instance._core == null)
                {
                    MultiplayerPlugin.LogS.LogWarning("[Shim] OnSceneLoaded called after core disposed.");
                    return;
                }

                // Give the transport a quick poke immediately after load
                try
                {
                    instance.TransportLite?.Poll();
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogWarning($"[Shim] Post-load poll failed: {e.Message}");
                }

                MultiplayerPlugin.LogS.LogInfo($"[Shim] Scene loaded: {scene.name}");
                instance._core?.OnSceneLoaded(scene.name);
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS.LogError($"[Shim] Scene load handler failed: {ex}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_core != null)
                {
                    _core.Dispose(false);
                    MultiplayerPlugin.LogS.LogInfo("[Shim] Clean shutdown completed.");
                }
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS.LogError($"[Shim] OnDestroy failed: {ex}");
            }
        }

        private void Update()
        {
            // Core tick; network polling is handled by the runner
            _core?.Tick();
        }

        /// <summary>
        /// Called on both host and client before scene generation.
        /// Ensures RNG state is synchronized before loading begins.
        /// </summary>
        public void LoadSceneNextFrame(string scene, int seed)
        {
            if (_core == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[Shim] Ignored scene load request – NetDriverCore missing.");
                return;
            }

            var runner = new GameObject("ShimCoroutineRunner").AddComponent<ShimCoroutineRunner>();
            DontDestroyOnLoad(runner);
            runner.Begin(scene, seed);
        }

        // Minimal helper that lives long enough to run the delayed scene load, then destroys itself.
        public class ShimCoroutineRunner : MonoBehaviour
        {
            private float _timer;
            private bool _triggered;
            private string _scene;
            private int _seed;

            public void Begin(string scene, int seed)
            {
                _scene = scene;
                _seed = seed;
                _timer = 0.10f;
                _triggered = false;
            }

            private void Update()
            {
                if (_triggered) return;

                _timer -= Time.unscaledDeltaTime;
                if (_timer > 0f) return;

                _triggered = true;

                try
                {
                    // Pass the seed to the core’s global hook so all patches read the same source
                    NetDriverCore.GlobalSeed = _seed;
                    MultiplayerPlugin.LogS.LogInfo($"[Shim] Global coop_seed set = {_seed}");

                    UnityEngine.Random.InitState(_seed);
                    MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Pre-seeded Unity RNG → {_seed} before scene load: {_scene}");

                    // Keep the socket breathing before the load
                    NetDriverShim.ShimInstance?.TransportLite?.Poll();

                    SceneManager.LoadScene(_scene, LoadSceneMode.Single);
                    MultiplayerPlugin.LogS.LogInfo($"[Shim] Scene load requested (Seed={_seed}, Scene={_scene})");
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogError($"[ShimCoroutineRunner] Exception in Update: {e}");
                }

                Destroy(gameObject, 0.1f);
            }
        }

        private void OnGUI()
        {
            GUI.depth = 0;
            if (_core == null) return;

            var lines = _core.OverlayLines();
            float h = 30f + lines.Length * 22f + (_core.IsHost ? 80f : 40f);
            var rect = new Rect(12, 12, 360, h);
            GUI.BeginGroup(rect, GUI.skin.box);

            float y = 10f;
            foreach (var ln in lines)
            {
                GUI.Label(new Rect(10, y, rect.width - 20, 20), ln);
                y += 22f;
            }

            if (_core.IsHost)
            {
                bool canStart = _core.CanHostStartRun();
                GUI.enabled = canStart;
                if (GUI.Button(new Rect(10, y, 220, 24), "Start Run (GeneratedMap)"))
                {
                    _core.HostStartRun("GeneratedMap");
                }
                GUI.enabled = true;
                y += 28f;
            }
            else
            {
                y += 8f;
            }

            string status = _core.IsHost
                ? _core.ReadyStatus()
                : (_core.IsLocalReady ? "Character locked in." : "Select a character to get ready.");
            if (!string.IsNullOrEmpty(status))
            {
                GUI.Label(new Rect(10, y, rect.width - 20, 20), status);
            }

            GUI.EndGroup();
        }
    }
}
