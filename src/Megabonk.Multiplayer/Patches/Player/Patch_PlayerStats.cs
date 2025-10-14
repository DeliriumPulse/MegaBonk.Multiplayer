using System;
using HarmonyLib;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(Assets.Scripts.Inventory.Stats.PlayerStats), nameof(Assets.Scripts.Inventory.Stats.PlayerStats.GetStat))]
    internal static class Patch_PlayerStats
    {
        [HarmonyPrefix]
        private static bool Prefix(Assets.Scripts.Menu.Shop.EStat stat, ref float __result)
        {
            if (!RemoteStatScope.IsActive)
                return true;

            __result = RemoteStatScope.GetFallback(stat);
            MultiplayerPlugin.LogS?.LogDebug($"[PlayerStats] Remote fallback for {stat} -> {__result}");
            return false;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Assets.Scripts.Menu.Shop.EStat stat, ref float __result, Exception __exception)
        {
            if (__exception == null)
                return null;

            __result = RemoteStatScope.GetFallback(stat);
            MultiplayerPlugin.LogS?.LogWarning($"[PlayerStats] Exception for {stat}: {__exception.Message}; returning fallback {__result}");
            MultiplayerPlugin.LogS?.LogDebug($"[PlayerStats] Stack trace for {stat}:{Environment.NewLine}{Environment.StackTrace}");
            return null;
        }
    }
}
