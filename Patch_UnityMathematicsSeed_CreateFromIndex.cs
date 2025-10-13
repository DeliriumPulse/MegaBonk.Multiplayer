using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_UnityMathematicsSeed_CreateFromIndex
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("Unity.Mathematics.Random");
            if (t == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[UMATHSEED] Unity.Mathematics.Random not found; skipping CreateFromIndex patch.");
                yield break;
            }

            var m = t.GetMethod(
                "CreateFromIndex",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(uint) },
                modifiers: null
            );

            if (m == null)
            {
                MultiplayerPlugin.LogS.LogWarning("[UMATHSEED] Random.CreateFromIndex(uint) not found.");
                yield break;
            }

            MultiplayerPlugin.LogS.LogInfo($"[UMATHSEED] Hooking {m.DeclaringType.FullName}.CreateFromIndex(uint)");
            yield return m;
        }

        // IMPORTANT: replace the returned struct, don't mutate a boxed copy
        [HarmonyPostfix]
        public static void Postfix(ref object __result)
        {
            try
            {
                int forced = CoopSeedStorage.Value != int.MinValue ? CoopSeedStorage.Value : NetDriverCore.GlobalSeed;
                if (forced == int.MinValue) return;

                var t = __result.GetType(); // Unity.Mathematics.Random
                var ctor = t.GetConstructor(new[] { typeof(uint) });
                if (ctor == null)
                {
                    MultiplayerPlugin.LogS.LogWarning("[UMATHSEED] Random(uint) ctor not found; cannot replace __result.");
                    return;
                }

                // Build a brand-new Random with our exact seed and REPLACE the return value
                var replacement = ctor.Invoke(new object[] { unchecked((uint)forced) });
                __result = replacement;

                MultiplayerPlugin.LogS.LogInfo($"[UMATHSEED] Replaced Random from CreateFromIndex → state={forced}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[UMATHSEED] Exception (CreateFromIndex Postfix): {e}");
            }
        }
    }
}
