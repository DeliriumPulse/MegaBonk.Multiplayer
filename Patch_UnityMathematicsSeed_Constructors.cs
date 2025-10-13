using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_UnityMathematicsSeed_Constructors
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("Unity.Mathematics.Random");
            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[UMATHSEED] Unity.Mathematics.Random not found; skipping constructor patch.");
                yield break;
            }

            foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                yield return ctor;
        }

        [HarmonyPrefix]
        public static void Prefix(ref uint seed)
        {
            try
            {
                int forcedSeed = CoopSeedStorage.Value != int.MinValue
                    ? CoopSeedStorage.Value
                    : NetDriverCore.GlobalSeed;

                if (forcedSeed == int.MinValue)
                    return;

                seed = (uint)forcedSeed;
                MultiplayerPlugin.LogS.LogInfo($"[UMATHSEED] Overriding Unity.Mathematics.Random ctor seed → {seed}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[UMATHSEED] Exception (constructors): {e}");
            }
        }
    }
}
