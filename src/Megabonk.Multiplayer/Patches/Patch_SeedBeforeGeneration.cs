using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_SeedBeforeGeneration
    {
        // Add types here as we learn them. These exist in your build/logs.
        // If any are missing at runtime, we just skip them gracefully.
        private static readonly string[] CandidateTypes = new[]
        {
            "MapGenerator",
            "Assets.Scripts.Managers.MapGenerator",
            "Assets.Scripts.Managers.MapGenerationController",
            "ProceduralTileGeneration",
            "GridPathGenerator",
            "Tile",
            "Noise",               // seen in typelist; some games tuck RNG here
            "TextureGenerator",    // perlin/texture based generation
            "MeshGenerator",       // chunk/mesh building
            "MapDisplay"           // if it drives generation pass-through
        };

        // Any function name that smells like content generation
        private static readonly string[] MethodNameHints = new[]
        {
            "Generate", "Build", "Create", "Init", "StartGeneration", "Populate", "Assemble"
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var typeName in CandidateTypes)
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) continue;

                // Grab all instance methods that look like generation steps
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                               .Where(m => MethodNameHints.Any(h => m.Name.Contains(h)));

                foreach (var m in methods)
                {
                    MultiplayerPlugin.LogS?.LogInfo($"[RNGGUARD] Hooking {t.FullName}.{m.Name}()");
                    yield return m;
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
            // Re-apply host seed right before every generation step.
            // This steamrolls whatever random reseed Unity tries behind the scenes.
            if (PlayerPrefs.HasKey("coop_seed"))
            {
                int forced = PlayerPrefs.GetInt("coop_seed");
                UnityEngine.Random.InitState(forced);
                // keep the log light; uncomment if you want spam
                // MultiplayerPlugin.LogS?.LogInfo($"[RNGGUARD] Re-applied coop seed {forced}");
            }
        }
    }
}
