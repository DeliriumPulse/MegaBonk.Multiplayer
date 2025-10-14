using HarmonyLib;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_ConsistentRandomTrace
    {
        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("ConsistentRandom");
            if (t == null) return null;

            // Hook both parameterless and int overloads if available
            return AccessTools.Method(t, "Next") ?? AccessTools.Method(t, "Next", new[] { typeof(int) });
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance)
        {
            try
            {
                //MultiplayerPlugin.LogS.LogInfo($"[RNG Trace] ConsistentRandom.Next() called at frame {Time.frameCount}");
            }
            catch { }
        }
    }
}

