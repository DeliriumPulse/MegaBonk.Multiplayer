using System;

namespace Megabonk.Multiplayer.Transport
{
    // Minimal placeholder to satisfy NetTransportFactory
    public class SteamP2PTransport : ITransport
    {
        public bool IsServer => false;
        public int ConnectedCount => 0;

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, ArraySegment<byte>, bool> DataReceived;

        public void StartHost(int port, string key) { }
        public void StartClient(string hostAddress, int port, string key, ulong hostSteamId) { }
        public void Poll() { }
        public void SendToAll(byte[] data, bool reliable) { }
        public void SendTo(ulong peerId, byte[] data, bool reliable) { }
    }
}
