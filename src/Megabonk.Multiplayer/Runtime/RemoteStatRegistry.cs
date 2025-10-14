using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Inventory__Items__Pickups;
using Assets.Scripts.Inventory.Stats;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal static class RemoteStatRegistry
    {
        private static readonly HashSet<nint> RemoteHealthPointers = new HashSet<nint>();
        private static readonly Dictionary<nint, StatSnapshot> StatsByPointer = new Dictionary<nint, StatSnapshot>();
        private static readonly object Sync = new object();
        private static readonly MethodInfo GetComponentsInChildrenMethod = typeof(GameObject).GetMethod("GetComponentsInChildren", new[] { typeof(Type), typeof(bool) });

        public static void RegisterPlayerHealth(GameObject root, StatSnapshot stats)
        {
            if (!root || GetComponentsInChildrenMethod == null)
                return;

            var snapshot = stats ?? StatSnapshot.Empty;

            try
            {
                foreach (var entry in EnumerateComponents(root, typeof(PlayerHealth)))
                    Track(entry, snapshot, true);
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[RemoteStatRegistry] Failed to register remote health components: {ex.Message}");
            }
        }

        public static void RegisterPlayerStats(GameObject root, StatSnapshot stats)
        {
            if (!root || GetComponentsInChildrenMethod == null)
                return;

            var snapshot = stats ?? StatSnapshot.Empty;

            try
            {
                foreach (var entry in EnumerateComponents(root, typeof(PlayerStats)))
                    Track(entry, snapshot, false);
                foreach (var entry in EnumerateComponents(root, typeof(Assets.Scripts.Inventory__Items__Pickups.Stats.PlayerStatsNew)))
                    Track(entry, snapshot, false);
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[RemoteStatRegistry] Failed to register player stats: {ex.Message}");
            }
        }

        public static bool IsRemote(Il2CppObjectBase candidate)
        {
            if (candidate == null)
                return false;

            lock (Sync)
                return RemoteHealthPointers.Contains(candidate.Pointer);
        }

        public static bool TryGetStats(Il2CppObjectBase candidate, out StatSnapshot stats)
        {
            stats = StatSnapshot.Empty;
            if (candidate == null)
                return false;

            lock (Sync)
                return StatsByPointer.TryGetValue(candidate.Pointer, out stats) && stats != null;
        }

        public static void Mark(Il2CppObjectBase candidate)
        {
            if (candidate == null)
                return;

            lock (Sync)
                RemoteHealthPointers.Add(candidate.Pointer);
        }

        private static IEnumerable<Il2CppObjectBase> EnumerateComponents(GameObject root, Type type)
        {
            object result;
            try
            {
                result = GetComponentsInChildrenMethod.Invoke(root, new object[] { type, true });
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[RemoteStatRegistry] Reflection invocation failed ({type.Name}): {ex.Message}");
                yield break;
            }

            if (!(result is Array array))
                yield break;

            for (int i = 0; i < array.Length; i++)
            {
                var element = array.GetValue(i);
                if (element is Il2CppObjectBase obj)
                    yield return obj;
            }
        }

        private static void Track(Il2CppObjectBase obj, StatSnapshot stats, bool markHealth)
        {
            if (obj == null)
                return;

            lock (Sync)
            {
                if (markHealth)
                    RemoteHealthPointers.Add(obj.Pointer);

                if (stats != null)
                    StatsByPointer[obj.Pointer] = stats;
            }
        }
    }
}
