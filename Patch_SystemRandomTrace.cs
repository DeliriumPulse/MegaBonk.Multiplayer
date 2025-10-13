using HarmonyLib;
using System;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(System.Random))]
    public static class Patch_SystemRandomTrace
    {
        // Trace when any System.Random constructor runs
        [HarmonyPrefix]
        [HarmonyPatch(MethodType.Constructor)]
        public static void CtorTrace(object __instance)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[SYSRNG] new System.Random() → {__instance.GetHashCode()}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[SYSRNG] Constructor trace failed: {e}");
            }
        }

        // Trace Random.Next() with no arguments
        [HarmonyPrefix]
        [HarmonyPatch("Next", new Type[] { })]
        public static void NextTrace(System.Random __instance)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[SYSRNG] {__instance.GetHashCode()}.Next()");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[SYSRNG] Next() trace failed: {e}");
            }
        }

        // Trace Random.Next(int maxValue)
        [HarmonyPrefix]
        [HarmonyPatch("Next", new Type[] { typeof(int) })]
        public static void NextMaxTrace(System.Random __instance, int maxValue)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[SYSRNG] {__instance.GetHashCode()}.Next({maxValue})");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[SYSRNG] Next(int) trace failed: {e}");
            }
        }

        // Trace Random.Next(int minValue, int maxValue)
        [HarmonyPrefix]
        [HarmonyPatch("Next", new Type[] { typeof(int), typeof(int) })]
        public static void NextRangeTrace(System.Random __instance, int minValue, int maxValue)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[SYSRNG] {__instance.GetHashCode()}.Next({minValue}, {maxValue})");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[SYSRNG] Next(int,int) trace failed: {e}");
            }
        }

        // Trace Random.NextDouble()
        [HarmonyPrefix]
        [HarmonyPatch("NextDouble")]
        public static void NextDoubleTrace(System.Random __instance)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[SYSRNG] {__instance.GetHashCode()}.NextDouble()");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[SYSRNG] NextDouble() trace failed: {e}");
            }
        }
    }
}
