using System;
using UnityEngine;
using Assets.Scripts.Actors.Player;

namespace Megabonk.Multiplayer
{
    /// Client-side: read the actual player model transform and stream it.
    public class InputDriver : MonoBehaviour
    {
        private NetDriverCore _core;
        private Transform _source;
        private float _txTimer;
        private const float TX_INTERVAL = 0.05f;   // 20 Hz

        private float _rebindTimer;
        private const float REBIND_EVERY = 0.5f;
        private float _refreshTimer;
        private const float REFRESH_EVERY = 2f;
        private string _lastAppearancePayload;
        private static InputDriver _active;

        private void Start()
        {
            _core = MultiplayerPlugin.Driver;
            ResolveSource("start");
        }

        private void OnEnable() => _active = this;

        private void OnDisable()
        {
            if (_active == this)
                _active = null;
        }

        internal static void NotifyLocalCharacterSet(PlayerRenderer renderer)
        {
            if (_active == null || renderer == null)
                return;

            _active.OnLocalCharacterSet(renderer);
        }

        private void OnLocalCharacterSet(PlayerRenderer renderer)
        {
            // Force a fresh resolve/broadcast because the player model changed.
            _source = null;
            _rebindTimer = 0f;
            _refreshTimer = 0f;
            _txTimer = 0f;
            _lastAppearancePayload = null;
            ResolveSource("character");
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
                    MultiplayerPlugin.LogS?.LogInfo("[InputDriver] Source became inactive; scheduling rebind.");
                    _source = null;
                    _rebindTimer = 0f;
                    _lastAppearancePayload = null;
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
            if (TryResolveFromMyPlayer(phase))
                return;

            var resolved = PlayerModelLocator.Find(transform, $"InputDriver.{phase}", allowFallback: false);
            if (resolved != null)
            {
                _source = resolved;
                _refreshTimer = REFRESH_EVERY;
                MultiplayerPlugin.LogS?.LogInfo($"[InputDriver] {phase} -> {PlayerModelLocator.Describe(_source)}");
                BroadcastAppearance();
                return;
            }

            _source = null;
            MultiplayerPlugin.LogS?.LogWarning($"[InputDriver] {phase}: locator returned null, will retry.");
        }

        private void TryRefreshSource()
        {
            if (TryResolveFromMyPlayer("refresh"))
                return;

            var refreshed = PlayerModelLocator.Find(_source, "InputDriver.refresh", allowFallback: false);
            if (refreshed != null && refreshed != _source)
            {
                _source = refreshed;
                _refreshTimer = REFRESH_EVERY;
                MultiplayerPlugin.LogS?.LogInfo($"[InputDriver] Refresh -> {PlayerModelLocator.Describe(_source)}");
                BroadcastAppearance();
            }
        }

        private void BroadcastAppearance()
        {
            if (_core == null || _source == null)
                return;

            var comps = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(_source, true);
            var skinned = comps != null && comps.Length > 0 ? comps[0] : null;
            if (skinned == null || skinned.sharedMesh == null)
                return;

            var visual = skinned.transform;
            if (!visual)
                return;

            PlayerModelLocator.RegisterKnownVisual(_source, visual, "InputDriver.BroadcastAppearance");
            ModelRegistry.Register(visual);

            var appearance = new AppearanceInfo
            {
                RootPath = string.Empty,
                PrefabName = string.Empty,
                MeshName = skinned.sharedMesh.name,
                MaterialNames = Array.Empty<string>(),
                CharacterClass = string.Empty,
                CharacterId = -1,
                SkinName = string.Empty
            };

            if (!SkinPrefabRegistry.TryGetDescriptor(visual, out var descriptor))
            {
                MultiplayerPlugin.LogS?.LogDebug("[InputDriver] Skipping appearance broadcast – descriptor unavailable.");
                return;
            }

            appearance.CharacterId = (int)descriptor.Character;
            appearance.SkinName = descriptor.SkinName ?? string.Empty;

            if (descriptor.CharacterData != null)
                SkinPrefabRegistry.RegisterCharacterData(descriptor.CharacterData);

            var payload = AppearanceSerializer.Serialize(appearance);
            if (string.Equals(payload, _lastAppearancePayload, StringComparison.Ordinal))
                return;

            _lastAppearancePayload = payload;
            MultiplayerPlugin.LogS?.LogInfo($"[InputDriver] Broadcasting appearance: {payload}");
            _core.SendAppearanceJson(payload);
        }

        private bool TryResolveFromMyPlayer(string phase)
        {
            try
            {
                var myPlayer = MyPlayer.Instance;
                if (myPlayer == null)
                    return false;

                var playerRenderer = myPlayer.playerRenderer;
                if (playerRenderer == null)
                    return false;

                Transform visual = null;
                try
                {
                    if (playerRenderer.renderer != null)
                        visual = playerRenderer.renderer.transform;
                }
                catch
                {
                    // ignored
                }

                if (visual == null && playerRenderer.rendererObject != null)
                    visual = playerRenderer.rendererObject.transform;

                if (visual == null)
                    visual = playerRenderer.transform;

                if (!visual)
                    return false;

                var skinned = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(visual, true);
                if (skinned == null || skinned.Length == 0 || skinned[0] == null || skinned[0].sharedMesh == null)
                    return false;

                _source = skinned[0].transform;
                _refreshTimer = REFRESH_EVERY;
                _rebindTimer = REBIND_EVERY;
                PlayerModelLocator.RegisterKnownVisual(transform, _source, $"InputDriver.{phase}.MyPlayer");
                PlayerModelLocator.RegisterKnownVisual(playerRenderer.transform, _source, $"InputDriver.{phase}.MyPlayerRenderer");
                SkinPrefabRegistry.RegisterCharacterData(playerRenderer.characterData);
                MultiplayerPlugin.LogS?.LogInfo($"[InputDriver] {phase} (MyPlayer) -> {PlayerModelLocator.Describe(_source)}");
                BroadcastAppearance();
                return true;
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[InputDriver] Failed to resolve from MyPlayer during {phase}: {ex.Message}");
                return false;
            }
        }
    }
}
