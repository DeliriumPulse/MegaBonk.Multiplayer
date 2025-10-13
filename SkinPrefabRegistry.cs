using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts._Data;
using Assets.Scripts.Actors.Player;
using Assets.Scripts.Inventory__Items__Pickups;
using Assets.Scripts.Inventory.Stats;
using HarmonyLib;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    /// <summary>
    /// Tracks character/skin data and cached prefab templates so remote players can be reconstructed.
    /// </summary>
    internal static class SkinPrefabRegistry
    {
        private static readonly Dictionary<SkinData, GameObject> _templates = new Dictionary<SkinData, GameObject>();
        private static readonly Dictionary<ECharacter, GameObject> _characterTemplates = new Dictionary<ECharacter, GameObject>();
        private static readonly Dictionary<(ECharacter character, string skinName), SkinData> _skinByKey = new Dictionary<(ECharacter character, string skinName), SkinData>();
        private static readonly Dictionary<ECharacter, SkinData> _defaultSkin = new Dictionary<ECharacter, SkinData>();
        private static readonly Dictionary<ECharacter, CharacterData> _characterData = new Dictionary<ECharacter, CharacterData>();
        private static readonly HashSet<ECharacter> _characterLookupFailures = new HashSet<ECharacter>();
        private static readonly HashSet<string> _skinLookupFailures = new HashSet<string>();
        private static readonly Dictionary<Transform, AppearanceDescriptor> _appearanceByVisual = new Dictionary<Transform, AppearanceDescriptor>();
        private static readonly HashSet<MyButtonCharacter> _menuButtons = new HashSet<MyButtonCharacter>();

        private static GameObject _collector;
        private static bool _menuRosterPrimed;
        private static bool _menuRosterLogged;

        internal static bool MenuMetadataLogged => _menuRosterLogged;

        internal readonly struct AppearanceDescriptor
        {
            public readonly ECharacter Character;
            public readonly string SkinName;
            public readonly SkinData Skin;
            public readonly CharacterData CharacterData;

            public AppearanceDescriptor(ECharacter character, string skinName, SkinData skin, CharacterData characterData)
            {
                Character = character;
                SkinName = NormalizeSkinName(skinName);
                Skin = skin;
                CharacterData = characterData;
            }
        }

        // ------------------------------------------------------------------
        // Menu helpers
        // ------------------------------------------------------------------
        public static void CacheMenuRoster() => PrimeMenuRoster();

        public static void PrimeMenuRoster()
        {
            try
            {
                if (_menuRosterPrimed)
                    return;

                if (_menuButtons.Count == 0)
                {
                    MultiplayerPlugin.LogS?.LogDebug("[SkinRegistry] PrimeMenuRoster found no MyButtonCharacter instances.");
                    return;
                }

                var buttons = new List<MyButtonCharacter>(_menuButtons.Count);
                foreach (var button in _menuButtons)
                {
                    if (button != null)
                        buttons.Add(button);
                }

                if (buttons.Count == 0)
                {
                    MultiplayerPlugin.LogS?.LogDebug("[SkinRegistry] PrimeMenuRoster found no MyButtonCharacter instances.");
                    return;
                }

                LogMenuButtonMetadata(buttons[0]);

                int registered = 0;
                for (int i = 0; i < buttons.Count; i++)
                {
                    var button = buttons[i];
                    if (button == null || button.characterData == null)
                        continue;

                    RegisterCharacterData(button.characterData);
                    registered++;
                }

                if (registered > 0)
                {
                    _menuRosterPrimed = true;
                    MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry] Primed {registered} characters from menu roster.");
                }
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] PrimeMenuRoster failed: {ex.Message}");
            }
        }

        private static void LogMenuButtonMetadata(MyButtonCharacter sample)
        {
            if (_menuRosterLogged || sample == null)
                return;

            _menuRosterLogged = true;

            try
            {
                var type = sample.GetType();
                MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry] Menu button type: {type.FullName}");

                foreach (var field in AccessTools.GetDeclaredFields(type))
                    MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry]  field  -> {field.FieldType?.FullName} {field.Name}");

                foreach (var prop in AccessTools.GetDeclaredProperties(type))
                    MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry]  prop   -> {prop.PropertyType?.FullName} {prop.Name}");

                foreach (var method in AccessTools.GetDeclaredMethods(type))
                {
                    var parameters = method.GetParameters();
                    string signature = parameters.Length == 0
                        ? string.Empty
                        : string.Join(", ", Array.ConvertAll(parameters, p => p.ParameterType?.Name ?? "<null>"));
                    MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry]  method -> {method.Name}({signature})");
                }
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[SkinRegistry] Failed to log MyButtonCharacter metadata: {ex.Message}");
            }
        }

        public static void RegisterMenuSelection(CharacterData characterData, SkinData skinData)
        {
            if (characterData != null)
                RegisterCharacterData(characterData);

            if (skinData != null)
                RegisterSkinLookup(skinData);
            else if (characterData != null)
                ResolveSkinData(characterData.eCharacter, string.Empty);
        }

        public static void RegisterMenuButton(MyButtonCharacter button)
        {
            if (button == null)
                return;

            if (!_menuButtons.Add(button))
                return;

            if (!_menuRosterLogged)
                LogMenuButtonMetadata(button);

            try
            {
                var data = button.characterData;
                if (data != null)
                    RegisterCharacterData(data);
            }
            catch { /* best effort */ }

            _menuRosterPrimed = false;
        }

        public static void UnregisterMenuButton(MyButtonCharacter button)
        {
            if (button == null)
                return;

            _menuButtons.Remove(button);

            if (_menuButtons.Count == 0)
                _menuRosterPrimed = false;
        }

        public static SkinData TryResolveSkin(MyButtonCharacter button)
        {
            if (button == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = button.GetType();

            foreach (var field in type.GetFields(flags))
            {
                if (field == null || field.FieldType == null)
                    continue;

                if (!typeof(SkinData).IsAssignableFrom(field.FieldType))
                    continue;

                try
                {
                    var value = field.GetValue(button) as SkinData;
                    if (value != null)
                        return value;
                }
                catch { }
            }

            foreach (var prop in type.GetProperties(flags))
            {
                if (prop == null || prop.PropertyType == null || prop.GetIndexParameters().Length > 0)
                    continue;

                if (!typeof(SkinData).IsAssignableFrom(prop.PropertyType))
                    continue;

                try
                {
                    var value = prop.GetValue(button, null) as SkinData;
                    if (value != null)
                        return value;
                }
                catch { }
            }

            MultiplayerPlugin.LogS?.LogDebug($"[SkinRegistry] Could not resolve SkinData from MyButtonCharacter '{button.name}'.");
            return null;
        }

        // ------------------------------------------------------------------
        // Runtime generation helpers
        // ------------------------------------------------------------------
        public static void RegisterSkinTemplate(PlayerRenderer renderer, SkinData skin)
        {
            if (renderer == null || skin == null)
                return;

            var visual = renderer.renderer != null ? renderer.renderer.transform : null;
            if (visual == null && renderer.rendererObject != null)
                visual = renderer.rendererObject.transform;
            if (visual == null)
                visual = renderer.transform;

            if (!visual)
                return;

            RegisterAppearance(renderer, visual, skin);

            if (_templates.TryGetValue(skin, out var existing) && existing)
            {
                var existingVisual = existing.GetComponentInChildren<SkinnedMeshRenderer>(true)?.transform ?? existing.transform;
                ModelRegistry.Register(existingVisual);
                RegisterAppearance(null, existingVisual, skin);
                return;
            }

            var clone = EnsureClone(visual.gameObject, skin.name ?? "<unnamed>");
            if (clone == null)
                return;

            _templates[skin] = clone;

            var cloneVisual = clone.GetComponentInChildren<SkinnedMeshRenderer>(true)?.transform ?? clone.transform;
            ModelRegistry.Register(cloneVisual);
            PlayerModelLocator.RegisterKnownVisual(clone.transform, cloneVisual, $"SkinRegistry[{skin.name}]");
            MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry] Captured template '{skin.name}' -> {PlayerModelLocator.Describe(cloneVisual)}");

            RegisterAppearance(null, cloneVisual, skin);
        }

        public static void RegisterAppearance(PlayerRenderer renderer, Transform visual, SkinData skin)
        {
            if (!visual)
                return;

            if (skin == null)
            {
                if (renderer != null && renderer.characterData != null &&
                    _defaultSkin.TryGetValue(renderer.characterData.eCharacter, out var fallback) && fallback != null)
                {
                    skin = fallback;
                }
                else
                {
                    return;
                }
            }

            RegisterSkinLookup(skin);

            CharacterData characterData = null;
            try { characterData = renderer != null ? renderer.characterData : null; }
            catch { }

            if (characterData != null)
                _characterData[characterData.eCharacter] = characterData;

            var descriptor = new AppearanceDescriptor(skin.character, skin.name, skin, characterData);
            _appearanceByVisual[visual] = descriptor;
        }

        private static GameObject EnsureCharacterTemplate(CharacterData data)
        {
            if (data == null || data.prefab == null)
                return null;

            if (_characterTemplates.TryGetValue(data.eCharacter, out var existing) && existing)
                return existing;

            var clone = EnsureClone(data.prefab, $"CharacterPrefab[{data.eCharacter}]");
            if (clone == null)
                return null;

            _characterTemplates[data.eCharacter] = clone;

            var visual = clone.GetComponentInChildren<SkinnedMeshRenderer>(true)?.transform ?? clone.transform;
            if (visual)
            {
                ModelRegistry.Register(visual);
                PlayerModelLocator.RegisterKnownVisual(clone.transform, visual, $"SkinRegistry.Character[{data.eCharacter}]");

                SkinData baselineSkin = null;
                if (_defaultSkin.TryGetValue(data.eCharacter, out var defaultSkin) && defaultSkin != null)
                    baselineSkin = defaultSkin;
                else if (_skinByKey.TryGetValue((data.eCharacter, string.Empty), out var implicitSkin) && implicitSkin != null)
                    baselineSkin = implicitSkin;
                else
                    baselineSkin = ResolveSkinData(data.eCharacter, string.Empty);

                if (baselineSkin != null)
                    RegisterAppearance(null, visual, baselineSkin);
            }

            return clone;
        }

        private static GameObject GetCharacterTemplate(ECharacter character)
        {
            if (_characterTemplates.TryGetValue(character, out var existing) && existing)
                return existing;

            if (_characterData.TryGetValue(character, out var data) && data != null)
                return EnsureCharacterTemplate(data);

            var resolved = ResolveCharacterData(character);
            return EnsureCharacterTemplate(resolved);
        }

        private static void ApplySkinToTemplate(GameObject root, SkinData skin)
        {
            if (root == null || skin == null || skin.materials == null || skin.materials.Length == 0)
                return;

            try
            {
                var renderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(root.transform, true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var smr = renderers[i];
                    if (smr == null)
                        continue;

                    try
                    {
                        var mats = skin.materials;
                        if (mats == null || mats.Length == 0)
                            continue;

                        var cloned = new Material[mats.Length];
                        for (int m = 0; m < mats.Length; m++)
                            cloned[m] = mats[m];

                        smr.sharedMaterials = cloned;
                    }
                    catch
                    {
                        // ignored; best effort
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[SkinRegistry] Failed to apply skin materials: {ex.Message}");
            }
        }

        public static bool TryGetDescriptor(Transform visual, out AppearanceDescriptor descriptor)
        {
            if (visual != null && _appearanceByVisual.TryGetValue(visual, out descriptor))
                return true;

            if (visual != null && visual.parent != null && _appearanceByVisual.TryGetValue(visual.parent, out descriptor))
                return true;

            descriptor = default;
            return false;
        }

        public static bool TryCreateRemoteAvatar(ECharacter character, string skinName, Vector3 position, Quaternion rotation, ulong peerId, out GameObject avatarRoot, out PlayerRenderer renderer)
        {
            avatarRoot = null;
            renderer = null;

            if (!TryGetCharacterData(character, out var characterData) || characterData == null)
            {
                MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] Missing CharacterData for {character}");
                return false;
            }

            var normalizedSkin = NormalizeSkinName(skinName);
            SkinData skin = null;

            if (!string.IsNullOrEmpty(normalizedSkin) && _skinByKey.TryGetValue((character, normalizedSkin), out var storedSkin) && storedSkin != null)
                skin = storedSkin;

            if (skin == null && _defaultSkin.TryGetValue(character, out var fallbackSkin))
            {
                skin = fallbackSkin;
                normalizedSkin = NormalizeSkinName(fallbackSkin?.name);
            }

            if (skin == null)
            {
                skin = ResolveSkinData(character, normalizedSkin);
                if (skin != null)
                    normalizedSkin = NormalizeSkinName(skin.name);
            }

            var effectiveSkin = skin ?? (_defaultSkin.TryGetValue(character, out var defaultSkin) ? defaultSkin : null);

            EnsureCharacterTemplate(characterData);

            if (TryCreateRemoteAvatarViaRenderer(character, characterData, effectiveSkin, normalizedSkin, position, rotation, peerId, out avatarRoot, out renderer))
                return true;

            try
            {
                GameObject template = null;
                if (effectiveSkin != null && TryGetTemplate(effectiveSkin, out var skinTemplate))
                {
                    template = skinTemplate;
                }
                else if (TryGetTemplate(character, normalizedSkin, out var characterTemplate))
                {
                    template = characterTemplate;
                }
                else if (TryGetTemplate(character, string.Empty, out var fallbackTemplate))
                {
                    template = fallbackTemplate;
                }
                else
                {
                    template = GetCharacterTemplate(character);
                }

                if (template == null)
                {
                    MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] No template available for {character}/{normalizedSkin}; remote avatar will not be created.");
                    return false;
                }

                var root = new GameObject($"RemotePlayer_{peerId}");
                root.transform.SetPositionAndRotation(position, rotation);

                var rendererContainer = UnityEngine.Object.Instantiate(template, root.transform, false);
                rendererContainer.name = "Renderer";
                rendererContainer.SetActive(true);
                rendererContainer.transform.localPosition = Vector3.zero;
                rendererContainer.transform.localRotation = Quaternion.identity;
                EnableRenderStack(rendererContainer);

                var playerRenderer = rendererContainer.GetComponentInChildren<PlayerRenderer>(true) ?? rendererContainer.GetComponent<PlayerRenderer>();

                Transform visual = null;
                Transform locatorRoot = rendererContainer.transform;

                if (playerRenderer != null)
                {
                    locatorRoot = playerRenderer.transform;
                    visual = playerRenderer.renderer != null
                        ? playerRenderer.renderer.transform
                        : playerRenderer.rendererObject != null
                            ? playerRenderer.rendererObject.transform
                            : playerRenderer.transform;
                }
                else
                {
                    visual = rendererContainer.transform;
                }

                if (visual && effectiveSkin != null)
                {
                    RegisterAppearance(null, visual, effectiveSkin);
                    PlayerModelLocator.RegisterKnownVisual(locatorRoot, visual, $"RemoteSpawn[{character}]");
                }
                else if (visual)
                {
                    PlayerModelLocator.RegisterKnownVisual(locatorRoot, visual, $"RemoteSpawn[{character}]");
                }

                if (effectiveSkin != null)
                    ApplySkinToTemplate(rendererContainer, effectiveSkin);

                RemovePhysicsComponents(root.transform);

                avatarRoot = root;
                renderer = playerRenderer;
                return true;
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] Failed to create remote avatar for {character}: {ex.Message}");
                return false;
            }
        }

        public static bool TryGetTemplate(SkinData skin, out GameObject template)
        {
            if (skin != null && _templates.TryGetValue(skin, out var stored) && stored)
            {
                template = stored;
                return true;
            }

            template = null;
            return false;
        }

        public static bool TryGetTemplate(ECharacter character, string skinName, out GameObject template)
        {
            skinName = NormalizeSkinName(skinName);

            if (_skinByKey.TryGetValue((character, skinName), out var skin) && skin != null)
                return TryGetTemplate(skin, out template);

            if (_defaultSkin.TryGetValue(character, out var defaultSkin) && TryGetTemplate(defaultSkin, out template))
                return true;

            var resolved = ResolveSkinData(character, skinName);
            if (resolved != null)
                return TryGetTemplate(resolved, out template);

            template = null;
            return false;
        }

        public static bool TryGetCharacterData(ECharacter character, out CharacterData data)
        {
            data = ResolveCharacterData(character);
            return data != null;
        }

        public static void RegisterCharacterData(CharacterData data)
        {
            if (data == null)
                return;

            _characterData[data.eCharacter] = data;
            MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry] Registered CharacterData for {data.eCharacter} ({data.name})");
            EnsureCharacterTemplate(data);
        }

        public static string NormalizeSkinNamePublic(string skinName) => NormalizeSkinName(skinName);

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static void EnsureCollector()
        {
            if (_collector != null)
                return;

            _collector = new GameObject("Megabonk.SkinTemplates");
            UnityEngine.Object.DontDestroyOnLoad(_collector);
            _collector.hideFlags = HideFlags.HideAndDontSave;
        }

        private static GameObject EnsureClone(GameObject source, string context)
        {
            if (!source)
                return null;

            EnsureCollector();

            try
            {
                var clone = UnityEngine.Object.Instantiate(source, _collector.transform, false);
                clone.name = source.name;
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.SetActive(false);

                foreach (var animator in Il2CppComponentUtil.GetComponentsInChildrenCompat<Animator>(clone.transform, true))
                    animator.enabled = false;

                foreach (var rb in Il2CppComponentUtil.GetComponentsInChildrenCompat<Rigidbody>(clone.transform, true))
                    UnityEngine.Object.DestroyImmediate(rb);

                foreach (var col in Il2CppComponentUtil.GetComponentsInChildrenCompat<Collider>(clone.transform, true))
                    UnityEngine.Object.DestroyImmediate(col);

                return clone;
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] Failed to clone '{source?.name}' ({context}): {ex.Message}");
                return null;
            }
        }

        private static void RemovePhysicsComponents(Transform root)
        {
            if (!root)
                return;

            var colliders = Il2CppComponentUtil.GetComponentsInChildrenCompat<Collider>(root, true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i])
                    UnityEngine.Object.Destroy(colliders[i]);

            var rigidbodies = Il2CppComponentUtil.GetComponentsInChildrenCompat<Rigidbody>(root, true);
            for (int i = 0; i < rigidbodies.Length; i++)
                if (rigidbodies[i])
                    UnityEngine.Object.Destroy(rigidbodies[i]);
        }

        private static void RegisterSkinLookup(SkinData skin)
        {
            if (skin == null)
                return;

            var character = skin.character;
            var normalized = NormalizeSkinName(skin.name);

            _skinByKey[(character, normalized)] = skin;

            if (!string.IsNullOrEmpty(skin.name) && !string.Equals(skin.name, normalized, StringComparison.Ordinal))
                _skinByKey[(character, skin.name)] = skin;

            if (!_defaultSkin.ContainsKey(character))
                _defaultSkin[character] = skin;
        }

        private static string NormalizeSkinName(string skinName)
        {
            if (string.IsNullOrEmpty(skinName))
                return string.Empty;

            return skinName.Replace("(Clone)", string.Empty, StringComparison.Ordinal).Trim();
        }

        private static CharacterData ResolveCharacterData(ECharacter character)
        {
            if (_characterData.TryGetValue(character, out var data) && data != null)
                return data;

            try
            {
                var dm = DataManager.Instance;
                if (dm != null && dm.characterData != null && dm.characterData.TryGetValue(character, out var dmData) && dmData != null)
                {
                    _characterData[character] = dmData;
                    return dmData;
                }
            }
            catch (Exception ex)
            {
                if (_characterLookupFailures.Add(character))
                    MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] Failed to pull CharacterData for {character} from DataManager: {ex.Message}");
            }

            if (_characterLookupFailures.Add(character))
                MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] CharacterData still missing for {character}");

            return null;
        }

        private static SkinData ResolveSkinData(ECharacter character, string skinName)
        {
            var key = $"{character}:{skinName}";

            try
            {
                var dm = DataManager.Instance;
                if (dm != null && dm.skinData != null && dm.skinData.TryGetValue(character, out var list) && list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var candidate = list[i];
                        if (candidate == null)
                            continue;

                        var name = NormalizeSkinName(candidate.name);
                        _skinByKey[(character, name)] = candidate;

                        if (!_defaultSkin.ContainsKey(character))
                            _defaultSkin[character] = candidate;

                        if (string.IsNullOrEmpty(skinName) || string.Equals(name, skinName, StringComparison.OrdinalIgnoreCase))
                            return candidate;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_skinLookupFailures.Add(key))
                    MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] Failed to pull SkinData for {character}/{skinName}: {ex.Message}");
            }

            if (_skinLookupFailures.Add(key))
                MultiplayerPlugin.LogS?.LogWarning($"[SkinRegistry] SkinData still missing for {character}/{skinName}");

            return null;
        }

        internal static void EnableRenderStack(GameObject root)
        {
            if (!root)
                return;

            int animatorTotal = 0;
            int skinnedTotal = 0;
            int skinnedEnabled = 0;
            int meshTotal = 0;
            int meshEnabled = 0;
            int playerRendererTotal = 0;

            List<string> disabledNotes = null;
            List<string> skinnedDetails = null;
            List<string> meshDetails = null;

            var animators = Il2CppComponentUtil.GetComponentsInChildrenCompat<Animator>(root, true);
            for (int i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (!animator)
                    continue;
                animatorTotal++;
                if (!animator.enabled)
                    animator.enabled = true;
                if (!animator.gameObject.activeSelf)
                    animator.gameObject.SetActive(true);
                if (animator.updateMode != AnimatorUpdateMode.Normal)
                    animator.updateMode = AnimatorUpdateMode.Normal;
            }

            var skinnedRenderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(root, true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                var renderer = skinnedRenderers[i];
                if (!renderer)
                    continue;
                skinnedTotal++;
                if (!renderer.gameObject.activeSelf)
                    renderer.gameObject.SetActive(true);
                if (!renderer.enabled)
                {
                    renderer.enabled = true;
                    skinnedEnabled++;
                }
                else
                {
                    skinnedEnabled++;
                }

                if (renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy)
                {
                    disabledNotes ??= new List<string>();
                    disabledNotes.Add($"SkinnedMesh[{renderer.name}] active={renderer.gameObject.activeInHierarchy} forceOff={renderer.forceRenderingOff}");
                }
                renderer.forceRenderingOff = false;
                renderer.updateWhenOffscreen = true;

                if (MultiplayerPlugin.LogS != null)
                {
                    skinnedDetails ??= new List<string>();
                    var meshName = renderer.sharedMesh ? renderer.sharedMesh.name : "<null>";
                    var bounds = renderer.bounds.size;
                    skinnedDetails.Add($"{DescribeRelative(root.transform, renderer.transform)} mesh={meshName} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} forceOff={renderer.forceRenderingOff} offscreen={renderer.updateWhenOffscreen} layer={renderer.gameObject.layer} bounds=({bounds.x:F2},{bounds.y:F2},{bounds.z:F2})");
                }
            }

            var meshRenderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<MeshRenderer>(root, true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var renderer = meshRenderers[i];
                if (!renderer)
                    continue;
                meshTotal++;
                if (!renderer.gameObject.activeSelf)
                    renderer.gameObject.SetActive(true);
                if (!renderer.enabled)
                {
                    renderer.enabled = true;
                    meshEnabled++;
                }
                else
                {
                    meshEnabled++;
                }

                if (renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy)
                {
                    disabledNotes ??= new List<string>();
                    disabledNotes.Add($"Mesh[{renderer.name}] active={renderer.gameObject.activeInHierarchy} forceOff={renderer.forceRenderingOff}");
                }
                renderer.forceRenderingOff = false;
                renderer.receiveShadows = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                if (MultiplayerPlugin.LogS != null)
                {
                    meshDetails ??= new List<string>();
                    var bounds = renderer.bounds.size;
                    meshDetails.Add($"{DescribeRelative(root.transform, renderer.transform)} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} forceOff={renderer.forceRenderingOff} layer={renderer.gameObject.layer} bounds=({bounds.x:F2},{bounds.y:F2},{bounds.z:F2})");
                }
            }

            var playerRenderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<PlayerRenderer>(root, true);
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                var playerRenderer = playerRenderers[i];
                if (!playerRenderer)
                    continue;
                playerRendererTotal++;
                if (!playerRenderer.enabled)
                    playerRenderer.enabled = true;
                if (!playerRenderer.gameObject.activeSelf)
                    playerRenderer.gameObject.SetActive(true);
            }

            if (MultiplayerPlugin.LogS != null)
            {
                var note = disabledNotes != null && disabledNotes.Count > 0
                    ? $" issues=[{string.Join("; ", disabledNotes)}]"
                    : string.Empty;
                MultiplayerPlugin.LogS.LogInfo($"[SkinRegistry] EnableRenderStack {root.name}: animators={animatorTotal}, skinned={skinnedTotal}/{skinnedEnabled}, mesh={meshTotal}/{meshEnabled}, playerRenderers={playerRendererTotal}{note}");

                if (skinnedDetails != null)
                    MultiplayerPlugin.LogS.LogDebug($"[SkinRegistry] Skinned details -> {string.Join(" | ", skinnedDetails)}");
                if (meshDetails != null)
                    MultiplayerPlugin.LogS.LogDebug($"[SkinRegistry] Mesh details -> {string.Join(" | ", meshDetails)}");
            }
        }

        private static bool TryCreateRemoteAvatarViaRenderer(ECharacter character, CharacterData characterData, SkinData skin, string skinName, Vector3 position, Quaternion rotation, ulong peerId, out GameObject avatarRoot, out PlayerRenderer renderer)
        {
            avatarRoot = null;
            renderer = null;

            GameObject root = null;
            GameObject rendererContainer = null;

            try
            {
                root = new GameObject($"RemotePlayer_{peerId}");
                root.transform.SetPositionAndRotation(position, rotation);

                rendererContainer = new GameObject("Renderer");
                rendererContainer.transform.SetParent(root.transform, false);
                rendererContainer.transform.localPosition = Vector3.zero;
                rendererContainer.transform.localRotation = Quaternion.identity;

                var playerRenderer = rendererContainer.AddComponent<PlayerRenderer>();
                if (playerRenderer == null)
                {
                    UnityEngine.Object.Destroy(root);
                    return false;
                }

                var inventory = new PlayerInventory(characterData);
                playerRenderer.SetCharacter(characterData, inventory, position);
                playerRenderer.CreateMaterials(4);

                if (skin != null)
                    playerRenderer.SetSkin(skin);

                rendererContainer.transform.localPosition = new Vector3(0f, -(characterData.colliderHeight / 2f), 0f);
                rendererContainer.transform.localRotation = Quaternion.identity;

                EnableRenderStack(rendererContainer);
                RemovePhysicsComponents(root.transform);

                try
                {
                    if (skin != null)
                        RegisterSkinTemplate(playerRenderer, skin);
                    else if (_defaultSkin.TryGetValue(character, out var captureSkin) && captureSkin != null)
                        RegisterSkinTemplate(playerRenderer, captureSkin);
                }
                catch (Exception templateEx)
                {
                    MultiplayerPlugin.LogS?.LogDebug($"[SkinRegistry] Template capture during renderer path failed: {templateEx.Message}");
                }

                avatarRoot = root;
                renderer = playerRenderer;
                MultiplayerPlugin.LogS?.LogInfo($"[SkinRegistry] Spawned PlayerRenderer avatar for {character}/{skinName}");
                return true;
            }
            catch (Exception ex)
            {
                if (rendererContainer != null)
                    UnityEngine.Object.Destroy(rendererContainer);

                if (root != null)
                    UnityEngine.Object.Destroy(root);

                MultiplayerPlugin.LogS?.LogDebug($"[SkinRegistry] PlayerRenderer path failed for {character}/{skinName}: {ex.Message}");
                avatarRoot = null;
                renderer = null;
                return false;
            }
        }

        private static string DescribeRelative(Transform root, Transform target)
        {
            if (!target)
                return "<missing>";

            if (!root)
                return target.name;

            if (target == root)
                return root.name;

            var list = new List<string>();
            var current = target;
            while (current && current != root)
            {
                list.Insert(0, current.name);
                current = current.parent;
            }

            if (current == root)
                list.Insert(0, root.name);
            else
                list.Insert(0, "<root?>");

            return string.Join("/", list);
        }

    }
}
