using HarmonyLib;
using System;
using System.Reflection;
using Unity.Mathematics;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_ProceduralTileJobSeed
    {
        static MethodBase TargetMethod()
        {
            // Dynamically find any 'Generate' method on ProceduralTileGeneration
            var t = AccessTools.TypeByName("ProceduralTileGeneration");
            if (t == null)
            {
                MultiplayerPlugin.LogS.LogError("[RNGSYNC] Could not find ProceduralTileGeneration type.");
                return null;
            }

            var method = AccessTools.FirstMethod(t, m => m.Name == "Generate");
            if (method == null)
            {
                MultiplayerPlugin.LogS.LogError("[RNGSYNC] Could not find Generate() method on ProceduralTileGeneration.");
                return null;
            }

            MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Targeting dynamic {t.FullName}.{method.Name}()");
            return method;
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance)
        {
            try
            {
                // Safely attempt to find a Random field on the job object
                var field = __instance.GetType().GetField("random", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null)
                {
                    MultiplayerPlugin.LogS.LogWarning($"[RNGSYNC] No 'random' field found on {__instance.GetType().Name}.");
                    return;
                }

                int forcedSeed = CoopSeedStorage.Value;
                if (forcedSeed == int.MinValue)
                    return;

                var newRand = new Unity.Mathematics.Random((uint)forcedSeed);
                field.SetValue(__instance, newRand);
                MultiplayerPlugin.LogS.LogInfo($"[RNGSYNC] Seed forced for ProceduralTileGeneration job: {forcedSeed}");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[RNGSYNC] Exception in Patch_ProceduralTileJobSeed: {e}");
            }
        }
    }
}
