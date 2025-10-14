using System;
using System.Collections.Generic;
using Assets.Scripts.Actors.Player;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal sealed class AnimatorSyncSource : MonoBehaviour
    {
        private const float SEND_INTERVAL = 0.05f; // 20 Hz

        private NetDriverCore _core;
        private Animator _animator;
        private readonly List<AnimatorLayerState> _layerBuffer = new List<AnimatorLayerState>(4);
        private float _timer;
        private bool _initialized;

        internal static void Ensure(NetDriverCore core, PlayerRenderer renderer, string context)
        {
            if (core == null || renderer == null)
                return;

            var source = Il2CppComponentUtil.GetComponentCompat<AnimatorSyncSource>(renderer);
            if (source == null)
                source = renderer.gameObject.AddComponent<AnimatorSyncSource>();

            source.Configure(core, renderer, context);
        }

        private void Configure(NetDriverCore core, PlayerRenderer renderer, string context)
        {
            _core = core;
            _animator = LocateAnimator(renderer);

            if (_animator == null)
            {
                _initialized = false;
                enabled = false;
                MultiplayerPlugin.LogS?.LogWarning($"[AnimatorSyncSource] {context}: no animator found, disabling sync.");
                return;
            }

            _timer = 0f;
            _initialized = true;
            enabled = true;

            MultiplayerPlugin.LogS?.LogDebug($"[AnimatorSyncSource] {context}: tracking animator layers={_animator.layerCount}.");
        }

        private void Update()
        {
            if (!_initialized || _core == null || _animator == null)
                return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;

            _timer = SEND_INTERVAL;
            CaptureAndSend();
        }

        private void CaptureAndSend()
        {
            if (_layerBuffer.Count > 0)
                _layerBuffer.Clear();

            int layerCount = _animator.layerCount;
            for (int layer = 0; layer < layerCount; layer++)
            {
                var current = _animator.GetCurrentAnimatorStateInfo(layer);
                float weight = _animator.GetLayerWeight(layer);
                bool inTransition = _animator.IsInTransition(layer);
                int nextHash = 0;
                float nextNormalized = 0f;
                float transitionNormalized = 0f;

                if (inTransition)
                {
                    var next = _animator.GetNextAnimatorStateInfo(layer);
                    nextHash = next.fullPathHash;
                    nextNormalized = next.normalizedTime;
                    try
                    {
                        var transition = _animator.GetAnimatorTransitionInfo(layer);
                        transitionNormalized = transition.normalizedTime;
                    }
                    catch
                    {
                        transitionNormalized = 0f;
                    }
                }

                _layerBuffer.Add(new AnimatorLayerState(
                    layer,
                    current.fullPathHash,
                    current.normalizedTime,
                    weight,
                    inTransition,
                    nextHash,
                    nextNormalized,
                    transitionNormalized));
            }

            _core.SendAnimatorState(null, _layerBuffer, _animator.speed);
        }

        private static Animator LocateAnimator(PlayerRenderer renderer)
        {
            if (renderer == null)
                return null;

            Animator animator = null;
            var candidates = Il2CppComponentUtil.GetComponentsInChildrenCompat<Animator>(renderer.gameObject, includeInactive: true);
            if (candidates != null && candidates.Length > 0)
                animator = candidates[0];

            if (animator == null && renderer.rendererObject != null)
            {
                var rendererCandidates = Il2CppComponentUtil.GetComponentsInChildrenCompat<Animator>(renderer.rendererObject, includeInactive: true);
                if (rendererCandidates != null && rendererCandidates.Length > 0)
                    animator = rendererCandidates[0];
            }

            if (animator == null)
            {
                var direct = Il2CppComponentUtil.GetComponentCompat<Animator>(renderer);
                if (direct != null)
                    animator = direct;
            }

            return animator;
        }
    }
}
