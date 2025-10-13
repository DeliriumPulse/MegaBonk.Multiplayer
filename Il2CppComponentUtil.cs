using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal static class Il2CppComponentUtil
    {
        public static T[] GetComponentsInChildrenCompat<T>(Transform root, bool includeInactive) where T : Component
        {
            if (root == null)
                return Array.Empty<T>();

            var results = new List<T>();
            Traverse(root, includeInactive, results);
            return results.ToArray();
        }

        public static T[] GetComponentsInChildrenCompat<T>(GameObject root, bool includeInactive) where T : Component
        {
            return root ? GetComponentsInChildrenCompat<T>(root.transform, includeInactive) : Array.Empty<T>();
        }

        public static Transform[] GetTransformsInChildrenCompat(Transform root, bool includeInactive)
        {
            if (root == null)
                return Array.Empty<Transform>();

            var results = new List<Transform>();
            TraverseTransforms(root, includeInactive, results);
            return results.ToArray();
        }

        public static T[] FindObjectsOfTypeCompat<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
            var type = typeof(T);
            if (type == typeof(Animator) ||
                type == typeof(SkinnedMeshRenderer) ||
                type == typeof(MeshRenderer) ||
                type == typeof(Transform) ||
                type == typeof(GameObject))
            {
                return CollectFromRegistry<T>(includeInactive);
            }

            return Array.Empty<T>();
        }

        private static T[] CollectFromRegistry<T>(bool includeInactive) where T : UnityEngine.Object
        {
            var results = new List<T>();
            var seen = new HashSet<UnityEngine.Object>();
            var targetType = typeof(T);

            foreach (var template in ModelRegistry.EnumerateTemplates())
            {
                if (!template.IsAlive)
                    continue;

                var root = template.Root;
                var visual = template.Visual;

                if (targetType == typeof(GameObject))
                {
                    if (root && (includeInactive || root.activeInHierarchy) && seen.Add(root))
                        results.Add(root as T);
                    continue;
                }

                if (targetType == typeof(Transform))
                {
                    if (visual && (includeInactive || visual.gameObject.activeInHierarchy) && seen.Add(visual))
                        results.Add(visual as T);
                    continue;
                }

                if (!root)
                    continue;

                if (targetType == typeof(Animator))
                {
                    var animators = GetComponentsInChildrenCompat<Animator>(root, includeInactive);
                    for (int a = 0; a < animators.Length; a++)
                    {
                        var animator = animators[a];
                        if (!animator)
                            continue;

                        var go = animator.gameObject;
                        if (!go)
                            continue;

                        if (!includeInactive && !go.activeInHierarchy)
                            continue;

                        if (!go.scene.IsValid() || go.hideFlags != HideFlags.None)
                            continue;

                        if (seen.Add(animator))
                            results.Add(animator as T);
                    }
                    continue;
                }

                if (targetType == typeof(SkinnedMeshRenderer))
                {
                    var meshes = GetComponentsInChildrenCompat<SkinnedMeshRenderer>(root, includeInactive);
                    for (int m = 0; m < meshes.Length; m++)
                    {
                        var smr = meshes[m];
                        if (!smr)
                            continue;

                        var go = smr.gameObject;
                        if (!go)
                            continue;

                        if (!includeInactive && !go.activeInHierarchy)
                            continue;

                        if (!go.scene.IsValid() || go.hideFlags != HideFlags.None)
                            continue;

                        if (seen.Add(smr))
                            results.Add(smr as T);
                    }
                    continue;
                }

                if (targetType == typeof(MeshRenderer))
                {
                    var meshes = GetComponentsInChildrenCompat<MeshRenderer>(root, includeInactive);
                    for (int mr = 0; mr < meshes.Length; mr++)
                    {
                        var renderer = meshes[mr];
                        if (!renderer)
                            continue;

                        var go = renderer.gameObject;
                        if (!go)
                            continue;

                        if (!includeInactive && !go.activeInHierarchy)
                            continue;

                        if (!go.scene.IsValid() || go.hideFlags != HideFlags.None)
                            continue;

                        if (seen.Add(renderer))
                            results.Add(renderer as T);
                    }
                }
            }

            return results.Count == 0 ? Array.Empty<T>() : results.ToArray();
        }

        private static void Traverse<T>(Transform node, bool includeInactive, List<T> results) where T : Component
        {
            if (node == null)
                return;

            if (includeInactive || node.gameObject.activeInHierarchy)
            {
                var component = node.GetComponent<T>();
                if (component != null)
                    results.Add(component);
            }

            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                    Traverse(child, includeInactive, results);
            }
        }

        private static void TraverseTransforms(Transform node, bool includeInactive, List<Transform> results)
        {
            if (node == null)
                return;

            if (includeInactive || node.gameObject.activeInHierarchy)
                results.Add(node);

            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                    TraverseTransforms(child, includeInactive, results);
            }
        }

    }
}
