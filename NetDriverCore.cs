using System;
using System.Collections.Generic;
using Assets.Scripts._Data;
using Assets.Scripts.Actors.Player;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.Transport;

namespace Megabonk.Multiplayer
{
    [Serializable]
    public struct AppearanceInfo
    {
        public string RootPath;
        public string PrefabName;
        public string MeshName;
        public string[] MaterialNames;
        public string CharacterClass;
        public int CharacterId;
        public string SkinName;
    }

    public class NetDriverCore
    {
        private readonly ITransport _transport;
        private readonly bool _isHost;
        private readonly string _hostAddress;
        private readonly int _port;
        private readonly bool _autoReconnect;
        private readonly bool _enableTypeDump;
        private readonly string _transportName;
        private readonly ulong _hostSteamId;

        private ulong _localPeerId;
        private readonly Queue<Action> _mainThreadActions = new();
        private readonly Dictionary<ulong, RemoteAvatar> _remoteAvatars = new();
        private readonly Dictionary<ulong, AppearanceInfo> _appearanceByPeer = new();
        private readonly Dictionary<ulong, AppearanceInfo> _appliedAppearanceByPeer = new();
        private readonly Dictionary<ulong, string> _appearancePayloadByPeer = new();
        private readonly HashSet<ulong> _connectedPeers = new();
        private readonly HashSet<ulong> _readyPeers = new();
        private readonly Dictionary<string, GameObject> _templateCache = new();
        private AppearanceInfo? _pendingAppearance;
        private string _pendingAppearanceSerialized;

        public static int GlobalSeed = 0;

        public Func<string> GetActiveSceneName;
        public Action<string, int> RequestSceneLoad;

        public bool IsHost => _isHost;
        public bool IsLocalReady => _readyPeers.Contains(_localPeerId);

        public NetDriverCore(
            ITransport transport,
            bool isHost,
            string hostAddress,
            int port,
            bool autoReconnect,
            bool enableTypeDump,
            string transportName,
            ulong hostSteamId)
        {
            _transport = transport;
            _isHost = isHost;
            _hostAddress = hostAddress;
            _port = port;
            _autoReconnect = autoReconnect;
            _enableTypeDump = enableTypeDump;
            _transportName = transportName;
            _hostSteamId = hostSteamId;
            ModelRegistry.TemplateRegistered += OnTemplateRegistered;
        }

        // --------------------------------------------------------------------
        // Initialization & main tick
        // --------------------------------------------------------------------
        public void Initialize()
        {
            if (_isHost)
                _transport.StartHost(_port, "megabonk_coop");
            else
                _transport.StartClient(_hostAddress, _port, "megabonk_coop", _hostSteamId);

            _transport.PeerConnected += OnPeerConnected;
            _transport.PeerDisconnected += OnPeerDisconnected;
            _transport.DataReceived += OnDataReceived;
        }

        public void Tick()
        {
            _transport.Poll();

            while (_mainThreadActions.Count > 0)
            {
                try
                {
                    _mainThreadActions.Dequeue().Invoke();
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogWarning($"[NetDriver] MainThread action failed: {e.Message}");
                }
            }
        }

        // --------------------------------------------------------------------
        // Peer events
        // --------------------------------------------------------------------
        private void OnPeerConnected(ulong peerId)
        {
            MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Peer connected: {peerId}");

            if (_isHost && peerId != _localPeerId)
            {
                _connectedPeers.Add(peerId);
                _readyPeers.Remove(peerId);
            }

            _mainThreadActions.Enqueue(() =>
            {
                if (peerId == _localPeerId && (_isHost || _localPeerId != 0))
                    return;

                var avatar = EnsureAvatar(peerId);
                if (avatar != null)
                    MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Spawned remote avatar for peer {peerId}");

                if (_isHost && peerId != _localPeerId)
                {
                    foreach (var kv in _appearancePayloadByPeer)
                    {
                        var payload = kv.Value;
                        if (string.IsNullOrWhiteSpace(payload))
                            continue;

                        var writer = new NetDataWriter();
                        writer.Put((byte)0x05);
                        writer.Put(kv.Key);
                        writer.Put(payload);

                        _transport.SendTo(peerId, writer.CopyData(), true);
                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Sent cached appearance for {kv.Key} to peer {peerId}: {payload}");
                    }
                }
            });
        }

        private void OnPeerDisconnected(ulong peerId)
        {
            MultiplayerPlugin.LogS.LogInfo($"[NetDriver] Peer disconnected: {peerId}");

            if (_isHost && peerId != _localPeerId)
            {
                _connectedPeers.Remove(peerId);
                _readyPeers.Remove(peerId);
            }

            _mainThreadActions.Enqueue(() =>
            {
                if (_remoteAvatars.TryGetValue(peerId, out var avatar) && avatar != null)
                    UnityEngine.Object.Destroy(avatar.gameObject);

                _remoteAvatars.Remove(peerId);
                _appearanceByPeer.Remove(peerId);
                _appliedAppearanceByPeer.Remove(peerId);
                _appearancePayloadByPeer.Remove(peerId);
                _readyPeers.Remove(peerId);
            });
        }

