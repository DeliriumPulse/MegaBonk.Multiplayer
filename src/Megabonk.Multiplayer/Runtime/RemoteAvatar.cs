using System;
using Assets.Scripts.Actors.Player;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    public sealed class RemoteAvatar : MonoBehaviour
    {
        private const float POSE_LERP_SPEED = 10f;
        private const float ROT_LERP_SPEED = 10f;
        private const float TIME_TOLERANCE = 0.02f;

        private Vector3 _targetPos;
        private Quaternion _targetRot;
        private float _nextRendererProbe;

        private PlayerRenderer _renderer;
        private Animator _animator;
        private AnimatorSnapshot? _pendingSnapshot;
        private int[] _stateHashes = Array.Empty<int>();
        private float[] _stateTimes = Array.Empty<float>();
        private bool[] _transitionStates = Array.Empty<bool>();
        private int[] _nextStateHashes = Array.Empty<int>();
        private float[] _nextStateTimes = Array.Empty<float>();

        private void Awake()
        {
            _targetPos = transform.position;
            _targetRot = transform.rotation;
            _nextRendererProbe = 0f;

            RemoteStatRegistry.RegisterPlayerHealth(gameObject, StatSnapshot.Empty);
        }

        public void ApplyPose(Vector3 pos, Quaternion rot)
        {
            _targetPos = pos;
            _targetRot = rot;
        }

        public void BindRenderer(PlayerRenderer renderer, string context)
        {
            if (renderer == null)
                return;

            _renderer = renderer;
            ConfigureAnimator(LocateAnimatorFromRenderer(renderer), context);
        }

        public void BindAnimatorFromRoot(Transform root, string context)
        {
            if (!root)
                root = transform;

            var animators = Il2CppComponentUtil.GetComponentsInChildrenCompat<Animator>(root, true);
            if (animators != null && animators.Length > 0)
                ConfigureAnimator(animators[0], context);
            else
                MultiplayerPlugin.LogS?.LogDebug($"[RemoteAvatar] {context}: animator not found while binding from root.");
        }

        public void EnsureAnimatorBinding(Transform root, string context)
        {
            if (_animator != null)
                return;

            if (_renderer != null)
            {
                ConfigureAnimator(LocateAnimatorFromRenderer(_renderer), context);
                if (_animator != null)
                    return;
            }

            BindAnimatorFromRoot(root ? root : transform, context);
        }

        internal void ApplyAnimatorSnapshot(AnimatorSnapshot snapshot)
        {
            if (_animator == null)
            {
                _pendingSnapshot = snapshot;
                return;
            }

            try
            {
                ApplyLayerStates(snapshot.Layers);
                _animator.speed = snapshot.Speed;
                if (snapshot.Layers.Length > 0 || snapshot.Parameters.Length > 0)
                    _animator.Update(0f);
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[RemoteAvatar] Failed to apply animator snapshot: {ex.Message}");
            }
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * POSE_LERP_SPEED);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRot, Time.deltaTime * ROT_LERP_SPEED);

            if (Time.time >= _nextRendererProbe)
            {
                _nextRendererProbe = Time.time + 1f;
                ProbeRenderers();
            }
        }

        private void ConfigureAnimator(Animator animator, string context)
        {
            if (animator == null)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[RemoteAvatar] {context}: animator not found.");
                return;
            }

            _animator = animator;

            EnsureStateBuffers(_animator.layerCount);
            MultiplayerPlugin.LogS?.LogInfo($"[RemoteAvatar] {context}: bound animator '{_animator.runtimeAnimatorController?.name ?? "<null>"}' (layers={_animator.layerCount}).");

            if (_pendingSnapshot.HasValue)
            {
                var snapshot = _pendingSnapshot.Value;
                _pendingSnapshot = null;
                ApplyAnimatorSnapshot(snapshot);
            }
        }

        private void ApplyLayerStates(AnimatorLayerState[] layers)
        {
            if (layers == null || layers.Length == 0 || _animator == null)
                return;

            EnsureStateBuffers(_animator.layerCount);

            for (int i = 0; i < layers.Length; i++)
            {
                var state = layers[i];
                int layerIndex = state.LayerIndex;
                if (layerIndex < 0 || layerIndex >= _animator.layerCount)
                    continue;

                _animator.SetLayerWeight(layerIndex, state.Weight);

                float normalizedTime = NormalizeTime(state.NormalizedTime);
                bool stateChanged = _stateHashes[layerIndex] != state.StateHash;
                bool timeChanged = Mathf.Abs(NormalizeTime(_stateTimes[layerIndex]) - normalizedTime) > TIME_TOLERANCE;
                bool transitionChanged = _transitionStates[layerIndex] != state.InTransition || _nextStateHashes[layerIndex] != state.NextStateHash;

                if (stateChanged || timeChanged || transitionChanged)
                {
                    _animator.Play(state.StateHash, layerIndex, normalizedTime);
                }

                if (state.InTransition && state.NextStateHash != 0)
                {
                    float nextTime = NormalizeTime(state.NextNormalizedTime);
                    bool nextTimeChanged = Mathf.Abs(NormalizeTime(_nextStateTimes[layerIndex]) - nextTime) > TIME_TOLERANCE;
                    if (transitionChanged || nextTimeChanged)
                        _animator.CrossFade(state.NextStateHash, 0f, layerIndex, nextTime);

                    _nextStateTimes[layerIndex] = state.NextNormalizedTime;
                }
                else
                {
                    _nextStateTimes[layerIndex] = 0f;
                }

                _stateHashes[layerIndex] = state.StateHash;
                _stateTimes[layerIndex] = state.NormalizedTime;
                _transitionStates[layerIndex] = state.InTransition;
                _nextStateHashes[layerIndex] = state.NextStateHash;
            }
        }

        private void EnsureStateBuffers(int layerCount)
        {
            if (layerCount < 0)
                layerCount = 0;

            void EnsureLength<T>(ref T[] array)
            {
                if (array.Length == layerCount)
                    return;

                var newArray = new T[layerCount];
                int copyLength = Math.Min(array.Length, layerCount);
                Array.Copy(array, newArray, copyLength);
                array = newArray;
            }

            EnsureLength(ref _stateHashes);
            EnsureLength(ref _stateTimes);
            EnsureLength(ref _transitionStates);
            EnsureLength(ref _nextStateHashes);
            EnsureLength(ref _nextStateTimes);
        }

        private void ProbeRenderers()
        {
            try
            {
                var skinned = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(transform, true);
                bool anyEnabled = false;
                bool anyActive = false;
                for (int i = 0; i < skinned.Length; i++)
                {
                    var r = skinned[i];
                    if (r == null)
                        continue;

                    if (r.gameObject.activeInHierarchy)
                        anyActive = true;

                    if (r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff)
                        anyEnabled = true;
                }

                if (!anyEnabled)
                {
                    MultiplayerPlugin.LogS?.LogWarning($"[RemoteAvatar] Renderer probe failed for {name}: active={anyActive}, skinnedCount={skinned?.Length ?? 0}");
                }
            }
            catch
            {
                // ignore probe failures
            }
        }

        private static float NormalizeTime(float value)
        {
            if (float.IsInfinity(value) || float.IsNaN(value))
                return 0f;
            return Mathf.Repeat(value, 1f);
        }

        private static Animator LocateAnimatorFromRenderer(PlayerRenderer renderer)
        {
            if (renderer == null)
                return null;

            Animator animator = null;

            try
            {
                animator = Il2CppComponentUtil.GetComponentInChildrenCompat<Animator>(renderer, true);
            }
            catch
            {
                animator = null;
            }

            if (animator == null && renderer.rendererObject != null)
            {
                try
                {
                    animator = Il2CppComponentUtil.GetComponentInChildrenCompat<Animator>(renderer.rendererObject, true);
                }
                catch
                {
                    animator = null;
                }
            }

            if (animator == null)
            {
                try
                {
                    animator = Il2CppComponentUtil.GetComponentCompat<Animator>(renderer);
                }
                catch
                {
                    animator = null;
                }
            }

            return animator;
        }
    }
}
