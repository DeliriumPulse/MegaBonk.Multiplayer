using System;
using Assets.Scripts.Inventory.Stats;
using Assets.Scripts.Menu.Shop;
using HarmonyLib;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.GetStat))]
    internal static class Patch_PlayerStats_GetStat
    {
        [HarmonyFinalizer]
        private static Exception Finalizer(EStat stat, ref float __result, Exception __exception)
        {
            if (__exception == null)
                return null;

            float fallback = stat switch
            {
                EStat.MaxHealth => 100f,
                EStat.ChestIncreaseMultiplier => 1f,
                _ => 1f
            };

            MultiplayerPlugin.LogS?.LogDebug($"[PlayerStats] Fallback for {stat} -> {fallback} ({__exception.Message})");
            __result = fallback;
            return null;
        }
    }
}
