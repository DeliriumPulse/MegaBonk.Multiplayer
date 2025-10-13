using Assets.Scripts.Inventory__Items__Pickups;
using HarmonyLib;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.UpdateMaxValues))]
    internal static class Patch_PlayerHealth_UpdateMaxValues
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerHealth __instance)
        {
            if (!RemoteStatScope.IsActive)
                return true;

            __instance.maxHp = 100;
            __instance.hp = 100;
            __instance.maxOverheal = 0f;
            __instance.overheal = 0f;
            __instance.shield = 0f;
            __instance.maxShield = 0f;

            return false;
        }
    }
}