        // --------------------------------------------------------------------
        // Network message dispatch
        // --------------------------------------------------------------------
        private void OnDataReceived(ulong peerId, ArraySegment<byte> data, bool reliable)
        {
            try
            {
                var reader = new NetDataReader(data.Array, data.Offset, data.Count);
                byte tag = reader.GetByte();

                switch (tag)
                {
                    case 0x02: // MSG_ASSIGN_ID
                    {
                        ulong newId = reader.GetULong();
                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Received ID assignment -> {newId}");
                        _localPeerId = newId;

                        var writer = new NetDataWriter();
                        writer.Put((byte)0x06); // MSG_ACK_ID
                        writer.Put(_localPeerId);
                        (_transport as LiteNetTransport)?.SendToHost(writer, true);

                        FlushPendingAppearance();
                        break;
                    }

                    case 0x06: // MSG_ACK_ID
                    {
                        ulong clientId = reader.GetULong();
                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Host received ACK from client -> ID={clientId}");

                        if (_isHost)
                        {
                            var transport = _transport as LiteNetTransport;
                            var field = transport?.GetType()
                                .GetField("_peerToId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var map = field?.GetValue(transport) as Dictionary<NetPeer, ulong>;
                            if (map != null)
                            {
                                foreach (var kvp in map)
                                {
                                    if (kvp.Value == 0)
                                    {
                                        map[kvp.Key] = clientId;
                                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Updated peer mapping -> NetPeer now maps to ID={clientId}");
                                        break;
                                    }
                                }
                            }
                        }

                        break;
                    }

                    case 0x03: // MSG_START_RUN
                        HandleStartRun(peerId, reader);
                        break;

                    case 0x04: // MSG_PAWN_TRANSFORM
                    {
                        ulong senderId = reader.GetULong();
                        float x = reader.GetFloat();
                        float y = reader.GetFloat();
                        float z = reader.GetFloat();
                        float qx = reader.GetFloat();
                        float qy = reader.GetFloat();
                        float qz = reader.GetFloat();
                        float qw = reader.GetFloat();

                        Vector3 pos = new Vector3(x, y, z);
                        Quaternion rot = new Quaternion(qx, qy, qz, qw);
                        rot = FlattenRotation(rot);

                        _mainThreadActions.Enqueue(() =>
                        {
                            if (senderId == _localPeerId)
                                return;

                            var avatar = EnsureAvatar(senderId);
                            if (avatar == null)
                                return;

                            avatar.ApplyPose(pos, rot);

                            var go = avatar.gameObject;
                            var renderer = go ? go.GetComponent<Renderer>() : null;
                            if (renderer != null && !renderer.enabled)
                                renderer.enabled = true;
                        });

                        if (_isHost)
                        {
                            foreach (var kvp in _remoteAvatars.Keys)
                            {
                                if (kvp == senderId)
                                    continue;
                                _transport.SendTo(kvp, data.ToArray(), false);
                            }
                        }

                        break;
                    }

                    case 0x05: // MSG_APPEARANCE
                    {
                        ulong senderId = reader.GetULong();
                        string json = reader.GetString();
                        _mainThreadActions.Enqueue(() =>
                        {
                            if (AppearanceSerializer.TryDeserialize(json, out var appearance))
                            {
                                _appearanceByPeer[senderId] = appearance;
                                _appearancePayloadByPeer[senderId] = json;
                                UpdatePeerReadyState(senderId, appearance);
                                MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Appearance update from {senderId}: {json}");
                                if (_remoteAvatars.ContainsKey(senderId))
                                    UpgradeAvatarAppearance(senderId);
                            }
                            else
                            {
                                MultiplayerPlugin.LogS.LogWarning($"[NetDriverCore] Failed to parse appearance payload from {senderId}: {json}");
                            }
                        });
                        // Optionally: forward to other peers if host
                        if (_isHost)
                        {
                            foreach (var target in _remoteAvatars.Keys)
                            {
                                if (target == senderId) continue;
                                var forward = new NetDataWriter();
                                forward.Put((byte)0x05);
                                forward.Put(senderId);
                                forward.Put(json);
                                _transport.SendTo(target, forward.CopyData(), true);
                            }
                        }
                        break;
                    }

                    default:
                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Unknown packet tag={tag} from {peerId}, length={data.Count}");
                        break;
                }

                MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Received data from {peerId}, bytes={data.Count}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogWarning($"[NetDriverCore] OnDataReceived error: {e.Message}");
            }
        }

        private void HandleStartRun(ulong peerId, NetDataReader reader)
        {
            int seed = reader.GetInt();
            string scene = reader.GetString();
            MultiplayerPlugin.LogS.LogInfo($"[NetDriver] START_RUN seed={seed} scene={scene}");

            if (_isHost)
                return;

            CoopSeedStorage.Value = seed;
            MultiplayerPlugin.LogS.LogInfo($"[NetDriver] Stored coop_seed = {seed}");

            _mainThreadActions.Enqueue(() =>
            {
                if (NetDriverShim.ShimInstance != null)
                    NetDriverShim.ShimInstance.LoadSceneNextFrame(scene, seed);
                else
                    SceneDelayHelper.Run(scene);
            });
        }

        // --------------------------------------------------------------------
        // Scene & spawn
        // --------------------------------------------------------------------
        public void OnSceneLoaded(string sceneName)
        {
            if (sceneName == "MainMenu")
            {
                MultiplayerPlugin.LogS.LogInfo("[NetDriver] Ignoring MainMenu spawn.");
                SkinPrefabRegistry.CacheMenuRoster();
                return;
            }

            MultiplayerPlugin.LogS.LogInfo($"[NetDriver] Scene loaded: {sceneName}");

            if (_isHost)
            {
                var hostPawn = new GameObject("HostPawn");
                hostPawn.AddComponent<HostPawnController>();
                MultiplayerPlugin.LogS.LogInfo("[NetDriver] Spawned host pawn.");

                foreach (var peerId in new List<ulong>(_remoteAvatars.Keys))
                    EnsureAvatar(peerId);

                _mainThreadActions.Enqueue(() =>
                {
                    var shim = NetDriverShim.ShimInstance;
                    if (shim != null)
                        shim.Invoke(nameof(SendInitialTransform), 1f);
                    else
                        SendInitialTransform();
                });
            }
            else
            {
                var clientPawn = new GameObject("ClientPawn");
                clientPawn.AddComponent<InputDriver>();
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(clientPawn.transform);
                capsule.transform.localPosition = Vector3.zero;
                clientPawn.transform.position = new Vector3(3f, 1f, 0f);

                MultiplayerPlugin.LogS.LogInfo("[NetDriver] Spawned local InputDriver for client.");

                var hostAvatar = EnsureAvatar(0);
                if (hostAvatar != null)
                {
                    hostAvatar.transform.position = new Vector3(0f, 1f, 0f);
                    MultiplayerPlugin.LogS.LogInfo("[NetDriver] Spawned remote host avatar on client.");
                }
            }

            foreach (var kv in _remoteAvatars)
            {
                var avatar = kv.Value;
                var go = avatar ? avatar.gameObject : null;
                if (go == null)
                    continue;

                var renderer = go.GetComponent<Renderer>();
                bool visible = renderer != null && renderer.enabled;
                MultiplayerPlugin.LogS.LogInfo($"[NetDriver] Avatar check -> {go.name}, visible={visible}, pos={go.transform.position}");
            }

            foreach (var peerId in new List<ulong>(_appearanceByPeer.Keys))
                UpgradeAvatarAppearance(peerId);
        }

        private void SendInitialTransform()
        {
            try
            {
                var hostPawn = GameObject.Find("HostPawn");
                if (hostPawn != null)
                {
                    var model = GameObject.Find("PlayerModel")?.transform ?? hostPawn.transform;
                    var pos = model.position;
                    var rot = model.rotation;
                    SendPawnTransform(pos, rot);
                    MultiplayerPlugin.LogS.LogInfo("[NetDriver] Initial host transform broadcast sent.");
                }
                else
                {
                    MultiplayerPlugin.LogS.LogWarning("[NetDriver] Could not find HostPawn for initial broadcast.");
                }
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogWarning($"[NetDriver] SendInitialTransform failed: {e.Message}");
            }
        }

        // --------------------------------------------------------------------
        // Transform broadcast
        // --------------------------------------------------------------------
        public void SendPawnTransform(Vector3 pos, Quaternion rot)
        {
            if (_transport == null)
                return;

            if (!_isHost && _localPeerId == 0)
                return;

            var writer = new NetDataWriter();
            writer.Put((byte)0x04); // MSG_PAWN_TRANSFORM
            writer.Put(_localPeerId);                    // who is this?
            writer.Put(pos.x);
            writer.Put(pos.y);
            writer.Put(pos.z);
            writer.Put(rot.x);
            writer.Put(rot.y);
            writer.Put(rot.z);
            writer.Put(rot.w);

            _transport.SendToAll(writer.CopyData(), false);
        }

        public void SendAppearanceJson(string json)
        {
            if (_transport == null || string.IsNullOrWhiteSpace(json))
                return;

            if (!AppearanceSerializer.TryDeserialize(json, out var appearance))
            {
                MultiplayerPlugin.LogS?.LogWarning($"[NetDriverCore] Ignoring malformed appearance payload: {json}");
                return;
            }

            if (appearance.MaterialNames == null)
                appearance.MaterialNames = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(appearance.MeshName) && appearance.CharacterId < 0)
            {
                MultiplayerPlugin.LogS?.LogWarning("[NetDriverCore] Ignoring appearance broadcast with empty mesh name.");
                return;
            }

            UpdatePeerReadyState(_localPeerId, appearance);
            if (_localPeerId == 0 && !_isHost)
            {
                _pendingAppearance = appearance;
                _pendingAppearanceSerialized = json;
                MultiplayerPlugin.LogS?.LogInfo("[NetDriverCore] Queued appearance broadcast until ID assignment.");
                return;
            }

            BroadcastAppearance(appearance, json);
        }

