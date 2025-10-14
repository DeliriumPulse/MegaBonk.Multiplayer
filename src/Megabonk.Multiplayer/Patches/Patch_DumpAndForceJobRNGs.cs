// File: Patch_DumpAndForceJobRNGs.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_DumpAndForceJobRNGs
    {
        [HarmonyTargetMethods]
		public static IEnumerable<MethodBase> TargetMethods()
		{
			string[] nameFragments =
			{
				"ProceduralTileGeneration",
				"GridPathGenerator",
				"Noise",
				"TextureGenerator",
				"MeshGenerator",
				"MapGenerator",
				"MapDisplay"
			};

			string[] methodNames =
			{
				"Generate", "Schedule", "Create", "CreateJobs",
				"Run", "Start", "Execute", "GenerateMap"
			};

			// only patch inside your main game assembly
			var gameAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a =>
				{
					var n = a.GetName().Name ?? "";
					return n.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase)
						|| n.Contains("Megabonk", StringComparison.OrdinalIgnoreCase)
						|| n.Contains("Ved", StringComparison.OrdinalIgnoreCase);
				});

			foreach (var asm in gameAssemblies)
			{
				Type[] allTypes;
				try { allTypes = asm.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(x => x != null).ToArray(); }

				foreach (var type in allTypes)
				{
					if (type == null || string.IsNullOrEmpty(type.FullName))
						continue;
					if (!nameFragments.Any(f => type.FullName.Contains(f, StringComparison.OrdinalIgnoreCase)))
						continue;
					if (type.ContainsGenericParameters)
						continue;

					MethodInfo[] allMethods;
					try { allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
					catch { continue; }

					foreach (var method in allMethods)
					{
						if (method == null || method.IsSpecialName)
							continue;
						if (!methodNames.Contains(method.Name))
							continue;
						if (method.ContainsGenericParameters)
							continue;

						MultiplayerPlugin.LogS.LogInfo($"[JOBRNG] Hooking {type.FullName}.{method.Name}()");
						yield return method;
					}
				}
			}
		}


        [HarmonyPrefix]
        public static void Prefix(object __instance)
        {
            try
            {
                Patch_ForceJobRNGs.ApplyToObject(__instance);
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[JOBRNG] Error forcing job RNGs: {e}");
            }
        }
    }
}
