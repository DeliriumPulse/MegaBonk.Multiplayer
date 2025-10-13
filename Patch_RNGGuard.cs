using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    // This universal RNG guard resets UnityEngine.Random before any generation method runs
    [HarmonyPatch]
    public static class Patch_RNGGuard
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Hook all major generation entry points
            string[] classNames =
            {
                "MapGenerator",
                "ProceduralTileGeneration",
                "GridPathGenerator",
                "Noise",
                "TextureGenerator",
                "MeshGenerator",
                "MapDisplay"
            };

            foreach (string cls in classNames)
            {
                var t = AccessTools.TypeByName(cls) ??
                        AccessTools.TypeByName("Assets.Scripts.Managers." + cls);

                if (t == null)
                {
                    MultiplayerPlugin.LogS.LogWarning($"[RNGGUARD] Type not found: {cls}");
                    continue;
                }

                // Target the most common entry functions
                string[] methods = { "Generate", "GenerateMap", "GeneratePerlinNoiseMap", "Start", "Awake" };
                foreach (string method in methods)
				{
					var candidates = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
									  .Where(x => x.Name == method);

					foreach (var m in candidates)
					{
						// Only patch parameterless or int-based overloads
						if (m.GetParameters().Length == 0 ||
							m.GetParameters().All(p => p.ParameterType == typeof(int)))
						{
							MultiplayerPlugin.LogS.LogInfo($"[RNGGUARD] Hooking {t.FullName}.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
							yield return m;
						}
					}
				}

            }
        }

        [HarmonyPrefix]
		public static void Prefix(MethodBase __originalMethod)
		{
			try
			{
				if (PlayerPrefs.HasKey("coop_seed"))
				{
					int coopSeed = PlayerPrefs.GetInt("coop_seed");
					UnityEngine.Random.InitState(coopSeed);
					MultiplayerPlugin.LogS.LogInfo($"[RNGGUARD] Re-seeding RNG → {coopSeed} before {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
				}
			}
			catch (System.Exception e)
			{
				MultiplayerPlugin.LogS.LogError($"[RNGGUARD] Exception in prefix for {__originalMethod?.Name}: {e}");
			}
		}

    }
}
