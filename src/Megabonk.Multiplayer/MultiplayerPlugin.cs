using System;
using Assets.Scripts._Data;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Megabonk.Multiplayer.Transport;

namespace Megabonk.Multiplayer
{
    [BepInPlugin(Guid, Name, Version)]
    public class MultiplayerPlugin : BasePlugin
    {
        public const string Guid = "vettr.megabonk.multiplayer";
        public const string Name = "Megabonk Multiplayer";
        public const string Version = "0.3.1";

        public static ManualLogSource LogS;
        public static NetDriverCore Driver { get; internal set; }

        // Config entries
        private BepInEx.Configuration.ConfigEntry<string> _role;
        private BepInEx.Configuration.ConfigEntry<string> _hostAddress;
        private BepInEx.Configuration.ConfigEntry<int> _port;
        private BepInEx.Configuration.ConfigEntry<bool> _autoReconnect;
        private BepInEx.Configuration.ConfigEntry<bool> _enableTypeDump;
        private BepInEx.Configuration.ConfigEntry<string> _transport;
        private BepInEx.Configuration.ConfigEntry<string> _hostSteamId;
        public static int CoopSeed = int.MinValue;

        public override void Load()
        {
            LogS = Log;

            _role = Config.Bind("Multiplayer", "Role", "Host", "Host or Client");
            _hostAddress = Config.Bind("Multiplayer", "HostAddress", "127.0.0.1", "Server IP for clients.");
            _port = Config.Bind("Multiplayer", "Port", 28960, "UDP port");
            _autoReconnect = Config.Bind("Multiplayer", "AutoReconnectClient", false, "Auto-reconnect after timeout");
            _enableTypeDump = Config.Bind("Debug", "EnableTypeDump", false, "Dump typelist.txt on first map load");
            _transport = Config.Bind("Multiplayer", "Transport", "lite", "Transport: 'lite' or 'steam'");
            _hostSteamId = Config.Bind("Multiplayer", "HostSteamId", "0", "64-bit SteamID of host");

            // Register IL2CPP types for BepInEx
            NetDriverShim.EnsureRegistered();
            ClassInjector.RegisterTypeInIl2Cpp<InputDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<CameraFollower>();
            ClassInjector.RegisterTypeInIl2Cpp<HostPawnController>();
            ClassInjector.RegisterTypeInIl2Cpp<RemoteAvatar>();
            ClassInjector.RegisterTypeInIl2Cpp<PatchBootstrapper>();

            // Bootstrap persistent network holder
            var go = new GameObject("Megabonk.Net");
            UnityEngine.Object.DontDestroyOnLoad(go);

            var drvObj = go.AddComponent<NetDriverShim>();

            bool isHost = string.Equals(_role.Value, "Host", StringComparison.OrdinalIgnoreCase);
            ulong hostSteamId = 0;
            UInt64.TryParse(_hostSteamId.Value, out hostSteamId);

            // Initialize driver
            drvObj.Initialize(
                isHost,
                _hostAddress.Value,
                _port.Value,
                _autoReconnect.Value,
                _enableTypeDump.Value,
                _transport.Value,
                hostSteamId
            );

            // Store reference to the underlying NetDriverCore
            Driver = drvObj.DriverCore;

            // Add Harmony patch bootstrapper
            go.AddComponent<PatchBootstrapper>();

            LogS.LogInfo($"{Name} {Version} loaded. Role={_role.Value} " +
                         $"Transport={_transport.Value} Host={_hostAddress.Value}:{_port.Value}");
        }

        private class PatchBootstrapper : MonoBehaviour
        {
            private void Start() => Invoke(nameof(DoPatch), 0.1f);

