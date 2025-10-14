// File: Patch_ForceJobRNGs.cs
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    public static class Patch_ForceJobRNGs
    {
        public static void ApplyToObject(object obj)
        {
            if (obj == null)
                return;

            int seed = CoopSeedStorage.Value != int.MinValue
                ? CoopSeedStorage.Value
                : NetDriverCore.GlobalSeed;

            if (seed == int.MinValue)
                return;

            int changed = 0;
            ForceRandoms(obj, (uint)seed, ref changed);

            if (changed > 0)
                MultiplayerPlugin.LogS.LogInfo($"[JOBRNG] Seeded {changed} random fields on {obj.GetType().FullName}");
        }

        private static void ForceRandoms(object obj, uint seed, ref int changed, int depth = 0, int maxDepth = 3)
        {
            if (obj == null || depth > maxDepth)
                return;

            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string))
                return;

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                object value;
                try
                {
                    value = field.GetValue(obj);
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Failed to read {type.FullName}.{field.Name}: {e.Message}");
                    continue;
                }

                if (value == null)
                    continue;

                try
                {
                    if (TryForceRandomLike(ref value, seed, ref changed))
                    {
                        field.SetValue(obj, value);
                        continue;
                    }

                    if (value is Array array && field.FieldType.IsArray)
                    {
                        ForceArrayRandoms(array, seed, ref changed, depth, maxDepth);
                        continue;
                    }

                    if (value is IList list && value is not string)
                    {
                        ForceListRandoms(list, seed, ref changed, depth, maxDepth);
                        continue;
                    }

                    ForceRandoms(value, seed, ref changed, depth + 1, maxDepth);

                    if (field.FieldType.IsValueType)
                        field.SetValue(obj, value);
                }
                catch (Exception e)
                {
                    MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Error processing {type.FullName}.{field.Name}: {e.Message}");
                }
            }
        }

        private static void ForceArrayRandoms(Array array, uint seed, ref int changed, int depth, int maxDepth)
        {
            if (array == null)
                return;

            for (int i = 0; i < array.Length; i++)
            {
                object element = array.GetValue(i);
                if (element == null)
                    continue;

                if (TryForceRandomLike(ref element, seed, ref changed))
                {
                    array.SetValue(element, i);
                    continue;
                }

                ForceRandoms(element, seed, ref changed, depth + 1, maxDepth);
                array.SetValue(element, i);
            }
        }

        private static void ForceListRandoms(IList list, uint seed, ref int changed, int depth, int maxDepth)
        {
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                if (element == null)
                    continue;

                if (TryForceRandomLike(ref element, seed, ref changed))
                {
                    list[i] = element;
                    continue;
                }

                ForceRandoms(element, seed, ref changed, depth + 1, maxDepth);
                list[i] = element;
            }
        }

        private static bool TryForceRandomLike(ref object value, uint seed, ref int changed)
        {
            if (value == null)
                return false;

            var type = value.GetType();
            string fullName = type.FullName ?? string.Empty;
            int seedInt = unchecked((int)seed);

            try
            {
                if (fullName == "Unity.Mathematics.Random")
                {
                    var ctor = type.GetConstructor(new[] { typeof(uint) });
                    if (ctor != null)
                    {
                        value = ctor.Invoke(new object[] { seed });
                        changed++;
                        return true;
                    }
                }

                if (fullName == "System.Random" || fullName == "Il2CppSystem.Random" || type.Name == "Random")
                {
                    var ctor = type.GetConstructor(new[] { typeof(int) });
                    if (ctor != null)
                    {
                        value = ctor.Invoke(new object[] { seedInt });
                        changed++;
                        return true;
                    }
                }

                if (type.Name.Contains("ConsistentRandom") || fullName == "ConsistentRandom")
                {
                    if (TrySeedViaCommonMembers(value, seed, seedInt))
                    {
                        changed++;
                        return true;
                    }
                }

                if (type.Name.Contains("MyRandom") || fullName == "Utility.MyRandom")
                {
                    if (TrySeedViaCommonMembers(value, seed, seedInt))
                    {
                        changed++;
                        return true;
                    }
                }

                if ((fullName.Contains("Random") || type.Name.Contains("Random")) &&
                    !(type.Namespace?.StartsWith("UnityEngine", StringComparison.Ordinal) ?? false) &&
                    !(type.Namespace?.StartsWith("System", StringComparison.Ordinal) ?? false))
                {
                    if (TrySeedViaCommonMembers(value, seed, seedInt))
                    {
                        changed++;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Failed to seed {fullName}: {e.Message}");
            }

            return false;
        }

        private static bool TrySeedViaCommonMembers(object instance, uint seed, int seedInt)
        {
            if (instance == null)
                return false;

            var type = instance.GetType();

            try
            {
                if (TryInvokeSeedMethod(type, instance, "SetSeed", seed, seedInt))
                    return true;

                if (TryInvokeSeedMethod(type, instance, "Init", seed, seedInt))
                    return true;

                if (TryInvokeSeedMethod(type, instance, "Initialize", seed, seedInt))
                    return true;

                var prop = type.GetProperty("Seed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite)
                {
                    if (prop.PropertyType == typeof(int))
                    {
                        prop.SetValue(instance, seedInt);
                        return true;
                    }
                    if (prop.PropertyType == typeof(uint))
                    {
                        prop.SetValue(instance, seed);
                        return true;
                    }
                }

                foreach (var fieldName in new[] { "seed", "_seed", "Seed" })
                {
                    var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field == null)
                        continue;

                    if (field.FieldType == typeof(int))
                    {
                        field.SetValue(instance, seedInt);
                        return true;
                    }
                    if (field.FieldType == typeof(uint))
                    {
                        field.SetValue(instance, seed);
                        return true;
                    }
                    if (field.FieldType.FullName == "System.Random" || field.FieldType == typeof(System.Random))
                    {
                        var rng = Activator.CreateInstance(field.FieldType, new object[] { seedInt });
                        field.SetValue(instance, rng);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Reflection failed on {type.FullName}: {e.Message}");
            }

            return false;
        }

        private static bool TryInvokeSeedMethod(Type type, object instance, string methodName, uint seed, int seedInt)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var methodInt = type.GetMethod(methodName, flags, null, new[] { typeof(int) }, null);
            if (methodInt != null)
            {
                methodInt.Invoke(instance, new object[] { seedInt });
                return true;
            }

            var methodUint = type.GetMethod(methodName, flags, null, new[] { typeof(uint) }, null);
            if (methodUint != null)
            {
                methodUint.Invoke(instance, new object[] { seed });
                return true;
            }

            return false;
        }
    }
}
