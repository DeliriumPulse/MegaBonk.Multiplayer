using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_ConsistentRandomInit
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("ConsistentRandom");
            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[Patch_ConsistentRandomInit] Could not find ConsistentRandom type!");
                yield break;
            }

            foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                MultiplayerPlugin.LogS.LogInfo($"[Patch_ConsistentRandomInit] Hooking ConsistentRandom ctor: ({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name))})");
                yield return ctor;
            }
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance, params object[] __args)
        {
            try
            {
                int forcedSeed = CoopSeedStorage.Value;
                if (forcedSeed == int.MinValue) return;

                // if constructor has an int seed parameter
                if (__args != null && __args.Length == 1 && __args[0] is int)
                {
                    __args[0] = forcedSeed;
                    MultiplayerPlugin.LogS.LogInfo($"[Patch_ConsistentRandomInit] Overriding ConsistentRandom(int seed) → {forcedSeed}");
                }
                else
                {
                    // no-arg ctor fallback: directly set the field
                    var field = __instance.GetType().GetField("seed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    field?.SetValue(__instance, forcedSeed);
                    MultiplayerPlugin.LogS.LogInfo($"[Patch_ConsistentRandomInit] Applied forced seed {forcedSeed} to parameterless ctor instance.");
                }
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[Patch_ConsistentRandomInit] Exception: {e}");
            }
        }
    }
}
