using HarmonyLib;
using System;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_UnityMathematicsSeed_Init
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("Unity.Mathematics.Random");
            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[UMATHSEED] Unity.Mathematics.Random not found; skipping InitState patch.");
                yield break;
            }

            var m = t.GetMethod("InitState", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(uint) }, null);
            if (m == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[UMATHSEED] Unity.Mathematics.Random.InitState(uint) not found.");
                yield break;
            }

            MultiplayerPlugin.LogS.LogInfo($"[UMATHSEED] Hooking {m.DeclaringType.FullName}.{m.Name}(uint)");
            yield return m;
        }

        [HarmonyPrefix]
        public static void Prefix(ref uint seed)
        {
            try
            {
                int forcedSeed =
                    CoopSeedStorage.Value != int.MinValue ? CoopSeedStorage.Value : NetDriverCore.GlobalSeed;

                if (forcedSeed == int.MinValue)
                    return;

                seed = (uint)forcedSeed;
                MultiplayerPlugin.LogS.LogInfo($"[UMATHSEED] Overriding Random.InitState({seed})");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[UMATHSEED] Exception (InitState): {e}");
            }
        }
    }
}
