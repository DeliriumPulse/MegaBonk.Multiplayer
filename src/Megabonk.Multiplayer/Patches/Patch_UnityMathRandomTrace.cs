using HarmonyLib;
using System;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_UnityMathRandomTrace
    {
        // Dynamically target Unity.Mathematics.Random if it exists
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("Unity.Mathematics.Random");
            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[UMATHRNG] Unity.Mathematics.Random not found.");
                yield break;
            }

            // Log every public instance method to see what gets called
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name.StartsWith("Next") || m.Name.StartsWith("NextUInt") || m.Name.StartsWith("NextInt"))
                {
                    MultiplayerPlugin.LogS.LogInfo($"[UMATHRNG] Hooking {m.DeclaringType.Name}.{m.Name}()");
                    yield return m;
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance, MethodBase __originalMethod)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[UMATHRNG] {__originalMethod.Name}() called on {__instance?.GetType().Name}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[UMATHRNG] Trace failed: {e}");
            }
        }
    }
}
