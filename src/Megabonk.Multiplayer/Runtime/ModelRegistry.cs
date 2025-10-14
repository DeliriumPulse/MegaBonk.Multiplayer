using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal readonly struct ModelTemplate
    {
        public readonly string Path;
        public readonly string PrefabName;
        public readonly GameObject Root;
        public readonly Transform Visual;

        public ModelTemplate(string path, string prefabName, GameObject root, Transform visual)
        {
            Path = path;
            PrefabName = prefabName;
            Root = root;
            Visual = visual;
        }

        public bool IsAlive => Root != null && Visual != null;
    }

    internal static class ModelRegistry
    {
        private static readonly Dictionary<string, ModelTemplate> _byPath = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, ModelTemplate> _byPrefab = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<ModelTemplate> _allTemplates = new();

        public static event Action<ModelTemplate> TemplateRegistered;

        public static IEnumerable<ModelTemplate> EnumerateTemplates()
        {
            for (int i = _allTemplates.Count - 1; i >= 0; i--)
            {
                var template = _allTemplates[i];
                if (!template.IsAlive)
                {
                    _allTemplates.RemoveAt(i);
                    continue;
                }

                yield return template;
            }
        }

        public static void Register(Transform visual)
        {
            if (!visual)
                return;

            var rootTransform = visual.root ? visual.root : visual;
            if (!rootTransform)
                return;

            var rootObject = rootTransform.gameObject;
            if (!rootObject)
                return;

            var path = PlayerModelLocator.GetPath(visual);
            var prefabName = rootObject.name ?? string.Empty;
            var template = new ModelTemplate(path, prefabName, rootObject, visual);

            if (!template.IsAlive)
                return;

            bool updated = false;

            if (!string.IsNullOrEmpty(path))
            {
                if (!_byPath.TryGetValue(path, out var existing) || !existing.IsAlive || existing.Visual != visual)
                {
                    _byPath[path] = template;
                    updated = true;
                }
            }

            if (!string.IsNullOrEmpty(prefabName))
            {
                if (!_byPrefab.TryGetValue(prefabName, out var existing) || !existing.IsAlive || existing.Visual != visual)
                {
                    _byPrefab[prefabName] = template;
                    updated = true;
                }
            }

            if (updated)
            {
                for (int i = _allTemplates.Count - 1; i >= 0; i--)
                {
                    var current = _allTemplates[i];
                    if (!current.IsAlive || current.Visual == visual || (!string.IsNullOrEmpty(path) && current.Path == path))
                        _allTemplates.RemoveAt(i);
                }

                _allTemplates.Add(template);
                TemplateRegistered?.Invoke(template);
            }
        }

        public static bool TryGetByPath(string path, out ModelTemplate template)
        {
            template = default;
            if (string.IsNullOrEmpty(path))
                return false;

            if (_byPath.TryGetValue(path, out template))
            {
                if (template.IsAlive)
                    return true;

                _byPath.Remove(path);
                template = default;
            }

            return false;
        }

        public static bool TryGetByPrefab(string prefabName, out ModelTemplate template)
        {
            template = default;
            if (string.IsNullOrEmpty(prefabName))
                return false;

            if (_byPrefab.TryGetValue(prefabName, out template))
            {
                if (template.IsAlive)
                    return true;

                _byPrefab.Remove(prefabName);
                template = default;
            }

            return false;
        }

        public static bool TryGetByMesh(string meshName, out ModelTemplate template)
        {
            template = default;
            if (string.IsNullOrEmpty(meshName))
                return false;

            for (int i = _allTemplates.Count - 1; i >= 0; i--)
            {
                var candidate = _allTemplates[i];
                if (!candidate.IsAlive)
                {
                    _allTemplates.RemoveAt(i);
                    continue;
                }

                var renderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(candidate.Visual, true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    var smr = renderers[r];
                    if (smr?.sharedMesh != null && string.Equals(smr.sharedMesh.name, meshName, StringComparison.Ordinal))
                    {
                        template = candidate;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
