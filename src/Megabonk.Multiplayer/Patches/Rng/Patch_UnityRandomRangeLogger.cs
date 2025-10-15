using HarmonyLib;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(UnityEngine.Random))]
    internal static class Patch_UnityRandomRangeLogger
    {
        [HarmonyPatch(nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) })]
        [HarmonyPostfix]
        public static void RangeIntPostfix(int minInclusive, int maxExclusive, ref int __result)
        {
            if (!MultiplayerPlugin.VerboseJobRng)
                return;

            if (Patch_DumpAndForceJobRNGs.TryGetCurrentContext(out var context))
            {
                var method = context.Method;
                string methodName = method != null
                    ? $"{method.DeclaringType?.FullName}.{method.Name}"
                    : "<unknown>";

                MultiplayerPlugin.LogS.LogDebug(
                    $"[JOBRNG] Random.Range(int) in {methodName}#{context.CallIndex} -> args=({minInclusive},{maxExclusive}) result={__result}"
                );
            }
        }

        [HarmonyPatch(nameof(UnityEngine.Random.Range), new[] { typeof(float), typeof(float) })]
        [HarmonyPostfix]
        public static void RangeFloatPostfix(float minInclusive, float maxInclusive, ref float __result)
        {
            if (!MultiplayerPlugin.VerboseJobRng)
                return;

            if (Patch_DumpAndForceJobRNGs.TryGetCurrentContext(out var context))
            {
                var method = context.Method;
                string methodName = method != null
                    ? $"{method.DeclaringType?.FullName}.{method.Name}"
                    : "<unknown>";

                MultiplayerPlugin.LogS.LogDebug(
                    $"[JOBRNG] Random.Range(float) in {methodName}#{context.CallIndex} -> args=({minInclusive},{maxInclusive}) result={__result}"
                );
            }
        }
    }
}
