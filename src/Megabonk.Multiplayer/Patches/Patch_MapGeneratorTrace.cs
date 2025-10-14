using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_MapGeneratorTrace
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
            MultiplayerPlugin.LogS.LogInfo($"[RNGTRACE] >>> Map generation started at frame {Time.frameCount}");
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            MultiplayerPlugin.LogS.LogInfo($"[RNGTRACE] <<< Map generation finished at frame {Time.frameCount}");
        }
    }
}
