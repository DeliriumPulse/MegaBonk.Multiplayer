using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_RandomTrace
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Patch both UnityEngine.Random and System.Random.Next/NextDouble
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(UnityEngine.Random)))
                if (m.Name.StartsWith("Range"))
                    yield return m;

            foreach (var m in AccessTools.GetDeclaredMethods(typeof(System.Random)))
                if (m.Name.StartsWith("Next"))
                    yield return m;
        }

        [HarmonyPrefix]
        public static void Prefix(MethodBase __originalMethod)
        {
            if (Time.frameCount < 500) // only first few frames of map gen
                MultiplayerPlugin.LogS.LogInfo($"[RNG TRACE] {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}()");
        }
    }
}
