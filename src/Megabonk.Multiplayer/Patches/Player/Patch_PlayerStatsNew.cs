using HarmonyLib;
using Assets.Scripts.Inventory__Items__Pickups.Stats;
using Assets.Scripts.Menu.Shop;
using Il2CppInterop.Runtime.InteropTypes;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(PlayerStatsNew), nameof(PlayerStatsNew.GetStat))]
    internal static class Patch_PlayerStatsNew
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerStatsNew __instance, EStat stat, ref float __result)
        {
            if (__instance == null)
                return true;

            var obj = __instance as Il2CppObjectBase;
            if (obj != null && RemoteStatRegistry.TryGetStats(obj, out var snapshot) && snapshot != null && snapshot.TryGet(stat, out var value))
            {
                __result = value;
                return false;
            }

            return true;
        }

        [HarmonyFinalizer]
        private static System.Exception Finalizer(PlayerStatsNew __instance, EStat stat, ref float __result, System.Exception __exception)
        {
            if (__exception == null)
                return null;

            var obj = __instance as Il2CppObjectBase;
            if (obj != null && RemoteStatRegistry.TryGetStats(obj, out var snapshot) && snapshot != null && snapshot.TryGet(stat, out var value))
            {
                __result = value;
                MultiplayerPlugin.LogS?.LogWarning($"[PlayerStatsNew] Exception for {stat}: {__exception.Message}; returning snapshot {value}");
                return null;
            }

            return __exception;
        }
    }
}
