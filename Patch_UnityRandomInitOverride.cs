using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_UnityRandomInitOverride
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(UnityEngine.Random);
            foreach (var m in AccessTools.GetDeclaredMethods(t))
            {
                if (m.Name.Contains("InitState"))
                    yield return m;
            }
        }

        [HarmonyPrefix]
		public static void Prefix(ref int seed)
		{
			int coopSeed = NetDriverCore.GlobalSeed != 0 ? NetDriverCore.GlobalSeed : seed;
			if (coopSeed != seed)
			{
				MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Forcing InitState override: {seed} -> {coopSeed}");
				seed = coopSeed;
			}
		}

    }
}
