using System;
using Assets.Scripts.Inventory.Stats;
using System;
using Assets.Scripts.Inventory__Items__Pickups;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.UpdateMaxValues))]
    internal static class Patch_PlayerHealth_UpdateMaxValues
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerHealth __instance)
        {
            if (ShouldRunVanilla(__instance))
                return true;

            ApplyFallbackStats(__instance);
            return false;
        }

        private static bool ShouldRunVanilla(PlayerHealth instance)
        {
            if (instance == null)
                return false;

            var il2CppInstance = instance as Il2CppObjectBase;

            if (RemoteStatScope.IsActive)
            {
                if (il2CppInstance != null)
                    RemoteStatRegistry.Mark(il2CppInstance);
                return false;
            }

            if (il2CppInstance != null && RemoteStatRegistry.IsRemote(il2CppInstance))
                return false;

            return AreLocalStatsReady();
        }

        private static bool AreLocalStatsReady()
        {
            try
            {
                return PlayerStats.HasStats();
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyFallbackStats(PlayerHealth instance)
        {
            if (instance == null)
                return;

            instance.maxHp = 100;
            instance.hp = 100;
            instance.maxOverheal = 0f;
            instance.overheal = 0f;
            instance.shield = 0f;
            instance.maxShield = 0f;
        }
    }
}
