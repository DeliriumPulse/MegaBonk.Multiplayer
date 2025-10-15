using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_AllUnityRandomInit
    {
        internal static bool SuppressOverride;

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Grab every method that looks like InitState or re-seeds Unity's RNG
            var t = typeof(UnityEngine.Random);
            foreach (var m in AccessTools.GetDeclaredMethods(t))
                if (m.Name.ToLower().Contains("init"))
                    yield return m;
        }

        [HarmonyPrefix]
        public static void Prefix(ref int seed)
        {
            if (SuppressOverride)
            {
                SuppressOverride = false;
                return;
            }

            int coopSeed = NetDriverCore.GlobalSeed != 0 ? NetDriverCore.GlobalSeed : seed;

            if (coopSeed != seed)
            {
                MultiplayerPlugin.LogS.LogInfo(
                    $"[RNGSYNC] Forcing InitState override: {seed} -> {coopSeed}"
                );
                seed = coopSeed;
            }
        }
    }
}
