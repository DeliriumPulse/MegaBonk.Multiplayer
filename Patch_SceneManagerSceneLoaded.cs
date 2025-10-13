// File: Patch_SceneManagerSceneLoaded.cs
using HarmonyLib;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.Transport;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(SceneManager), "Internal_SceneLoaded")]
    internal static class Patch_SceneManagerSceneLoaded
    {
        [HarmonyPostfix]
        private static void Postfix(Scene scene, LoadSceneMode mode)
        {
            try
            {
                NetDriverShim.OnSceneLoaded(scene, mode);
            }
            catch { }

            try
            {
                LiteNetTransportRunner.HandleSceneLoaded(scene, mode);
            }
            catch { }
        }
    }
}
