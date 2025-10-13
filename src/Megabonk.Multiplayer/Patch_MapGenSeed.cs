using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_MapGenSeed
    {
        [HarmonyTargetMethod]
		public static MethodBase TargetMethod()
		{
			var t = AccessTools.TypeByName("MapGenerator");
			if (t == null)
			{
				MultiplayerPlugin.LogS.LogError("[Patch_MapGenSeed] Could not find MapGenerator!");
				return null;
			}

			// Explicitly choose the overload with 3 parameters
			var m = AccessTools.Method(t, "GenerateMap",
				new System.Type[] {
					AccessTools.TypeByName("MapData"),
					AccessTools.TypeByName("StageData"),
					typeof(int)
				});

			if (m == null)
			{
				MultiplayerPlugin.LogS.LogError("[Patch_MapGenSeed] Failed to locate GenerateMap(MapData, StageData, Int32)!");
				return null;
			}

			MultiplayerPlugin.LogS.LogInfo($"[Patch_MapGenSeed] Targeting {t.FullName}.{m.Name}(MapData, StageData, Int32)");
			return m;
		}



        [HarmonyPrefix]
		public static void Prefix(object __instance, object __0, object __1, int __2)
		{
			try
			{
				int seedToUse = __2;

				// If the multiplayer sync seed exists, use that instead
				if (PlayerPrefs.HasKey("coop_seed"))
				{
					seedToUse = PlayerPrefs.GetInt("coop_seed");
					MultiplayerPlugin.LogS.LogInfo($"[Patch_MapGenSeed] Overriding incoming seed {__2} with coop_seed={seedToUse}");
				}
				else
				{
					MultiplayerPlugin.LogS.LogInfo($"[Patch_MapGenSeed] Using incoming seed={__2}");
				}

				// Explicitly reset Unity's RNG
				UnityEngine.Random.InitState(seedToUse);
				MultiplayerPlugin.LogS.LogInfo($"[Patch_MapGenSeed] RNG state explicitly reset with seed {seedToUse}");
			}
			catch (System.Exception e)
			{
				MultiplayerPlugin.LogS.LogError($"[Patch_MapGenSeed] Exception in Prefix: {e}");
			}
		}


    }
}