        private void BroadcastAppearance(in AppearanceInfo appearance, string serialized)
        {
            if (_transport == null)
                return;

            var payload = !string.IsNullOrEmpty(serialized) ? serialized : AppearanceSerializer.Serialize(appearance);

            _appearanceByPeer[_localPeerId] = appearance;
            _appearancePayloadByPeer[_localPeerId] = payload;
            UpdatePeerReadyState(_localPeerId, appearance);

            var writer = new NetDataWriter();
            writer.Put((byte)0x05); // MSG_APPEARANCE
            writer.Put(_localPeerId);
            writer.Put(payload);

            _transport.SendToAll(writer.CopyData(), true);
            var prefabLabel = string.IsNullOrWhiteSpace(appearance.PrefabName) ? "<none>" : appearance.PrefabName;
            MultiplayerPlugin.LogS?.LogInfo($"[NetDriverCore] Appearance broadcast: prefab={prefabLabel}, mesh={appearance.MeshName} (peer {_localPeerId})");
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------
        private static Quaternion FlattenRotation(Quaternion rot)
        {
            var forward = rot * Vector3.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 1e-6f)
            {
                // fallback: use right vector if forward is vertical
                forward = rot * Vector3.right;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 1e-6f)
                return Quaternion.identity;

            forward.Normalize();
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        private RemoteAvatar EnsureAvatar(ulong peerId)
        {
            if (_remoteAvatars.TryGetValue(peerId, out var existing) && existing != null)
            {
                var existingMeshes = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(existing.transform, true);
                bool hasVisual = existingMeshes != null && existingMeshes.Length > 0 && existingMeshes[0] != null && existingMeshes[0].enabled;
                if (!hasVisual)
                {
                    var meshes = Il2CppComponentUtil.GetComponentsInChildrenCompat<MeshRenderer>(existing.transform, true);
                    hasVisual = meshes != null && meshes.Length > 0 && meshes[0] != null && meshes[0].enabled;
                }
                if (!hasVisual)
                {
                    var currentPos = existing.transform.position;
                    var currentRot = existing.transform.rotation;
                    var upgradedGo = TryCreateAvatarFromAppearance(peerId, currentPos, currentRot);
                    if (upgradedGo != null)
                    {
                        UnityEngine.Object.Destroy(existing.gameObject);
                        var upgraded = upgradedGo.GetComponent<RemoteAvatar>() ?? upgradedGo.AddComponent<RemoteAvatar>();
                        upgraded.ApplyPose(currentPos, currentRot);
                        _remoteAvatars[peerId] = upgraded;
                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Upgraded avatar for peer {peerId} to character {DescribeAppearanceCharacter(peerId)}");
                        return upgraded;
                    }
                }

                return existing;
            }

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            var host = GameObject.FindFirstObjectByType<HostPawnController>();
            if (host != null)
            {
                spawnPos = host.transform.position + Vector3.right * 2f;
                spawnRot = host.transform.rotation;
            }
            else
            {
                var input = GameObject.FindFirstObjectByType<InputDriver>();
                if (input != null)
                {
                    spawnPos = input.transform.position + Vector3.right * 2f;
                    spawnRot = input.transform.rotation;
                }
            }

            GameObject go = TryCreateAvatarFromAppearance(peerId, spawnPos, spawnRot);
            if (go == null)
            {
                var templateGo = ResolveTemplateForPeer(peerId);
                if (templateGo != null)
                    go = TryCreateAvatarFromTemplate(templateGo, peerId, spawnPos, spawnRot);
            }

            if (go == null)
                go = CreateCapsuleAvatar(peerId, spawnPos, spawnRot);

            var avatar = go.GetComponent<RemoteAvatar>() ?? go.AddComponent<RemoteAvatar>();
            avatar.ApplyPose(spawnPos, spawnRot);

            _remoteAvatars[peerId] = avatar;
            MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] EnsureAvatar -> {go.name} for peer {peerId} at {spawnPos}");
            return avatar;
        }