            private void DoPatch()
            {
                try
                {
                    HarmonyLib.Tools.Logger.ChannelFilter = HarmonyLib.Tools.Logger.LogChannel.None;
                    var harmony = new Harmony("vettr.megabonk.multiplayer.core");

                    void SafePatch(Type t)
                    {
                        try { harmony.CreateClassProcessor(t).Patch(); }
                        catch (Exception e) { LogS.LogWarning($"[PATCH] Skipped {t.Name}: {e.Message}"); }
                    }

                    SafePatch(typeof(Patch_MapGenSeed));
                    SafePatch(typeof(Patch_ForceConsistentRandomSeed));
                    SafePatch(typeof(Patch_ConsistentRandomInit));
                    SafePatch(typeof(Patch_TraceCR_Constructors));
                    SafePatch(typeof(Patch_DumpConsistentRandomFields));
                    SafePatch(typeof(Patch_SystemRandomTrace));
                    SafePatch(typeof(Patch_MapGeneratorTrace));
                    SafePatch(typeof(Patch_UnityRandomSeed));
                    SafePatch(typeof(Patch_UnityRandomInitOverride));
                    SafePatch(typeof(Patch_AllUnityRandomInit));
                    SafePatch(typeof(Patch_UnityRandomStateSetter));
                    SafePatch(typeof(Patch_SeedBeforeGeneration));
                    SafePatch(typeof(Patch_MapGeneratorForceSeed));
                    SafePatch(typeof(Patch_RNGGuard));
                    SafePatch(typeof(Patch_RNGCoroutineGuard));
                    SafePatch(typeof(Patch_UnityMathRandomTrace));
                    SafePatch(typeof(Patch_UnityMathematicsSeed_Constructors));
                    SafePatch(typeof(Patch_UnityMathematicsSeed_Init));
                    SafePatch(typeof(Patch_UnityMathematicsSeed_CreateFromIndex));
                    SafePatch(typeof(Patch_JobCreationTrace));
                    SafePatch(typeof(Patch_ProceduralTileJobSeed));
                    SafePatch(typeof(Patch_TraceTileRNG));
                    SafePatch(typeof(Patch_ForceTileSeed));
                    SafePatch(typeof(Patch_SceneManagerSceneLoaded));
                    SafePatch(typeof(Patch_PlayerStats));
                    SafePatch(typeof(Patch_PlayerHealth_UpdateMaxValues));
                    SafePatch(typeof(Patch_PlayerRenderer));
                    SafePatch(typeof(Patch_MyButtonCharacter));
                    SafePatch(typeof(Patch_CharacterInfoUI));

                    try
                    {
                        try
                        {
                            var characterInfoType = typeof(CharacterInfoUI);
                            var methods = AccessTools.GetDeclaredMethods(characterInfoType);
                            foreach (var method in methods)
                            {
                                var parameters = method.GetParameters();
                                string signature = parameters.Length == 0
                                    ? string.Empty
                                    : string.Join(", ", Array.ConvertAll(parameters, p => p.ParameterType?.Name ?? "<null>"));
                                LogS.LogDebug($"[Patch_MainMenuRoster] Candidate -> {method.Name}({signature})");
                            }
                        }
                        catch (Exception dumpEx)
                        {
                            LogS.LogDebug($"[Patch_MainMenuRoster] Failed to enumerate CharacterInfoUI methods: {dumpEx.Message}");
                        }

                        harmony.CreateClassProcessor(typeof(Patch_MainMenuRoster)).Patch();
                    }
                    catch (Exception e)
                    {
                        LogS.LogWarning($"[PATCH] Patch_MainMenuRoster failed: {e}");
                    }

                    try
                    {
                        var gameEventsType = AccessTools.TypeByName("GameEvents");
                        if (gameEventsType != null)
                        {
                            var trigger = AccessTools.Method(gameEventsType, "TriggerCharacterChanged");
                            if (trigger != null)
                            {
                                harmony.Patch(trigger, postfix: new HarmonyMethod(typeof(PatchBootstrapper).GetMethod(nameof(CharacterChangedPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
                                LogS.LogInfo("[PATCH] Hooked GameEvents.TriggerCharacterChanged");
                            }
                            else
                            {
                                LogS.LogWarning("[PATCH] GameEvents.TriggerCharacterChanged not found.");
                            }
                        }
                        else
                        {
                            LogS.LogWarning("[PATCH] GameEvents type not found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogS.LogWarning($"[PATCH] Failed to hook GameEvents.TriggerCharacterChanged: {ex.Message}");
                    }

                    var harmonyJob = new Harmony("vettr.megabonk.multiplayer.jobrng.force");
                    var prefix = typeof(Patch_DumpAndForceJobRNGs)
                        .GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);

                    foreach (var target in Patch_DumpAndForceJobRNGs.TargetMethods())
                    {
                        if (target == null) continue;
                        if (target.DeclaringType?.ContainsGenericParameters == true ||
                            target.ContainsGenericParameters)
                            continue;

                        try { harmonyJob.Patch(target, prefix: new HarmonyMethod(prefix)); }
                        catch (Exception e)
                        {
                            LogS.LogWarning($"[JOBRNG] Skipped {target?.DeclaringType?.FullName}.{target?.Name}: {e.Message}");
                        }
                    }

                    LogS.LogInfo("[JOBRNG] Manual patch registration completed.");
                }
                catch (Exception e)
                {
                    LogS.LogError($"[Bootstrap] Harmony patch failure: {e}");
                }

                Destroy(this);
            }

            private static void CharacterChangedPostfix(CharacterData character)
            {
                if (character == null)
                    return;

                SkinPrefabRegistry.RegisterMenuSelection(character, null);

                var driver = Driver;
                driver?.ReportMenuSelection(character, null);
            }
        }
    }
}

