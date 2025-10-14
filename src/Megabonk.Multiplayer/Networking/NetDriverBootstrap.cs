// File: NetDriverBootstrap.cs
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace Megabonk.Multiplayer
{
    /// <summary>
    /// Registers all IL2CPP types used by the plugin before any AddComponent calls.
    /// Keep this list in sync with any new MonoBehaviours you introduce.
    /// </summary>
    [BepInPlugin("megabonk.multiplayer.bootstrap", "Megabonk Multiplayer Bootstrap", "0.3.1")]
    public class NetDriverBootstrap : BasePlugin
    {
        public override void Load()
        {
            // Core behaviours used anywhere in the plugin
            ClassInjector.RegisterTypeInIl2Cpp<NetDriverShim>();
            ClassInjector.RegisterTypeInIl2Cpp<NetDriverShim.ShimCoroutineRunner>();
            ClassInjector.RegisterTypeInIl2Cpp<InputDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<HostPawnController>();
            ClassInjector.RegisterTypeInIl2Cpp<CameraFollower>();
            ClassInjector.RegisterTypeInIl2Cpp<AnimatorSyncSource>();
            ClassInjector.RegisterTypeInIl2Cpp<RemoteAvatar>();

            // Transport runner that keeps LiteNetLib polling
            ClassInjector.RegisterTypeInIl2Cpp<Megabonk.Multiplayer.Transport.LiteNetTransportRunner>();

            // Ensure ModelRegistry survives IL2CPP stripping so runtime patch tools can find it
            AccessTools.TypeByName(typeof(ModelRegistry).FullName);

        }
    }
}