        private GameObject CreateCapsuleAvatar(ulong peerId, Vector3 spawnPos, Quaternion spawnRot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"RemoteAvatar_{peerId}";
            go.transform.SetPositionAndRotation(spawnPos, spawnRot);
            go.transform.localScale = new Vector3(1f, 2f, 1f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var hue = (peerId % 997) / 997f;
                renderer.material.color = Color.HSVToRGB(hue, 0.7f, 0.9f);
                renderer.enabled = true;
            }

            return go;
        }

        private GameObject TryCreateAvatarFromTemplate(GameObject templateGo, ulong peerId, Vector3 spawnPos, Quaternion spawnRot)
        {
            if (!templateGo)
                return null;

            GameObject root = null;
            try
            {
                root = new GameObject($"RemoteAvatar_{peerId}");
                root.transform.SetPositionAndRotation(spawnPos, spawnRot);

                var instance = UnityEngine.Object.Instantiate(templateGo, root.transform, false);
                instance.name = templateGo.name;
                SkinPrefabRegistry.EnableRenderStack(instance);

                foreach (var col in Il2CppComponentUtil.GetComponentsInChildrenCompat<Collider>(root, true))
                    UnityEngine.Object.Destroy(col);

                foreach (var rb in Il2CppComponentUtil.GetComponentsInChildrenCompat<Rigidbody>(root, true))
                    UnityEngine.Object.Destroy(rb);

                MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Cloned template '{templateGo.name}' for peer {peerId}");
                return root;
            }
            catch (Exception ex)
            {
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                MultiplayerPlugin.LogS.LogWarning($"[NetDriverCore] Failed to clone player model for peer {peerId}: {ex.Message}");
                return null;
            }
        }

