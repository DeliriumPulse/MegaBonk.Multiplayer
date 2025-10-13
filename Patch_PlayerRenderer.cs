using System;
using Assets.Scripts._Data;
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
        }
    }
}
