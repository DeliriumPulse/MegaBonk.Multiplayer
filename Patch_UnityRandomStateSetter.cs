using HarmonyLib;
using UnityEngine;
using System.Reflection;
using Random = UnityEngine.Random;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(UnityEngine.Random), "set_state")]
    public static class Patch_UnityRandomStateSetter
    {
        [HarmonyPrefix]
        public static void Prefix(ref Random.State value)
        {
            if (PlayerPrefs.HasKey("coop_seed"))
            {
                int forcedSeed = PlayerPrefs.GetInt("coop_seed");
                UnityEngine.Random.InitState(forcedSeed);
                MultiplayerPlugin.LogS.LogInfo(
                    $"[RNGSYNC] Overriding Random.state setter → coop_seed={forcedSeed}"
                );
            }
        }
    }
}