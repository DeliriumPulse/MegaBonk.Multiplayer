// File: NetTransportFactory.cs
using Megabonk.Multiplayer.Transport;

namespace Megabonk.Multiplayer
{
    internal static class NetTransportFactory
    {
        public static ITransport Create(string id)
        {
            id = id?.ToLowerInvariant() ?? "lite";

            if (id.Contains("steam"))
                return new SteamP2PTransport();
            else
                return new LiteNetTransport();
        }
    }
}
