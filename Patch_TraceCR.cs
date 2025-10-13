using HarmonyLib;
using UnityEngine;
using System.Diagnostics;
using System;
using System.Collections.Generic;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_TraceCR
    {
        [HarmonyTargetMethods]
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            // Dynamically look up the ConsistentRandom type at runtime
            var t = AccessTools.TypeByName("ConsistentRandom");
            if (t == null)
            {
                MultiplayerPlugin.LogS?.LogWarning("[RNGTRACE] Could not find ConsistentRandom type!");
                yield break;
            }

            // Patch all 'Next' overloads
            foreach (var m in AccessTools.GetDeclaredMethods(t))
                if (m.Name == "Next")
                    yield return m;
        }

        [HarmonyPrefix]
        public static void Prefix(System.Reflection.MethodBase __originalMethod)
        {
            int frame = Time.frameCount;
            var stack = new StackTrace();
            var caller = stack.GetFrame(2)?.GetMethod();
            //MultiplayerPlugin.LogS?.LogInfo(
            //    $"[RNGTRACE] {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}() called by {caller?.DeclaringType?.Name}.{caller?.Name}() at frame {frame}"
            //);
        }
    }
}
