// File: ITransport.cs
// Updated to match LiteNetTransport event names.
using System;

namespace Megabonk.Multiplayer.Transport
{
    public interface ITransport
    {
        bool IsServer { get; }
        int ConnectedCount { get; }

        event Action<ulong> PeerConnected;
        event Action<ulong> PeerDisconnected;
        event Action<ulong, ArraySegment<byte>, bool> DataReceived;

        void StartHost(int port, string key);
        void StartClient(string hostAddress, int port, string key, ulong hostSteamId);
        void Poll();
        void SendToAll(byte[] data, bool reliable);
        void SendTo(ulong peerId, byte[] data, bool reliable);
    }
}
