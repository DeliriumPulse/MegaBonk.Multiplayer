using HarmonyLib;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_ForceTileSeed
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var allTypes = AccessTools.AllTypes();
            foreach (var t in allTypes)
            {
                if (t.FullName == null) continue;
                if (!t.FullName.Contains("MapGeneration") || !t.FullName.Contains("GenerateMap"))
                    continue;
                if (!t.FullName.Contains("d__")) continue;

                var moveNext = AccessTools.Method(t, "MoveNext");
                if (moveNext != null)
                {
                    MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Hooking coroutine: {t.FullName}.MoveNext()");
                    yield return moveNext;
                }
            }
        }

        public static void Prefix()
        {
            try
            {
                uint coopSeed = (uint)CoopSeedStorage.Value;
                MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Forcing coroutine RNG seed = {coopSeed}");
                Unity.Mathematics.Random seeded = new Unity.Mathematics.Random(coopSeed);
                UnityEngine.Random.InitState((int)coopSeed);
                MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Unity Random reinitialized with seed {coopSeed}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[RNGSYNC] Failed to set coroutine RNG seed: {e}");
            }
        }
    }
}
