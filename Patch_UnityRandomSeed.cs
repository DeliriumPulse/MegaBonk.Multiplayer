using HarmonyLib;
using UnityEngine;
using System.Reflection;
using Random = UnityEngine.Random;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_UnityRandomSeed
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("MapGenerator")
                    ?? AccessTools.TypeByName("Assets.Scripts.Managers.MapGenerator")
                    ?? AccessTools.TypeByName("Assets.Scripts.Managers.MapGenerationController");

            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[RNGSYNC] Could not find MapGenerator type!");
                return null;
            }

            var m = AccessTools.Method(t, "GenerateMap", new[] {
                AccessTools.TypeByName("MapData"),
                AccessTools.TypeByName("StageData"),
                typeof(int)
            });
            return m;
        }

        [HarmonyPrefix]
        public static void Prefix(int __2)
        {
            // __2 = seed argument from GenerateMap(MapData, StageData, Int32)
            int seed = NetDriverCore.GlobalSeed != 0 ? NetDriverCore.GlobalSeed : __2;


            Random.InitState(seed);
            MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] UnityEngine.Random.InitState({seed}) applied before terrain generation");
        }
    }
}
