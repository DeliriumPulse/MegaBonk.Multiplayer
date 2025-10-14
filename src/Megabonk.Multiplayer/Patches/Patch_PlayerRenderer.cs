using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts._Data;
using Assets.Scripts.Actors;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Inventory__Items__Pickups;
using HarmonyLib;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(PlayerRenderer), nameof(PlayerRenderer.SetSkin), new Type[] { typeof(SkinData) })]
    internal static class Patch_PlayerRenderer
    {
        private static void Postfix(PlayerRenderer __instance, SkinData skinData)
        {
            if (__instance == null)
                return;

            try
            {
                var smr = __instance.renderer;
                if (smr == null || smr.sharedMesh == null)
                {
                    var candidates = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(__instance.transform, includeInactive: true);
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate && candidate.sharedMesh != null)
                        {
                            smr = candidate;
                            break;
                        }
                    }
                }

                if (smr == null || smr.sharedMesh == null)
                    return;

                var visual = smr.transform;
                if (!visual)
                    return;

                string context = $"PlayerRenderer.SetSkin[{skinData?.name ?? "<null>"}]";
                PlayerModelLocator.RegisterKnownVisual(__instance.transform, visual, context);
                SkinPrefabRegistry.RegisterAppearance(__instance, visual, skinData);
                SkinPrefabRegistry.RegisterSkinTemplate(__instance, skinData);

                if (__instance.rendererObject != null && __instance.rendererObject.transform && __instance.rendererObject.transform != visual)
                    PlayerModelLocator.RegisterKnownVisual(__instance.rendererObject.transform, visual, context + ".rendererObject");

                ModelRegistry.Register(visual);
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[Patch_PlayerRenderer] Postfix error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerRenderer), nameof(PlayerRenderer.SetCharacter), new Type[] { typeof(CharacterData), typeof(PlayerInventory), typeof(Vector3) })]
    internal static class Patch_PlayerRenderer_SetCharacter
    {
        private static void Postfix(PlayerRenderer __instance, CharacterData characterData)
        {
            if (__instance == null || characterData == null)
                return;

            SkinPrefabRegistry.RegisterCharacterData(characterData);
            var visual = __instance.renderer != null ? __instance.renderer.transform : __instance.transform;
            SkinPrefabRegistry.RegisterAppearance(__instance, visual, null);

            if (!RemoteStatScope.IsActive)
            {
                InputDriver.NotifyLocalCharacterSet(__instance);
                HostPawnController.NotifyLocalCharacterSet(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerRenderer))]
    internal static class Patch_PlayerRenderer_OnDamage
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = AccessTools.GetDeclaredMethods(typeof(PlayerRenderer));
            for (int i = 0; i < methods.Count; i++)
            {
                var method = methods[i];
                if (method != null && method.Name == nameof(PlayerRenderer.OnDamage))
                    yield return method;
            }
        }

        private static bool Prefix(PlayerRenderer __instance)
        {
            return PlayerRendererLayerGuard.ShouldRun(__instance);
        }
    }

    internal static class PlayerRendererLayerGuard
    {
        private static int _playerLayer = -2;

        private static int PlayerLayer
        {
            get
            {
                if (_playerLayer == -2)
                {
                    try
                    {
                        _playerLayer = LayerMask.NameToLayer("Player");
                    }
                    catch
                    {
                        _playerLayer = -1;
                    }
                }
                return _playerLayer;
            }
        }

        internal static bool ShouldRun(PlayerRenderer renderer)
        {
            if (renderer == null)
                return true;

            var go = renderer.gameObject;
            if (!go)
                return true;

            int playerLayer = PlayerLayer;
            bool isOnPlayerLayer = playerLayer >= 0 && go.layer == playerLayer;
            bool hasRemote = renderer.GetComponentInParent<RemoteAvatar>() != null;

            MultiplayerPlugin.LogS?.LogDebug($"[DamageGuard] renderer={go.name} layer={go.layer} playerLayer={PlayerLayer} remote={hasRemote}");

            if (hasRemote)
            {
                if (!isOnPlayerLayer)
                    return false;

                MultiplayerPlugin.LogS?.LogDebug($"[DamageGuard] Remote renderer '{go.name}' still on Player layer. Forcing to Default.");
                SetLayerRecursive(go.transform, LayerMask.NameToLayer("Default"));
                return false;
            }

            return true;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (!root)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child)
                    SetLayerRecursive(child, layer);
            }
        }
    }
}

