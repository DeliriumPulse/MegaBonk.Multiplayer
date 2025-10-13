using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    public class HostPawnController : MonoBehaviour
    {
        private NetDriverCore _core;
        private Transform _source;
        private float _txTimer;
        private const float TX_INTERVAL = 0.05f;  // 20 Hz
        private float _rebindTimer;
        private const float REBIND_EVERY = 0.5f;
        private float _refreshTimer;
        private const float REFRESH_EVERY = 2f;

        private void Start()
        {
            _core = MultiplayerPlugin.Driver;
            ResolveSource("start");
        }

        private void Update()
        {
            if (_core == null)
                return;

            if (_source != null)
            {
                var go = _source.gameObject;
                if (!go || !go.activeInHierarchy)
                {
                    MultiplayerPlugin.LogS?.LogInfo("[HostPawn] Source became inactive; scheduling rebind.");
                    _source = null;
                    _rebindTimer = 0f;
                }
            }

            if (_source == null)
            {
                _rebindTimer -= Time.unscaledDeltaTime;
                if (_rebindTimer <= 0f)
                {
                    _rebindTimer = REBIND_EVERY;
                    ResolveSource("rebind");
                }
                return;
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = REFRESH_EVERY;
                TryRefreshSource();
            }

            _txTimer += Time.unscaledDeltaTime;
            if (_txTimer >= TX_INTERVAL)
            {
                _txTimer = 0f;
                _core.SendPawnTransform(_source.position, _source.rotation);
            }
        }

        private void ResolveSource(string phase)
        {
            var resolved = PlayerModelLocator.Find(transform, $"HostPawn.{phase}", allowFallback: false);
            if (resolved != null)
            {
                _source = resolved;
                _refreshTimer = REFRESH_EVERY;
                if (phase == "rebind")
                    MultiplayerPlugin.LogS?.LogInfo($"[HostPawn] Rebound -> {PlayerModelLocator.Describe(_source)}");
                BroadcastAppearance();
                return;
            }

            _source = null;
            MultiplayerPlugin.LogS?.LogWarning($"[HostPawn] {phase}: locator returned null, will retry.");
        }

        private void TryRefreshSource()
        {
            var refreshed = PlayerModelLocator.Find(_source, "HostPawn.refresh", allowFallback: false);
            if (refreshed != null && refreshed != _source)
            {
                _source = refreshed;
                MultiplayerPlugin.LogS?.LogInfo($"[HostPawn] Refresh -> {PlayerModelLocator.Describe(_source)}");
                BroadcastAppearance();
            }
        }

        private void BroadcastAppearance()
        {
            if (_core == null || _source == null)
                return;

            var comps = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(_source, true);
            var skinned = comps.Length > 0 ? comps[0] : null;

            if (skinned == null || skinned.sharedMesh == null)
                return;

            var visual = skinned.transform;
            if (!visual)
                return;

            PlayerModelLocator.RegisterKnownVisual(_source, visual, "HostPawn.BroadcastAppearance");
            ModelRegistry.Register(visual);

            var path = PlayerModelLocator.GetPath(visual);
            var prefabRoot = visual.root ? visual.root : visual;
            var prefabName = prefabRoot != null ? prefabRoot.name : visual.gameObject.name;
            var appearance = new AppearanceInfo
            {
                RootPath = string.Empty,
                PrefabName = string.Empty,
                MeshName = skinned.sharedMesh.name,
                MaterialNames = Array.Empty<string>(),
                CharacterClass = string.Empty, // TODO: fill from player data if available
                CharacterId = -1,
                SkinName = string.Empty
            };

            if (SkinPrefabRegistry.TryGetDescriptor(visual, out var descriptor))
            {
                appearance.CharacterId = (int)descriptor.Character;
                appearance.SkinName = descriptor.SkinName ?? string.Empty;
            }

            var payload = AppearanceSerializer.Serialize(appearance);
            MultiplayerPlugin.LogS?.LogInfo($"[HostPawn] Broadcasting appearance: {payload}");
            _core.SendAppearanceJson(payload);
        }
    }
}
