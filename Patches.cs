// File: Patches.cs
using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megabonk.Multiplayer
{
    internal static class Patches
    {
        private static bool _applied;
        private static bool _clientMode;

        // Targets: single-player systems that must be disabled on clients
        private static readonly (string typeName, string method)[] Targets =
        {
            ("MapGenerationController", "Awake"),
            ("Assets.Scripts.Managers.MapController", "Awake"),
            ("Assets.Scripts.Managers.EnemyManager", "Start"),
            ("Assets.Scripts.Managers.ParticleSpawner", "Start"),
            ("PlayerSpawner", "Start"),
        };

        public static void ApplyAll(bool isClient)
        {
            if (_applied) return;
            _clientMode = isClient;
            MultiplayerPlugin.LogS.LogInfo($"[Patch] Initializing Harmony patches. isClient={isClient}");

            var harmony = new Harmony("vettr.megabonk.multiplayer.patches");
			// disable noisy reflection logs
			HarmonyLib.Tools.Logger.ChannelFilter = HarmonyLib.Tools.Logger.LogChannel.None;



            int patched = 0;
            for (int i = 0; i < Targets.Length; i++)
            {
                var (typeName, methodName) = Targets[i];
				if (typeName.StartsWith("UnityEngine") || typeName.StartsWith("TMPro") || typeName.StartsWith("Unity.")) 
				{
					continue; // skip Unity modules
				}


                try
                {
                    var t = AccessTools.TypeByName(typeName);
                    if (t == null)
                    {
                        MultiplayerPlugin.LogS.LogInfo($"[Patch] Type not found (skipped): {typeName}");
                        continue;
                    }

                    var m = AccessTools.Method(t, methodName);
                    if (m == null)
                    {
                        MultiplayerPlugin.LogS.LogInfo($"[Patch] Method not found: {typeName}.{methodName}");
                        continue;
                    }

                    var prefix = new HarmonyMethod(typeof(Patches).GetMethod(nameof(ClientSafeDisablePrefix)));
                    harmony.Patch(m, prefix: prefix);
                    patched++;
                    MultiplayerPlugin.LogS.LogInfo($"[Patch] Client-safe disable applied to {typeName}.{methodName}");
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogWarning($"[Patch] Failed to patch {typeName}.{methodName}: {e}");
                }
            }

            _applied = true;
            MultiplayerPlugin.LogS.LogInfo($"[Patch] Applied: {patched} method(s). ClientMode={_clientMode}");
        }

        /// <summary>
        /// Prefix that disables heavy single-player systems on clients
        /// without removing their GameObjects entirely.
        /// </summary>
        public static bool ClientSafeDisablePrefix(MonoBehaviour __instance)
		{
			if (!_clientMode) return true; // host still runs normal Awake()

			string scene = SceneManager.GetActiveScene().name ?? "";
			if (!scene.Equals("GeneratedMap", StringComparison.OrdinalIgnoreCase))
				return true;

			// Special case: let map generation run, but skip heavy spawners later.
			if (__instance.GetType().Name == "MapGenerationController")
			{
				MultiplayerPlugin.LogS.LogInfo("[Patch] Allowing MapGenerationController on client for sync seed.");
				return true;
			}

			try
			{
				if (__instance != null)
				{
					__instance.enabled = false;
					MultiplayerPlugin.LogS.LogInfo($"[Patch] Disabled {__instance.GetType().FullName} on client.");
				}
			}
			catch (Exception e)
			{
				MultiplayerPlugin.LogS.LogWarning($"[Patch] Disable error: {e.Message}");
			}

			return true;
		}

    }
}