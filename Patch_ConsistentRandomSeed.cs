using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_ForceConsistentRandomSeed
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("ConsistentRandom");
            if (t == null)
            {
                MultiplayerPlugin.LogS?.LogWarning("[RNGSYNC] Could not find ConsistentRandom for forced seeding!");
                yield break;
            }

            foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                yield return ctor;
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            try
            {
                var t = __instance.GetType();
                var seedField = t.GetField("seed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (seedField != null)
                {
                    seedField.SetValue(__instance, MultiplayerPlugin.CoopSeed);
                    MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Forced ConsistentRandom seed={MultiplayerPlugin.CoopSeed}");
                }
                else
                {
                    MultiplayerPlugin.LogS.LogWarning("[RNGSYNC] No 'seed' field found on ConsistentRandom.");
                }
            }
            catch (System.Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[RNGSYNC] Error forcing seed: {e}");
            }
        }
    }
}
