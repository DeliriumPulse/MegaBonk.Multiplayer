// File: RemoteAvatar.cs
using UnityEngine;

namespace Megabonk.Multiplayer
{
    public sealed class RemoteAvatar : MonoBehaviour
    {
        private Vector3 _targetPos;
        private Quaternion _targetRot;
        private float _nextRendererProbe;

        private void Awake()
        {
            _targetPos = transform.position;
            _targetRot = transform.rotation;
            _nextRendererProbe = 0f;
        }

        public void ApplyPose(Vector3 pos, Quaternion rot)
        {
            _targetPos = pos;
            _targetRot = rot;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRot, Time.deltaTime * 10f);

            if (Time.time >= _nextRendererProbe)
            {
                _nextRendererProbe = Time.time + 1f;
                ProbeRenderers();
            }
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
    }
}
