using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_MapGeneratorForceSeed
    {
        [HarmonyTargetMethod]
		public static MethodBase TargetMethod()
		{
			var t = AccessTools.TypeByName("MapGenerator") ??
					AccessTools.TypeByName("Assets.Scripts.Managers.MapGenerator");

			if (t == null)
			{
				MultiplayerPlugin.LogS.LogWarning("[RNGSYNC] MapGenerator class not found!");
				return null;
			}

			// Pick the exact overload by parameter types
			var m = AccessTools.Method(t, "GenerateMap", new[] {
				AccessTools.TypeByName("MapData"),
				AccessTools.TypeByName("StageData"),
				typeof(int)
			});

			if (m == null)
			{
				MultiplayerPlugin.LogS.LogWarning($"[RNGSYNC] Could not find overload MapGenerator.GenerateMap(MapData, StageData, Int32)");
				return null;
			}

			MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Hooking {t.FullName}.{m.Name}(MapData, StageData, Int32)");
			return m;
		}


        [HarmonyPrefix]
        public static void Prefix()
        {
            if (PlayerPrefs.HasKey("coop_seed"))
            {
                int coopSeed = PlayerPrefs.GetInt("coop_seed");
                UnityEngine.Random.InitState(coopSeed);
                MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Re-seeding RNG → {coopSeed}");
				UnityEngine.Random.state = UnityEngine.Random.state; // ensure internal reset
            }
        }
    }
}

