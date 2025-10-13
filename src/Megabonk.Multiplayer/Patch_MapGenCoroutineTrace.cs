using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_MapGenCoroutineTrace
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // Dynamically locate the MapGenerationController class
            var t = AccessTools.TypeByName("MapGenerationController") ??
                    AccessTools.TypeByName("Assets.Scripts.Managers.MapGenerationController");

            if (t == null)
            {
                MultiplayerPlugin.LogS.LogError("[Patch_MapGenCoroutineTrace] Could not find MapGenerationController in any namespace!");
                return null;
            }

            var m = AccessTools.Method(t, "Start");
            if (m == null)
            {
                MultiplayerPlugin.LogS.LogError("[Patch_MapGenCoroutineTrace] Could not find Start() method!");
                return null;
            }

            MultiplayerPlugin.LogS.LogInfo($"[Patch_MapGenCoroutineTrace] Targeting {t.FullName}.Start()");
            return m;
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            MultiplayerPlugin.LogS.LogInfo($"[CoroutineTrace] MapGenerationController.Start() fired at frame {Time.frameCount}");
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            MultiplayerPlugin.LogS.LogInfo($"[CoroutineTrace] MapGenerationController.Start() completed at frame {Time.frameCount}");
        }
    }
}
