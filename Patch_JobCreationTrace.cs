// File: Patch_JobCreationTrace.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_JobCreationTrace
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.FullName ?? "";

                // Skip Unity/System/CoreModule/Il2Cpp/etc. to avoid invalid metadata
                if (asmName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase))
                    continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in types)
                {
                    if (t == null || !t.Name.EndsWith("Job", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        yield return ctor;
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance)
        {
            try
            {
                MultiplayerPlugin.LogS.LogInfo($"[JOBTRACE] New job created: {__instance.GetType().FullName}");
            }
            catch { /* ignore */ }
        }
    }
}