        private GameObject TryCreateAvatarFromAppearance(ulong peerId, Vector3 spawnPos, Quaternion spawnRot)
        {
            if (_appearanceByPeer.TryGetValue(peerId, out var appearance))
            {
                if (appearance.CharacterId >= 0)
                {
                    var character = (ECharacter)appearance.CharacterId;
                    if (SkinPrefabRegistry.TryCreateRemoteAvatar(character, appearance.SkinName, spawnPos, spawnRot, peerId, out var avatarRoot, out var renderer))
                    {
                        var remote = avatarRoot.GetComponent<RemoteAvatar>() ?? avatarRoot.AddComponent<RemoteAvatar>();
                        remote.ApplyPose(spawnPos, spawnRot);
                        _appliedAppearanceByPeer[peerId] = appearance;
                        MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Created remote avatar for peer {peerId} -> {character} ({appearance.SkinName})");
                        return avatarRoot;
                    }
                }

                var templateGo = ResolveTemplateForAppearance(appearance);
                if (templateGo != null)
                {
                    var templated = TryCreateAvatarFromTemplate(templateGo, peerId, spawnPos, spawnRot);
                    if (templated != null)
                        _appliedAppearanceByPeer[peerId] = appearance;
                    return templated;
                }
            }

            var fallbackTemplate = ResolveTemplateForPeer(peerId);
            if (fallbackTemplate != null)
            {
                var templated = TryCreateAvatarFromTemplate(fallbackTemplate, peerId, spawnPos, spawnRot);
                if (templated != null && _appearanceByPeer.TryGetValue(peerId, out var appliedAppearance))
                    _appliedAppearanceByPeer[peerId] = appliedAppearance;
                return templated;
            }

            _appliedAppearanceByPeer.Remove(peerId);
            return null;
        }

        private string DescribeAppearanceCharacter(ulong peerId)
        {
            if (_appearanceByPeer.TryGetValue(peerId, out var info) && info.CharacterId >= 0)
                return ((ECharacter)info.CharacterId).ToString();
            return "<unknown>";
        }

