using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    internal static class Patch_InteractableChestSync
    {
        private const string ChestTypeName = "Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest";

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName(ChestTypeName), "Start");

        [HarmonyPrefix]
        private static void Prefix(object __instance, ref int __state)
        {
            __state = -1;
            if (Patch_DumpAndForceJobRNGs.TryGetCurrentContext(out var context))
                __state = context.CallIndex;
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, int __state)
        {
            if (__state < 0)
                return;

            if (__instance == null)
                return;

            var transformProperty = __instance.GetType().GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);
            if (transformProperty == null)
                return;

            var transform = transformProperty.GetValue(__instance) as Transform;
            if (transform == null)
                return;

            var core = MultiplayerPlugin.Driver;
            if (core != null && core.IsHost)
            {
                core.BroadcastChestState(__state, transform.rotation, transform.localScale);
            }
            else
            {
                ChestSyncRegistry.RegisterClientChest(__state, transform);
            }
        }
    }
}
