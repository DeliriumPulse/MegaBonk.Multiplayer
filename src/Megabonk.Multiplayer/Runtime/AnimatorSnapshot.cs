using System;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal readonly struct AnimatorParameterState
    {
        public readonly int Hash;
        public readonly AnimatorControllerParameterType Type;
        public readonly float FloatValue;
        public readonly int IntValue;
        public readonly bool BoolValue;

        public AnimatorParameterState(int hash, AnimatorControllerParameterType type, float floatValue, int intValue, bool boolValue)
        {
            Hash = hash;
            Type = type;
            FloatValue = floatValue;
            IntValue = intValue;
            BoolValue = boolValue;
        }
    }

    internal readonly struct AnimatorLayerState
    {
        public readonly int LayerIndex;
        public readonly int StateHash;
        public readonly float NormalizedTime;
        public readonly float Weight;
        public readonly bool InTransition;
        public readonly int NextStateHash;
        public readonly float NextNormalizedTime;
        public readonly float TransitionNormalizedTime;

        public AnimatorLayerState(
            int layerIndex,
            int stateHash,
            float normalizedTime,
            float weight,
            bool inTransition,
            int nextStateHash,
            float nextNormalizedTime,
            float transitionNormalizedTime)
        {
            LayerIndex = layerIndex;
            StateHash = stateHash;
            NormalizedTime = normalizedTime;
            Weight = weight;
            InTransition = inTransition;
            NextStateHash = nextStateHash;
            NextNormalizedTime = nextNormalizedTime;
            TransitionNormalizedTime = transitionNormalizedTime;
        }
    }

    internal readonly struct AnimatorSnapshot
    {
        public readonly AnimatorParameterState[] Parameters;
        public readonly AnimatorLayerState[] Layers;
        public readonly float Speed;

        public AnimatorSnapshot(AnimatorParameterState[] parameters, AnimatorLayerState[] layers, float speed)
        {
            Parameters = parameters ?? Array.Empty<AnimatorParameterState>();
            Layers = layers ?? Array.Empty<AnimatorLayerState>();
            Speed = speed;
        }
    }
}
