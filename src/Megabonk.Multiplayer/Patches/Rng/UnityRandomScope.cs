#nullable enable
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityRandom = UnityEngine.Random;

namespace Megabonk.Multiplayer
{
    internal static class UnityRandomScope
    {
        private readonly record struct CallIndexKey(MethodBase Method, object? Target);

        private struct ScopeState
        {
            public bool Seeded;
            public UnityRandom.State PreviousState;
            public bool RestoreState;
        }

        private static readonly Dictionary<CallIndexKey, int> CallIndices = new();
        private static readonly object CallIndexLock = new();
        private static int _lastGlobalSeed = int.MinValue;

        [ThreadStatic] private static Stack<ScopeState>? _scopeStack;
        [ThreadStatic] private static bool _logReentry;

        internal static void Enter(MethodBase? method, object? instance, bool enableLogging = true, bool captureState = true)
        {
            var stack = _scopeStack ??= new Stack<ScopeState>();

            if (_logReentry)
            {
                stack.Push(new ScopeState { Seeded = false, RestoreState = false });
                return;
            }

            int activeSeed = Patch_ForceJobRNGs.GetActiveSeed();
            if (activeSeed == int.MinValue || method == null)
            {
                stack.Push(new ScopeState { Seeded = false, RestoreState = false });
                return;
            }

            MethodBase nonNullMethod = method;

            int callIndex;
            object? targetKey = instance;
            if (targetKey is Type && (nonNullMethod.IsStatic))
                targetKey = null;

            lock (CallIndexLock)
            {
                if (_lastGlobalSeed != activeSeed)
                {
                    CallIndices.Clear();
                    _lastGlobalSeed = activeSeed;
                }

                var key = new CallIndexKey(nonNullMethod, targetKey ?? nonNullMethod);
                CallIndices.TryGetValue(key, out callIndex);
                CallIndices[key] = callIndex + 1;
            }

            UnityRandom.State previous = default;
            if (captureState)
                previous = UnityRandom.state;

            int methodSeed = Patch_ForceJobRNGs.DeriveMethodSeed(activeSeed, method, callIndex);

            Patch_AllUnityRandomInit.SuppressOverride = true;
            try
            {
                UnityRandom.InitState(methodSeed);
            }
            finally
            {
                Patch_AllUnityRandomInit.SuppressOverride = false;
            }

            if (enableLogging && MultiplayerPlugin.VerboseJobRng)
            {
                bool prior = _logReentry;
                _logReentry = true;
                try
                {
                    string typeName = instance switch
                    {
                        null => nonNullMethod.DeclaringType?.FullName ?? "<null>",
                        Type t => t.FullName ?? t.Name,
                        _ => instance.GetType().FullName ?? "<null>"
                    };
                    MultiplayerPlugin.LogS.LogDebug(
                        $"[JOBRNG] Seeded UnityEngine.Random with {methodSeed} before {typeName}.{nonNullMethod.Name}#{callIndex}");
                }
                finally
                {
                    _logReentry = prior;
                }
            }

            stack.Push(new ScopeState
            {
                Seeded = true,
                PreviousState = previous,
                RestoreState = captureState
            });
        }

        internal static void Exit()
        {
            var stack = _scopeStack;
            if (stack == null || stack.Count == 0)
                return;

            var state = stack.Pop();
            if (!state.Seeded)
                return;

            if (state.RestoreState)
                UnityRandom.state = state.PreviousState;
        }
    }
}
