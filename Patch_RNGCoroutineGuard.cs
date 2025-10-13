using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_RNGCoroutineGuard
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Look for coroutine-style generation methods
            string[] candidateTypes =
            {
                "MapGenerator",
                "ProceduralTileGeneration",
                "MapGenerationController"
            };

            foreach (string cls in candidateTypes)
            {
                var t = AccessTools.TypeByName(cls) ??
                        AccessTools.TypeByName("Assets.Scripts.Managers." + cls);

                if (t == null)
                    continue;

                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (m.ReturnType.Name.Contains("IEnumerator") &&
                        (m.Name.Contains("Generate") || m.Name.Contains("Map") || m.Name.Contains("Build")))
                    {
                        MultiplayerPlugin.LogS.LogInfo($"[RNGGUARD] Hooking coroutine {t.FullName}.{m.Name}()");
                        yield return m;
                    }
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix(MethodBase __originalMethod)
        {
            if (PlayerPrefs.HasKey("coop_seed"))
            {
                int coopSeed = PlayerPrefs.GetInt("coop_seed");
                UnityEngine.Random.InitState(coopSeed);
                MultiplayerPlugin.LogS.LogInfo($"[RNGGUARD] Coroutine RNG re-seeded → {coopSeed} before {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
            }
        }
    }
}
