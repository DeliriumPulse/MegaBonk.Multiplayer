using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Megabonk.Multiplayer.Transport
{
    /// <summary>
    /// Clean non-singleton version — separate instance per session.
    /// Keeps LiteNetLib NetManager alive via runner and ensures
    /// host socket fully ready before clients connect.
    /// </summary>
    public sealed class LiteNetTransport : ITransport, INetEventListener
    {
        private readonly EventBasedNetListener _listener;
        private readonly NetManager _net;
		private readonly Dictionary<string, ulong> _epToId = new();

        private readonly Dictionary<NetPeer, ulong> _peerToId = new();
        private readonly Dictionary<ulong, NetPeer> _idToPeer = new();
        private readonly List<NetPeer> _connectedPeers = new();
        private ulong _nextPeerId = 1;
		public int GetPort() => _hostPort;

        private int _hostPort;
        private string _hostAddress = "127.0.0.1";
        private string _key = "megabonk_coop";

        public bool IsServer { get; private set; }
        public int ConnectedCount => _connectedPeers.Count;

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, ArraySegment<byte>, bool> DataReceived;

        public LiteNetTransport()
        {
			

            _listener = new EventBasedNetListener();
            _net = new NetManager(_listener)
            {
                AutoRecycle = true,
                IPv6Mode = IPv6Mode.Disabled,
                UnsyncedEvents = true,
                DisconnectTimeout = 15000,
                PingInterval = 2000
            };

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;

            LiteNetTransportRunner.Ensure(_net);
            Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug("[LiteNetTransport] Transport created.");
        }
		// --------------------------------------------------------------------
		// Accessors used by LiteNetTransportRunner for rebinding
		// --------------------------------------------------------------------
		public LiteNetLib.EventBasedNetListener GetListener() => _listener;
		public LiteNetLib.NetManager GetInternalNet() => _net;
        // --------------------------------------------------------------------
        // Startup / shutdown
        // --------------------------------------------------------------------
        public void StartHost(int port, string key)
        {
            IsServer = true;
            _hostPort = port;
            _key = key ?? "megabonk_coop";

            _net.Start(_hostPort);
            // Warm up the socket before accepting clients
            for (int i = 0; i < 20; i++)
            {
                _net.PollEvents();
                System.Threading.Thread.Sleep(10);
            }

            Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug($"[LiteNetTransport] Host started on port {_hostPort}");
        }

        public void StartClient(string hostAddress, int port, string key, ulong _)
		{
			IsServer = false;
			_hostPort = port;
			_hostAddress = string.IsNullOrWhiteSpace(hostAddress) ? "127.0.0.1" : hostAddress;
			_key = key ?? "megabonk_coop";

			// Bind to a random high port so we don’t collide with host
			int tryPort = UnityEngine.Random.Range(30000, 40000);
			try
			{
				_net.Start(System.Net.IPAddress.Any, System.Net.IPAddress.Any, tryPort);
				Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug($"[LiteNetTransport] Client bound to local port {tryPort}");
			}
			catch (Exception e)
			{
				Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning($"[LiteNetTransport] Failed to bind port {tryPort}: {e.Message}");
				_net.Start();
			}

			_net.Connect(_hostAddress, _hostPort, _key);
			Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug($"[LiteNetTransport] Client connecting to {_hostAddress}:{_hostPort}");
		}
		

        public void Poll() => _net?.PollEvents();

        // --------------------------------------------------------------------
        // Event handling
        // --------------------------------------------------------------------
		public void OnPeerConnected(NetPeer peer)
		{
			if (peer == null) return;

			ulong id;

			if (IsServer)
			{
				id = _nextPeerId++;
				_peerToId[peer] = id;
				_idToPeer[id] = peer;
				_epToId[peer.EndPoint.ToString()] = id;
				_connectedPeers.Add(peer);

				// Send ID assignment to the connecting client
				var writer = new NetDataWriter();
				writer.Put((byte)0x02); // MSG_ASSIGN_ID
				writer.Put(id);
				peer.Send(writer, DeliveryMethod.ReliableOrdered);

				Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug($"[LiteNetTransport] Host registered client {peer.EndPoint} → ID={id}");
			}
			else
			{
				id = 0; // host always has ID 0 (from the client's perspective)
				_peerToId[peer] = id;
				_idToPeer[id] = peer;
				_epToId[peer.EndPoint.ToString()] = id;
				_connectedPeers.Add(peer);
				Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug($"[LiteNetTransport] Client connected to host {peer.EndPoint}, ID={id}");
			}

			PeerConnected?.Invoke(id);
		}


		public void SendToHost(NetDataWriter writer, bool reliable)
		{
			if (_idToPeer.TryGetValue(0, out var hostPeer))
			{
				hostPeer.Send(writer, reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
				if (Megabonk.Multiplayer.MultiplayerPlugin.VerboseNetwork)
					Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug("[LiteNetTransport] Sent message to host.");
			}
		}


        /// <summary>
        /// Ensures the internal dictionaries contain a mapping for this peer without firing new events.
        /// Used by the runner when Unity drops delegate bindings.
        /// </summary>
        internal bool EnsurePeerMapping(NetPeer peer)
        {
            if (peer == null)
                return false;

            if (_peerToId.ContainsKey(peer))
                return false;

            if (_epToId.TryGetValue(peer.EndPoint.ToString(), out var existingId))
            {
                _peerToId[peer] = existingId;
                _idToPeer[existingId] = peer;
                if (!_connectedPeers.Contains(peer))
                    _connectedPeers.Add(peer);
                return false;
            }

            // We have no record of this endpoint; treat as a fresh connection on the server.
            if (IsServer)
            {
                OnPeerConnected(peer);
                return true;
            }

            // On clients, host is always ID=0.
            _peerToId[peer] = 0;
            _idToPeer[0] = peer;
            if (!_connectedPeers.Contains(peer))
                _connectedPeers.Add(peer);
            return false;
        }



		public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
		{
			if (peer == null) return;

			if (_peerToId.TryGetValue(peer, out ulong id))
			{
				_peerToId.Remove(peer);
				_idToPeer.Remove(id);
				_epToId.Remove(peer.EndPoint.ToString());
				_connectedPeers.Remove(peer);
				Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug($"[LiteNetTransport] Peer {id} disconnected.");
				PeerDisconnected?.Invoke(id);
			}
		}


        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
		{
			if (peer == null || reader == null)
				return;

			// Prefer fast object lookup first
			ulong id;
			if (!_peerToId.TryGetValue(peer, out id))
			{
				// Fallback to endpoint lookup (stable key)
				_epToId.TryGetValue(peer.EndPoint.ToString(), out id);
			}

			int len = reader.AvailableBytes;
			if (len <= 0)
				return;

			var tmp = new byte[len];
			reader.GetBytes(tmp, len);
			reader.Recycle();

			bool reliable = deliveryMethod != DeliveryMethod.Unreliable && deliveryMethod != DeliveryMethod.Sequenced;

			if (Megabonk.Multiplayer.MultiplayerPlugin.VerboseNetwork)
			{
				Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug(
					$"[LiteNetTransport] Packet received from {peer.EndPoint}, bytes={len}, firstByte={(len > 0 ? tmp[0] : 255):X2}, mappedId={id}, reliable={reliable}");
			}

			if (id == 0 && IsServer)
			{
				Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning($"[LiteNetTransport] Unregistered endpoint {peer.EndPoint} on server; dropping packet.");
				return;
			}

			DataReceived?.Invoke(id, new ArraySegment<byte>(tmp, 0, len), reliable);
		}



        // --------------------------------------------------------------------
        // Sending
        // --------------------------------------------------------------------
		public void SendToAll(byte[] data, bool reliable)
		{
			if (_net == null || data == null || data.Length == 0)
				return;

			var method = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

			foreach (var peer in _net.ConnectedPeerList)
			{
				try
				{
					peer.Send(data, 0, data.Length, method);   // explicit offset + length
				}
				catch (Exception e)
				{
					Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning(
						$"[LiteNetTransport] SendToAll failed: {e.Message}");
				}
			}

			if (Megabonk.Multiplayer.MultiplayerPlugin.VerboseNetwork)
			{
				Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug(
					$"[LiteNetTransport] SendToAll -> {data.Length} bytes, reliable={reliable}");
			}

		}





		public void SendTo(ulong peerId, byte[] data, bool reliable)
		{
			if (data == null || data.Length == 0)
				return;

			if (!_idToPeer.TryGetValue(peerId, out var peer))
				return;

			var method = reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

			try
			{
				peer.Send(data, 0, data.Length, method);       // explicit offset + length

				if (Megabonk.Multiplayer.MultiplayerPlugin.VerboseNetwork)
				{
					Megabonk.Multiplayer.MultiplayerPlugin.LogS?.LogDebug(
						$"[LiteNetTransport] Sent {data.Length} bytes to {peer.EndPoint}, reliable={reliable}");
				}
			}
			catch (Exception e)
			{
				Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning(
					$"[LiteNetTransport] SendTo failed: {e.Message}");
			}
		}




        // --------------------------------------------------------------------
        // LiteNetLib required callbacks
        // --------------------------------------------------------------------
        public void OnNetworkError(System.Net.IPEndPoint ep, System.Net.Sockets.SocketError err)
            => Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogWarning($"[LiteNetTransport] Network error: {err}");

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint ep, NetPacketReader r, UnconnectedMessageType t) { }

        public void OnConnectionRequest(ConnectionRequest req)
        {
            if (IsServer && _net.ConnectedPeersCount < 8)
                req.AcceptIfKey(_key);
            else
                req.Reject();
        }
    }
}
