using HarmonyLib;
using System;
using Unity.Mathematics;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(Unity.Mathematics.Random), MethodType.Constructor, new Type[] { typeof(uint) })]
    public static class Patch_TraceTileRNG
    {
        public static void Prefix(uint seed)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[RNGTRACE] Unity.Mathematics.Random constructed with seed={seed}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[RNGTRACE] Exception: {e}");
            }
        }
    }
}
