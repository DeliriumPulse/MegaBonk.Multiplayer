// File: LiteNetTransportRunner.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using LiteNetLib;

namespace Megabonk.Multiplayer.Transport
{
    /// <summary>
    /// Keeps the LiteNetLib NetManager alive across scene transitions and
    /// rebinds listener events whenever Unity swaps scenes or IL2CPP sheds delegates.
    /// </summary>
    public class LiteNetTransportRunner : MonoBehaviour
    {
        private static LiteNetTransportRunner _instance;

        public static LiteNetTransportRunner Ensure(NetManager net)
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("LiteNetTransportRunner");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            DontDestroyOnLoad(go);

            _instance = go.AddComponent<LiteNetTransportRunner>();
            _instance.Net = net;
            _instance._lastScene = SceneManager.GetActiveScene().name;

            return _instance;
        }

        public NetManager Net;

        private LiteNetTransport _transport;
        private EventBasedNetListener _listener;
        private bool _eventsBound;
        private string _lastScene;
        private float _rebindCooldown;

        private void Awake()
        {
            // Intentionally left blank; scene load handled via Harmony patch
        }

        private void OnDestroy()
        {
            try
            {
                if (_listener != null && _transport != null)
                {
                    _listener.ConnectionRequestEvent -= _transport.OnConnectionRequest;
                    _listener.PeerConnectedEvent      -= _transport.OnPeerConnected;
                    _listener.PeerDisconnectedEvent   -= _transport.OnPeerDisconnected;
                    _listener.NetworkReceiveEvent     -= _transport.OnNetworkReceive;
                }
            }
            catch { /* best-effort */ }

            try { Net?.Stop(); } catch { }
            _instance = null;
        }

        internal static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_instance != null)
            {
                _instance._lastScene = scene.name;
                _instance._eventsBound = false;   // force a rebind on next Update
            }
        }

        private void TryAcquireTransport()
        {
            if (_transport != null)
                return;

            // IMPORTANT: IL2CPP cannot marshal interface returns; use concrete property.
            var shim = Megabonk.Multiplayer.NetDriverShim.ShimInstance;
            _transport = shim?.TransportLite;

            if (_transport != null)
            {
                Net = _transport.GetInternalNet();
                _listener = _transport.GetListener();
                _eventsBound = false;
            }
        }

        private void EnsureSocketRunning()
        {
            if (Net == null)
                return;

            if (!Net.IsRunning)
            {
                try
                {
                    if (_transport.IsServer)
                        Net.Start(_transport.GetPort());
                    else
                        Net.Start();

                    Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogInfo(
                        "[LiteNetTransportRunner] NetManager socket restarted after scene reload.");
                }
                catch (Exception e)
                {
                    Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning(
                        $"[LiteNetTransportRunner] Socket restart failed: {e.Message}");
                }
            }
        }

        private void RebindEventsIfNeeded()
        {
            if (_listener == null || _transport == null)
                return;

            // IL2CPP sometimes drops delegates on scene load. Detach then reattach idempotently.
            if (_eventsBound && _rebindCooldown > Time.unscaledTime)
                return;

            try
            {
                // Detach to avoid duplicate handlers
                _listener.ConnectionRequestEvent -= _transport.OnConnectionRequest;
                _listener.PeerConnectedEvent      -= _transport.OnPeerConnected;
                _listener.PeerDisconnectedEvent   -= _transport.OnPeerDisconnected;
                _listener.NetworkReceiveEvent     -= _transport.OnNetworkReceive;
            }
            catch { /* best-effort */ }

            _listener.ConnectionRequestEvent += _transport.OnConnectionRequest;
            _listener.PeerConnectedEvent      += _transport.OnPeerConnected;
            _listener.PeerDisconnectedEvent   += _transport.OnPeerDisconnected;
            _listener.NetworkReceiveEvent     += _transport.OnNetworkReceive;

            _eventsBound = true;
            _rebindCooldown = Time.unscaledTime + 1.0f;

            Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogInfo(
                "[LiteNetTransportRunner] Listener events bound.");
        }

        private void ResyncPeersAfterRebind()
        {
            if (Net == null || _transport == null)
                return;

            try
            {
                var peers = Net.ConnectedPeerList;
                foreach (var peer in peers)
                {
                    _transport.EnsurePeerMapping(peer);
                }
            }
            catch (Exception e)
            {
                Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning(
                    $"[LiteNetTransportRunner] Peer re-register failed: {e.Message}");
            }
        }

        private void Update()
        {
            try
            {
                // Always ensure we have current references
                if (_transport == null || _listener == null || Net == null)
                {
                    TryAcquireTransport();
                }

                if (Net == null)
                    return;

                EnsureSocketRunning();

                // Force a rebind each time the scene changes or if we lost handlers
                if (!_eventsBound)
                {
                    RebindEventsIfNeeded();
                    ResyncPeersAfterRebind();
                }

                // Pump the socket
                Net.PollEvents();
            }
            catch (Exception e)
            {
                Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning(
                    $"[LiteNetTransportRunner] Poll error: {e.Message}");
            }
        }
    }
}