        // --------------------------------------------------------------------
        // Host utilities
        // --------------------------------------------------------------------
        public void HostStartRun(string scene)
        {
            if (!_isHost)
                return;

            if (!CanHostStartRun())
            {
                MultiplayerPlugin.LogS.LogWarning("[NetDriver] Cannot start run yet – waiting for players to choose characters.");
                return;
            }

            var writer = new NetDataWriter();
            writer.Put((byte)0x03);
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            writer.Put(seed);
            writer.Put(scene);

            _transport.SendToAll(writer.CopyData(), true);
            MultiplayerPlugin.LogS.LogInfo($"[NetDriver] START_RUN broadcast. Seed={seed} Scene={scene}");

            CoopSeedStorage.Value = seed;
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        // --------------------------------------------------------------------
        // Cleanup
        // --------------------------------------------------------------------
        public void Dispose(bool full = true)
        {
            try
            {
                if (_transport != null)
                {
                    _transport.PeerConnected -= OnPeerConnected;
                    _transport.PeerDisconnected -= OnPeerDisconnected;
                    _transport.DataReceived -= OnDataReceived;
                }
                ModelRegistry.TemplateRegistered -= OnTemplateRegistered;
                MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Disposing network core... (full={full})");
                if (full)
                {
                    var shutdown = _transport?.GetType().GetMethod("Shutdown");
                    shutdown?.Invoke(_transport, null);
                }
                MultiplayerPlugin.LogS.LogInfo("[NetDriverCore] Dispose complete.");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[NetDriverCore] Dispose() exception: {e}");
            }
        }

        // --------------------------------------------------------------------
        // Overlay info for debug HUD
        // --------------------------------------------------------------------
        public string[] OverlayLines()
        {
            var lines = new List<string>(6)
            {
                $"Megabonk Multiplayer v{MultiplayerPlugin.Version} [{(_isHost ? "Host" : "Client")}]",
                $"Transport: {_transportName}",
                $"Scene: {GetActiveSceneName?.Invoke() ?? "???"}",
                $"Peers: {_transport?.ConnectedCount ?? 0}"
            };

            if (_isHost)
                lines.Add($"Ready: {CountReadyPeers()}/{CountRequiredPeers()}");
            else if (_localPeerId != 0)
                lines.Add($"Local ready: {(_readyPeers.Contains(_localPeerId) ? "yes" : "no")}");

            return lines.ToArray();
        }

        // --------------------------------------------------------------------
        // Persistent updater attach (public MonoBehaviour, registered in bootstrap)
        // --------------------------------------------------------------------
        public static void AttachPersistentUpdater()
        {
            var go = new GameObject("NetDriverCoreUpdater");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<NetDriverCoreUpdater>();
        }

        private GameObject ResolveTemplateForPeer(ulong peerId)
        {
            if (_appearanceByPeer.TryGetValue(peerId, out var appearance))
            {
                var template = ResolveTemplateForAppearance(appearance);
                if (template != null)
                    return template;
            }

            if (peerId == _localPeerId)
            {
                var localVisual = PlayerModelLocator.LastResolvedVisual;
                if (TemplateHasRenderable(localVisual))
                    return localVisual.gameObject;
            }

            return null;
        }

        private GameObject GetTemplateForMesh(string meshName)
        {
            if (string.IsNullOrWhiteSpace(meshName))
                return null;

            var cacheKey = $"mesh:{meshName}";
            if (_templateCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            if (ModelRegistry.TryGetByMesh(meshName, out var template) && template.IsAlive && TemplateHasRenderable(template.Root.transform))
            {
                _templateCache[cacheKey] = template.Root;
                return template.Root;
            }

            return null;
        }

        private GameObject ResolveTemplateForAppearance(in AppearanceInfo appearance)
        {
            if (appearance.CharacterId >= 0)
            {
                var character = (ECharacter)appearance.CharacterId;
                if (SkinPrefabRegistry.TryGetTemplate(character, appearance.SkinName, out var fromSkin) && TemplateHasRenderable(fromSkin.transform))
                    return fromSkin;
            }

            if (!string.IsNullOrWhiteSpace(appearance.RootPath))
            {
                var fromPath = GetTemplateForPath(appearance.RootPath);
                if (fromPath != null && TemplateHasRenderable(fromPath.transform))
                    return fromPath;
            }

            if (!string.IsNullOrWhiteSpace(appearance.PrefabName))
            {
                var fromPrefab = GetTemplateForPrefab(appearance.PrefabName, appearance.MeshName);
                if (fromPrefab != null)
                    return fromPrefab;
            }

            if (!string.IsNullOrWhiteSpace(appearance.MeshName))
            {
                var fromMesh = GetTemplateForMesh(appearance.MeshName);
                if (fromMesh != null)
                    return fromMesh;
            }

            return null;
        }

        private GameObject GetTemplateForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var cacheKey = $"path:{path}";
            if (_templateCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            if (ModelRegistry.TryGetByPath(path, out var template) && template.IsAlive && TemplateHasRenderable(template.Root.transform))
            {
                _templateCache[cacheKey] = template.Root;
                return template.Root;
            }

            foreach (var candidate in ModelRegistry.EnumerateTemplates())
            {
                if (!candidate.IsAlive)
                    continue;
                if (PathTailEquals(path, candidate.Path, segments: 2) && TemplateHasRenderable(candidate.Root.transform))
                {
                    _templateCache[cacheKey] = candidate.Root;
                    return candidate.Root;
                }
            }

            var fallbackGo = GameObject.Find(path);
            if (fallbackGo != null && fallbackGo.GetComponentInParent<RemoteAvatar>() == null && TemplateHasRenderable(fallbackGo.transform))
            {
                _templateCache[cacheKey] = fallbackGo;
                return fallbackGo;
            }

            return null;
        }

        private GameObject GetTemplateForPrefab(string prefabName, string meshName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return null;

            string normalized = NormalizeName(prefabName);
            string cacheKey = $"prefab:{normalized}";
            if (_templateCache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            if (ModelRegistry.TryGetByPrefab(prefabName, out var template) && template.IsAlive)
            {
                if (!string.IsNullOrWhiteSpace(meshName) && !TemplateContainsMesh(template.Root.transform, meshName))
                    return null;

                if (TemplateHasRenderable(template.Root.transform))
                {
                    _templateCache[cacheKey] = template.Root;
                    return template.Root;
                }
            }

            var direct = GameObject.Find(prefabName);
            if (direct != null && direct.GetComponentInParent<RemoteAvatar>() == null && TemplateHasRenderable(direct.transform))
            {
                if (string.IsNullOrWhiteSpace(meshName) || TemplateContainsMesh(direct.transform, meshName))
                {
                    _templateCache[cacheKey] = direct;
                    return direct;
                }
            }

            return null;
        }

        private static bool TemplateContainsMesh(Transform root, string meshName)
        {
            if (!root || string.IsNullOrWhiteSpace(meshName))
                return true;

            var skinnedArray = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(root, true);
            if (skinnedArray != null)
            {
                for (int i = 0; i < skinnedArray.Length; i++)
                {
                    var smr = skinnedArray[i];
                    if (smr?.sharedMesh != null && string.Equals(smr.sharedMesh.name, meshName, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return name.Replace("(Clone)", string.Empty).Trim().ToLowerInvariant();
        }

        private void UpgradeAvatarAppearance(ulong peerId)
        {
            if (!_remoteAvatars.TryGetValue(peerId, out var current) || current == null)
                return;

            if (!_appearanceByPeer.TryGetValue(peerId, out var appearance))
                return;

            if (_appliedAppearanceByPeer.TryGetValue(peerId, out var applied) && AppearanceMatches(applied, appearance))
                return;

            if (appearance.CharacterId < 0)
                return;

            PlayerRenderer existingRenderer = null;
            var existingRenderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<PlayerRenderer>(current.transform, true);
            if (existingRenderers != null)
            {
                for (int i = 0; i < existingRenderers.Length; i++)
                {
                    var renderer = existingRenderers[i];
                    if (renderer != null)
                    {
                        existingRenderer = renderer;
                        break;
                    }
                }
            }

            if (existingRenderer != null)
            {
                try
                {
                    var data = existingRenderer.characterData;
                    if (data != null && (int)data.eCharacter == appearance.CharacterId)
                    {
                        _appliedAppearanceByPeer[peerId] = appearance;
                        return;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            var currentPos = current.transform.position;
            var currentRot = current.transform.rotation;
            var upgradedGo = TryCreateAvatarFromAppearance(peerId, currentPos, currentRot);
            if (upgradedGo == null)
                return;

            if (upgradedGo == current.gameObject)
            {
                _appliedAppearanceByPeer[peerId] = appearance;
                return;
            }

            UnityEngine.Object.Destroy(current.gameObject);
            var upgraded = upgradedGo.GetComponent<RemoteAvatar>() ?? upgradedGo.AddComponent<RemoteAvatar>();
            upgraded.ApplyPose(currentPos, currentRot);
            _remoteAvatars[peerId] = upgraded;
            _appliedAppearanceByPeer[peerId] = appearance;
            MultiplayerPlugin.LogS.LogInfo($"[NetDriverCore] Upgraded avatar for peer {peerId} to character {DescribeAppearanceCharacter(peerId)}");
        }

        private void OnTemplateRegistered(ModelTemplate template)
        {
            foreach (var kv in _appearanceByPeer)
            {
                var peerId = kv.Key;
                var appearance = kv.Value;
                if (TemplateMatchesAppearance(template, appearance))
                {
                    _mainThreadActions.Enqueue(() =>
                    {
                        if (_remoteAvatars.ContainsKey(peerId))
                            UpgradeAvatarAppearance(peerId);
                    });
                }
            }
        }

        private static bool AppearanceMatches(in AppearanceInfo a, in AppearanceInfo b)
        {
            if (a.CharacterId != b.CharacterId)
                return false;
            return string.Equals(a.SkinName ?? string.Empty, b.SkinName ?? string.Empty, StringComparison.Ordinal);
        }

        private void UpdatePeerReadyState(ulong peerId, in AppearanceInfo appearance)
        {
            if (appearance.CharacterId >= 0)
                _readyPeers.Add(peerId);
            else
                _readyPeers.Remove(peerId);
        }

        private int CountReadyPeers()
        {
            int ready = 0;
            if (_readyPeers.Contains(_localPeerId))
                ready++;
            foreach (var peer in _connectedPeers)
            {
                if (_readyPeers.Contains(peer))
                    ready++;
            }
            return ready;
        }

        private int CountRequiredPeers()
        {
            return 1 + _connectedPeers.Count;
        }

        public void ReportMenuSelection(CharacterData characterData, SkinData skinData)
        {
            if (characterData == null)
                return;

            SkinPrefabRegistry.RegisterMenuSelection(characterData, skinData);

            var skinName = skinData != null ? SkinPrefabRegistry.NormalizeSkinNamePublic(skinData.name) : string.Empty;
            var appearance = new AppearanceInfo
            {
                RootPath = string.Empty,
                PrefabName = string.Empty,
                MeshName = string.Empty,
                MaterialNames = Array.Empty<string>(),
                CharacterClass = characterData.name ?? string.Empty,
                CharacterId = (int)characterData.eCharacter,
                SkinName = skinName
            };

            UpdatePeerReadyState(_localPeerId, appearance);

            var payload = AppearanceSerializer.Serialize(appearance);
            BroadcastAppearance(appearance, payload);
            MultiplayerPlugin.LogS?.LogInfo($"[NetDriverCore] Reported menu selection -> {characterData.eCharacter} ({skinName})");
        }

        public bool CanHostStartRun()
        {
            if (!_isHost)
                return false;

            if (!_readyPeers.Contains(_localPeerId))
                return false;

            foreach (var peer in _connectedPeers)
            {
                if (!_readyPeers.Contains(peer))
                    return false;
            }

            return true;
        }

        public string ReadyStatus()
        {
            if (!_isHost)
                return string.Empty;

            if (CanHostStartRun())
                return "All players ready.";

            var waiting = new List<string>();
            if (!_readyPeers.Contains(_localPeerId))
                waiting.Add("host");

            foreach (var peer in _connectedPeers)
            {
                if (!_readyPeers.Contains(peer))
                    waiting.Add($"peer {peer}");
            }

            return waiting.Count == 0 ? "Waiting for players..." : $"Waiting for {string.Join(", ", waiting)}";
        }

        private static bool TemplateMatchesAppearance(ModelTemplate template, AppearanceInfo appearance)
        {
            if (!string.IsNullOrEmpty(appearance.RootPath) && !string.IsNullOrEmpty(template.Path))
            {
                if (string.Equals(appearance.RootPath, template.Path, StringComparison.Ordinal))
                    return true;

                if (PathTailEquals(appearance.RootPath, template.Path, segments: 2))
                    return true;
            }

            if (!string.IsNullOrEmpty(appearance.PrefabName) && !string.IsNullOrEmpty(template.PrefabName) &&
                string.Equals(NormalizeName(appearance.PrefabName), NormalizeName(template.PrefabName), StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrEmpty(appearance.MeshName) && TemplateContainsMesh(template.Root.transform, appearance.MeshName))
                return true;

            return false;
        }

        private static bool PathTailEquals(string a, string b, int segments)
        {
            if (segments <= 0)
                return false;

            var tailA = GetPathTail(a, segments);
            var tailB = GetPathTail(b, segments);

            if (string.IsNullOrEmpty(tailA) || string.IsNullOrEmpty(tailB))
                return false;

            return string.Equals(tailA, tailB, StringComparison.Ordinal);
        }

        private static string GetPathTail(string path, int segments)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var parts = path.Split('/');
            if (parts.Length == 0)
                return string.Empty;

            if (parts.Length <= segments)
                return path;

            int start = parts.Length - segments;
            return string.Join("/", parts, start, segments);
        }

        private void FlushPendingAppearance()
        {
            if (_localPeerId == 0)
                return;

            if (!_pendingAppearance.HasValue)
                return;

            var appearance = _pendingAppearance.Value;
            var payload = _pendingAppearanceSerialized;
            _pendingAppearance = null;
            _pendingAppearanceSerialized = null;

            BroadcastAppearance(appearance, payload);
        }

        private static bool TemplateHasRenderable(Transform template)
        {
            if (!template)
                return false;

            var skinnedArray = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(template, true);
            if (skinnedArray != null && skinnedArray.Length > 0)
                return true;

            var meshFilterArray = Il2CppComponentUtil.GetComponentsInChildrenCompat<MeshFilter>(template, true);
            if (meshFilterArray != null)
            {
                for (int i = 0; i < meshFilterArray.Length; i++)
                {
                    var mf = meshFilterArray[i];
                    if (mf == null || mf.sharedMesh == null)
                        continue;

                    if (mf.sharedMesh.vertexCount > 200)
                        return true;
                }
            }

            return false;
        }
    }
}

