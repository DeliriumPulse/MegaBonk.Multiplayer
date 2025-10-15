using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal static class ChestSyncRegistry
    {
        private struct ChestState
        {
            public Quaternion Rotation;
            public Vector3 LocalScale;
        }

        private static readonly Dictionary<int, WeakReference<Transform>> ActiveChests = new();
        private static readonly Dictionary<int, ChestState> PendingStates = new();

        public static void Reset()
        {
            ActiveChests.Clear();
            PendingStates.Clear();
        }

        public static void RegisterClientChest(int index, Transform transform)
        {
            if (transform == null)
                return;

            ActiveChests[index] = new WeakReference<Transform>(transform);

            if (PendingStates.TryGetValue(index, out var pending))
            {
                Apply(transform, pending);
                PendingStates.Remove(index);
            }
        }

        public static void ApplyFromNetwork(int index, Quaternion rotation, Vector3 localScale)
        {
            if (ActiveChests.TryGetValue(index, out var weak) && weak.TryGetTarget(out var transform) && transform != null)
            {
                Apply(transform, new ChestState { Rotation = rotation, LocalScale = localScale });
            }
            else
            {
                PendingStates[index] = new ChestState { Rotation = rotation, LocalScale = localScale };
            }
        }

        private static void Apply(Transform transform, ChestState state)
        {
            transform.rotation = state.Rotation;
            transform.localScale = state.LocalScale;
        }
    }
}
