using HarmonyLib;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_PreLoadRNGSeed
    {
        // Patch *before* scene async loading begins
        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            // look for the class handling scene loads
            var t = AccessTools.TypeByName("LoadingScreenController") ??
                    AccessTools.TypeByName("Assets.Scripts.Managers.LoadingScreenController");

            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[RNGSYNC] Could not find LoadingScreenController!");
                return null;
            }

            var m = AccessTools.Method(t, "StartLoading"); // or "BeginLoad" / "Show"
            if (m == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[RNGSYNC] Could not find LoadingScreenController.StartLoading()");
                return null;
            }

            MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Hooking {t.FullName}.{m.Name}()");
            return m;
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            if (PlayerPrefs.HasKey("coop_seed"))
            {
                int coopSeed = PlayerPrefs.GetInt("coop_seed");
                UnityEngine.Random.InitState(coopSeed);
                MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Pre-Loading RNG locked → {coopSeed}");
            }
        }
    }
}
