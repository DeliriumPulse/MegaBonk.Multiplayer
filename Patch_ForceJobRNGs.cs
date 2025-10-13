// File: Patch_ForceJobRNGs.cs (diagnostic version)
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    public static class Patch_ForceJobRNGs
    {
        public static void ApplyToObject(object obj)
        {
            if (obj == null) return;
            int seed = CoopSeedStorage.Value != int.MinValue ? CoopSeedStorage.Value : NetDriverCore.GlobalSeed;
            if (seed == int.MinValue) return;

            MultiplayerPlugin.LogS.LogInfo($"[JOBRNG-DUMP] Inspecting {obj.GetType().FullName}");
            int changed = 0;
            ForceRandoms(obj, (uint)seed, ref changed);
            MultiplayerPlugin.LogS.LogInfo($"[JOBRNG-DUMP] → Finished {obj.GetType().FullName}, forced={changed}");
        }

        private static void ForceRandoms(object obj, uint seed, ref int changed, int depth = 0, int maxDepth = 3)
        {
            if (obj == null || depth > maxDepth) return;
            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string)) return;

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                try
                {
                    var ft = f.FieldType;
                    MultiplayerPlugin.LogS.LogInfo($"[JOBRNG-DUMP] Field {f.Name}: {ft.FullName}");
                    var val = f.GetValue(obj);
                    if (val == null) continue;

                    if (ft.FullName == "Unity.Mathematics.Random")
                    {
                        var ctor = ft.GetConstructor(new[] { typeof(uint) });
                        var newRand = ctor != null ? ctor.Invoke(new object[] { seed }) : val;
                        f.SetValue(obj, newRand);
                        changed++;
                        continue;
                    }

                    if (ft.IsArray && val is Array arr)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var elem = arr.GetValue(i);
                            if (elem == null) continue;
                            var et = elem.GetType();
                            if (et.FullName == "Unity.Mathematics.Random")
                            {
                                var ctor = et.GetConstructor(new[] { typeof(uint) });
                                var newRand = ctor != null ? ctor.Invoke(new object[] { seed }) : elem;
                                arr.SetValue(newRand, i);
                                changed++;
                            }
                            else
                            {
                                ForceRandoms(elem, seed, ref changed, depth + 1, maxDepth);
                            }
                        }
                        continue;
                    }

                    ForceRandoms(val, seed, ref changed, depth + 1, maxDepth);
                    if (ft.IsValueType) f.SetValue(obj, val);
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogWarning($"[JOBRNG-DUMP] Error inspecting {f.Name}: {e.Message}");
                }
            }
        }
    }
}
