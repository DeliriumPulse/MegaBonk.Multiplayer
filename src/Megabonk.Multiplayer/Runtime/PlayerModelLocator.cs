using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Megabonk.Multiplayer
{
    internal static class PlayerModelLocator
    {
        private static readonly string[] NameHints = { "model", "body", "character", "player", "mesh", "bonk" };
        private static readonly string[] PathHints =
        {
            "FogOfWar/Bandit",
            "FogOfWar/Bandit/BanditMesh",
            "FogOfWar(Clone)/Bandit",
            "FogOfWar(Clone)/Bandit/BanditMesh",
            "GeneratedMap/FogOfWar/Bandit",
            "GeneratedMap/FogOfWar/Bandit/BanditMesh",
            "GeneratedMap(Clone)/FogOfWar/Bandit",
            "GeneratedMap(Clone)/FogOfWar/Bandit/BanditMesh",
            "FogOfWarSphere",
            "FogOfWarSphere/BonkCharacter",
            "FogOfWarSphere/BonkCharacter/Body",
            "FogOfWarSphere/BonkCharacter/Body/Bonk",
            "FogOfWarSphere/BonkCharacter/Body/Bonk/BonkMesh",
            "FogOfWarSphere/BonkCharacter/Body/Bonk/SM_Bonk",
            "FogOfWarSphere(Clone)",
            "FogOfWarSphere(Clone)/BonkCharacter",
            "FogOfWarSphere(Clone)/BonkCharacter/Body",
            "FogOfWarSphere(Clone)/BonkCharacter/Body/Bonk",
            "FogOfWarSphere(Clone)/BonkCharacter/Body/Bonk/BonkMesh",
            "FogOfWarSphere(Clone)/BonkCharacter/Body/Bonk/SM_Bonk"
        };
        private static readonly string[] NamePathHints = { "Bandit", "BanditMesh" };
        private static readonly HashSet<string> GlobalMeshDumpedContexts = new HashSet<string>();
        private static readonly HashSet<Transform> LoggedHierarchies = new HashSet<Transform>();
        private static readonly HashSet<string> PathHintFailures = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> NameHintFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Transform, Transform> RegisteredVisuals = new Dictionary<Transform, Transform>();
        private static readonly List<string> _componentScratch = new List<string>();
        private static bool CandidatesLogged;
        public static Transform LastResolvedVisual { get; private set; }
        public static string LastResolvedPath { get; private set; }
        public static Transform Find(Transform fallback, string context = null, bool allowFallback = true)
        {
            var global = GetGlobalVisual();
            if (global)
                return ReturnResult(global, "cached visual", fallback, context);

            var registered = FindRegisteredVisual(fallback, context);
            if (registered)
                return ReturnResult(registered, "registered visual", fallback, context);
            var pathCandidate = FindByPathHints(out var pathReason);
            if (pathCandidate)
                return ReturnResult(pathCandidate, pathReason, fallback, context);
            var nameCandidate = FindByNameHints(out var nameReason);
            if (nameCandidate)
                return ReturnResult(nameCandidate, nameReason, fallback, context);
            var explicitModel = GameObject.Find("PlayerModel");
            if (IsUsable(explicitModel))
                return ReturnResult(explicitModel.transform, "GameObject.Find(\"PlayerModel\")", fallback, context);
            var rig = ResolveCameraRig();
            if (rig)
            {
                var fromRig = ScanRigForVisual(rig);
                if (fromRig)
                    return ReturnResult(fromRig, $"child of camera rig '{rig.name}'", fallback, context);
            }
            var animatorCandidate = FindAnimatorNear(rig);
            if (animatorCandidate)
                return ReturnResult(animatorCandidate.transform, $"Animator '{animatorCandidate.name}' near player rig", fallback, context);
            var meshCandidate = FindSkinnedRendererNear(rig);
            if (meshCandidate)
            {
                var animator = FindInParents<Animator>(meshCandidate.transform);
                if (IsUsable(animator))
                    return ReturnResult(animator.transform, $"Animator parent of skinned mesh '{meshCandidate.name}'", fallback, context);
                if (IsUsable(meshCandidate.transform))
                    return ReturnResult(meshCandidate.transform, $"SkinnedMeshRenderer '{meshCandidate.name}'", fallback, context);
            }
            var staticMesh = FindMeshRendererNear(rig);
            if (staticMesh)
            {
                var animator = FindInParents<Animator>(staticMesh.transform);
                if (IsUsable(animator))
                    return ReturnResult(animator.transform, $"Animator parent of mesh '{staticMesh.name}'", fallback, context);
                var candidate = staticMesh.transform;
                if (candidate.childCount > 0)
                    candidate = candidate.GetChild(0);
                if (IsUsable(candidate))
                    return ReturnResult(candidate, $"MeshRenderer '{staticMesh.name}'", fallback, context);
            }
            DumpFailure(context, fallback, rig);
            if (allowFallback && fallback != null)
                return ReturnResult(fallback, "fallback argument", fallback, context);
            return ReturnResult(null, "no locator match", fallback, context);
        }
        private static Transform ResolveCameraRig()
        {
            var cam = Camera.main;
            if (!cam)
                return null;
            var rig = cam.transform;
            for (int i = 0; i < 6 && rig.parent != null; i++)
                rig = rig.parent;
            return rig;
        }
        private static Transform ScanRigForVisual(Transform rig)
        {
            if (!rig)
                return null;
            Transform hinted = null;
            var transforms = Il2CppComponentUtil.GetTransformsInChildrenCompat(rig, true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var tf = transforms[i];
                if (!IsUsable(tf) || IsCameraRig(tf))
                    continue;
            var renderer = Il2CppComponentUtil.GetComponentCompat<SkinnedMeshRenderer>(tf);
                if (renderer != null)
                {
                    var animator = FindInParents<Animator>(renderer.transform);
                    if (IsUsable(animator))
                        return animator.transform;
                    return tf;
                }
                var nameLower = tf.name.ToLowerInvariant();
                for (int j = 0; j < NameHints.Length; j++)
                {
                    if (!nameLower.Contains(NameHints[j]))
                        continue;
                    var nestedRenderer = FindInHierarchy<SkinnedMeshRenderer>(tf, includeInactive: true, includeSelf: false);
                    if (nestedRenderer != null)
                    {
                        var animator = FindInParents<Animator>(nestedRenderer.transform);
                        if (IsUsable(animator))
                            return animator.transform;
                        if (IsUsable(nestedRenderer.transform))
                            return nestedRenderer.transform;
                    }
                    if (hinted == null)
                        hinted = tf;
                    break;
                }
            }
            return hinted;
        }
        private static Animator FindAnimatorNear(Transform rig)
        {
            var animators = Il2CppComponentUtil.GetComponentsInChildrenCompat<Animator>(rig ? rig.gameObject : null, true);
            if (animators != null && animators.Length > 0)
            {
                for (int i = 0; i < animators.Length; i++)
                {
                    var animator = animators[i];
                    if (IsUsable(animator))
                        return animator;
                }
            }
            return null;
        }
        private static SkinnedMeshRenderer FindSkinnedRendererNear(Transform rig)
        {
            Transform reference = rig;
            if (!reference)
                reference = PlayerModelLocator.LastResolvedVisual;
            var renderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<SkinnedMeshRenderer>(reference ? reference.gameObject : null, true);
            if (renderers != null && renderers.Length > 0)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                        continue;
                    var tf = renderer.transform;
                    if (IsUsable(tf) && !IsCameraRig(tf))
                        return renderer;
                }
            }
            return null;
        }
        private static MeshRenderer FindMeshRendererNear(Transform rig)
        {
            Transform reference = rig;
            if (!reference)
                reference = PlayerModelLocator.LastResolvedVisual;
            var renderers = Il2CppComponentUtil.GetComponentsInChildrenCompat<MeshRenderer>(reference ? reference.gameObject : null, true);
            if (renderers != null && renderers.Length > 0)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                        continue;
                    var tf = renderer.transform;
                    if (IsUsable(tf) && !IsCameraRig(tf))
                        return renderer;
                }
            }
            return null;
        }
        private static Transform FindInParents<T>(Transform transform) where T : Component
        {
            var current = transform;
            while (current != null)
            {
                if (Il2CppComponentUtil.GetComponentCompat<T>(current) != null)
                    return current;
                current = current.parent;
            }
            return null;
        }
        private static T FindInHierarchy<T>(Transform root, bool includeInactive, bool includeSelf = true) where T : Component
        {
            if (root == null)
                return null;
            if (includeSelf)
            {
                var candidate = Il2CppComponentUtil.GetComponentCompat<T>(root);
                if (candidate != null && (includeInactive || candidate.gameObject.activeInHierarchy))
                    return candidate;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var found = FindInHierarchy<T>(child, includeInactive, includeSelf: true);
                if (found != null)
                    return found;
            }
            return null;
        }
        private static void DumpFailure(string context, Transform fallback, Transform rig)
        {
            var log = MultiplayerPlugin.LogS;
            if (log == null)
                return;
            var sb = new StringBuilder();
            sb.AppendLine($"[LocatorDump] {context}: rig={Describe(rig)}, fallback={Describe(fallback)}, camera={Describe(Camera.main ? Camera.main.transform : null)}");
            if (rig)
            {
                sb.AppendLine("  Rig children:");
                for (int i = 0; i < rig.childCount; i++)
                {
                    var child = rig.GetChild(i);
                    sb.Append("    - ").Append(Describe(child)).AppendLine();
                }
            }
            LogHierarchy($"{context}:fallback", fallback, log);
            var skinnedArray = Il2CppComponentUtil.FindObjectsOfTypeCompat<SkinnedMeshRenderer>(includeInactive: true);
            if (skinnedArray != null && skinnedArray.Length > 0)
            {
                Vector3 reference = rig ? rig.position : (Camera.main ? Camera.main.transform.position : Vector3.zero);
                var entries = new List<(float dist, SkinnedMeshRenderer smr)>(skinnedArray.Length);
                for (int i = 0; i < skinnedArray.Length; i++)
                {
                    var smr = skinnedArray[i];
                    if (!smr) continue;
                    float dist = Vector3.Distance(reference, smr.transform.position);
                    entries.Add((dist, smr));
                }
                entries.Sort((a, b) => a.dist.CompareTo(b.dist));
                sb.AppendLine("  Closest SkinnedMeshRenderers:");
                int limit = Math.Min(8, entries.Count);
                for (int i = 0; i < limit; i++)
                {
                    var entry = entries[i];
                    sb.Append("    - ").Append(Describe(entry.smr ? entry.smr.transform : null))
                      .Append(" dist=").Append(entry.dist.ToString("F2"))
                      .Append(" size=").Append(entry.smr ? entry.smr.bounds.size.ToString("F2") : "<none>")
                      .AppendLine();
                }
            }
            var meshArray = Il2CppComponentUtil.FindObjectsOfTypeCompat<MeshRenderer>(includeInactive: true);
            if (meshArray != null && meshArray.Length > 0)
            {
                Vector3 reference = rig ? rig.position : (Camera.main ? Camera.main.transform.position : Vector3.zero);
                var entries = new List<(float dist, MeshRenderer mr)>(meshArray.Length);
                for (int i = 0; i < meshArray.Length; i++)
                {
                    var mr = meshArray[i];
                    if (!mr) continue;
                    float dist = Vector3.Distance(reference, mr.transform.position);
                    entries.Add((dist, mr));
                }
                entries.Sort((a, b) => a.dist.CompareTo(b.dist));
                sb.AppendLine("  Closest MeshRenderers (static):");
                int limit = Math.Min(8, entries.Count);
                for (int i = 0; i < limit; i++)
                {
                    var entry = entries[i];
                    var mr = entry.mr;
                    sb.Append("    - ").Append(Describe(mr ? mr.transform : null))
                      .Append(" dist=").Append(entry.dist.ToString("F2"))
                      .Append(" size=").Append(mr ? mr.bounds.size.ToString("F2") : "<none>")
                      .AppendLine();
                }
            }
            AppendGlobalMeshDump(sb, rig, context);
            log.LogWarning(sb.ToString());
        }
        private static void LogHierarchy(string context, Transform root, BepInEx.Logging.ManualLogSource log)
        {
            if (!root || log == null)
                return;
            if (!LoggedHierarchies.Add(root))
                return;
            var sb = new StringBuilder();
            sb.AppendLine($"[LocatorHierarchy] {context}: root={Describe(root)}");
            _scratchCount = 0;
            AppendHierarchy(root, sb, depth: 0, maxDepth: 4, maxEntries: 40, entriesSeen: ref _scratchCount);
            _scratchCount = 0;
            log.LogInfo(sb.ToString());
        }
        private static int _scratchCount;
        private static void AppendHierarchy(Transform node, StringBuilder sb, int depth, int maxDepth, int maxEntries, ref int entriesSeen)
        {
            if (!node || depth > maxDepth || entriesSeen >= maxEntries)
                return;
            string indent = new string(' ', depth * 2);
            sb.Append(indent)
              .Append("- ")
              .Append(Describe(node))
              .Append(" flags=[");

            var tags = _componentScratch;
            tags.Clear();

            if (Il2CppComponentUtil.GetComponentCompat<Animator>(node) != null)
                tags.Add("Animator");
            if (Il2CppComponentUtil.GetComponentCompat<SkinnedMeshRenderer>(node) != null)
                tags.Add("SkinnedMeshRenderer");
            if (Il2CppComponentUtil.GetComponentCompat<MeshRenderer>(node) != null)
                tags.Add("MeshRenderer");
            if (Il2CppComponentUtil.GetComponentCompat<SkinnedMeshRenderer>(node)?.sharedMesh != null)
                tags.Add("HasMesh");

            if (tags.Count > 0)
                sb.Append(string.Join(",", tags));

            sb.Append("]");
            sb.Append(" children=").Append(node.childCount);
            sb.AppendLine();
            entriesSeen++;
            for (int i = 0; i < node.childCount && entriesSeen < maxEntries; i++)
            {
                AppendHierarchy(node.GetChild(i), sb, depth + 1, maxDepth, maxEntries, ref entriesSeen);
            }
        }
        internal static string Describe(Transform tf)
        {
            if (!tf)
                return "<null>";
            return $"{tf.name} (path={BuildPath(tf)})";
        }
        internal static void RegisterKnownVisual(Transform owner, Transform visual, string context)
        {
            if (!visual)
                return;
            void Store(Transform key, bool logBinding)
            {
                if (!key)
                    return;
                if (RegisteredVisuals.TryGetValue(key, out var existing) && existing == visual)
                    return;
                RegisteredVisuals[key] = visual;
                if (logBinding)
                    MultiplayerPlugin.LogS?.LogInfo($"[LocatorRegister] {context}: {Describe(key)} -> {Describe(visual)}");
            }
            var currentVisual = visual;
            while (currentVisual)
            {
                bool log = currentVisual == visual;
                Store(currentVisual, logBinding: log);
                currentVisual = currentVisual.parent;
            }

            var currentOwner = owner;
            while (currentOwner)
            {
                Store(currentOwner, logBinding: true);
                currentOwner = currentOwner.parent;
            }

            if (IsUsable(visual) && IsRenderable(visual))
            {
                LastResolvedVisual = visual;
                LastResolvedPath = BuildPath(visual);
            }
        }
        private static Transform FindRegisteredVisual(Transform fallback, string context)
        {
            Transform current = fallback;
            while (current)
            {
                if (RegisteredVisuals.TryGetValue(current, out var visual))
                {
                    if (visual)
                    {
                        MultiplayerPlugin.LogS?.LogInfo($"[Locator] {(context ?? "<null>")}: registered -> {Describe(visual)} (key={Describe(current)})");
                        return visual;
                    }
                    RegisteredVisuals.Remove(current);
                }
                current = current.parent;
            }
            return null;
        }
        private static Transform GetGlobalVisual()
        {
            var visual = LastResolvedVisual;
            if (!IsUsable(visual) || !IsRenderable(visual))
                return null;
            return visual;
        }
        private static bool IsRenderable(Transform visual)
        {
            if (!visual)
                return false;

            if (Il2CppComponentUtil.GetComponentCompat<SkinnedMeshRenderer>(visual)?.sharedMesh != null)
                return true;

            var childSmr = FindInHierarchy<SkinnedMeshRenderer>(visual, includeInactive: true, includeSelf: false);
            if (childSmr && childSmr.sharedMesh != null)
                return true;

            return false;
        }
        internal static string GetPath(Transform tf) => BuildPath(tf);
        private static string BuildPath(Transform tf)
        {
            if (!tf)
                return "<null>";
            var segments = new Stack<string>();
            var current = tf;
            while (current != null)
            {
                segments.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", segments.ToArray());
        }
        private static Transform ReturnResult(Transform candidate, string reason, Transform fallback, string context)
        {
            LogResult(context, candidate, reason, fallback);
            if (candidate && IsRenderable(candidate))
            {
                LastResolvedVisual = candidate;
                LastResolvedPath = BuildPath(candidate);
            }
            return candidate;
        }
        private static void LogResult(string context, Transform candidate, string reason, Transform fallback)
        {
            var log = MultiplayerPlugin.LogS;
            if (log == null)
                return;
            var sb = new StringBuilder();
            sb.Append("[Locator] ").Append(context ?? "<null>").Append(": ").Append(Describe(candidate));
            sb.Append(" (via ").Append(reason).Append(")");
            if (fallback)
                sb.Append(" fallback=").Append(Describe(fallback));
            log.LogInfo(sb.ToString());
        }
        private static bool IsCameraRig(Transform tf)
        {
            if (!tf)
                return false;
            if (Il2CppComponentUtil.GetComponentCompat<Camera>(tf) != null)
                return true;
            var name = tf.name.ToLowerInvariant();
            return name.Contains("camera") || name.Contains("cine") || name.Contains("virtual");
        }
        private static bool IsUsable(GameObject go)
        {
            if (!go)
                return false;
            if (!go.activeInHierarchy)
                return false;
            return go.scene.IsValid();
        }
        private static bool IsUsable(Transform tf)
        {
            if (!tf)
                return false;
            return IsUsable(tf.gameObject);
        }
        private static bool IsUsable(Animator animator)
        {
            if (!animator)
                return false;
            return IsUsable(animator.gameObject);
        }
        private static Transform FindByPathHints(out string reason)
        {
            for (int i = 0; i < PathHints.Length; i++)
            {
                string path = PathHints[i];
                if (TryResolvePath(path, out var visual))
                {
                    reason = $"path hint '{path}'";
                    MultiplayerPlugin.LogS?.LogInfo($"[LocatorHint] Resolved path hint '{path}' -> {Describe(visual)}");
                    LogHierarchy($"PathHint.{path}", visual, MultiplayerPlugin.LogS);
                    try
                    {
                        var mesh = visual ? FindInHierarchy<SkinnedMeshRenderer>(visual, includeInactive: true, includeSelf: true) : null;
                        if (mesh)
                            MultiplayerPlugin.LogS?.LogInfo($"[LocatorHint] First SMR under '{path}': {Describe(mesh.transform)}");
                        else
                            MultiplayerPlugin.LogS?.LogInfo($"[LocatorHint] No SkinnedMeshRenderer found under '{path}'");
                    }
                    catch (Exception ex)
                    {
                        MultiplayerPlugin.LogS?.LogDebug($"[LocatorHint] Failed to inspect meshes under '{path}': {ex}");
                    }
                    return visual;
                }
                if (PathHintFailures.Add(path))
                    MultiplayerPlugin.LogS?.LogDebug($"[LocatorHint] Path hint not found: {path}");
            }
            reason = null;
            LogAvailableCandidates("path");
            return null;
        }
        private static Transform SelectVisualFrom(Transform root)
        {
            if (!root)
                return null;
            var animator = FindInHierarchy<Animator>(root, includeInactive: true, includeSelf: true);
            if (IsUsable(animator))
                return animator.transform;
            var smr = FindInHierarchy<SkinnedMeshRenderer>(root, includeInactive: true, includeSelf: true);
            if (smr && IsUsable(smr.transform))
                return smr.transform;
            LogTransformTree(root);
            return IsUsable(root) ? root : null;
        }
        private static Transform FindByNameHints(out string reason)
        {
            for (int i = 0; i < NamePathHints.Length; i++)
            {
                string name = NamePathHints[i];
                var go = GameObject.Find(name);
                if (!IsUsable(go))
                    continue;
                var visual = SelectVisualFrom(go.transform);
                if (!visual)
                    continue;
                string path = BuildPath(visual);
                if (!path.Contains("Bandit", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains("FogOfWar", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                reason = $"name hint '{name}' (path={path})";
                MultiplayerPlugin.LogS?.LogInfo($"[LocatorHint] Resolved name hint '{name}' -> {Describe(visual)}");
                return visual;
            }
            if (NameHintFailures.Add(string.Join("|", NamePathHints)))
                MultiplayerPlugin.LogS?.LogDebug("[LocatorHint] Name hints did not resolve any usable object.");
            reason = null;
            LogAvailableCandidates("name");
            return null;
        }
        private static bool TryResolvePath(string path, out Transform visual)
        {
            visual = null;
            var go = GameObject.Find(path);
            if (IsUsable(go))
            {
                visual = SelectVisualFrom(go.transform);
                if (visual)
                    return true;
            }
            var segments = path.Split('/');
            if (segments.Length <= 1)
                return false;
            var root = GameObject.Find(segments[0]);
            if (!IsUsable(root))
                root = GameObject.Find($"{segments[0]}(Clone)");
            if (!IsUsable(root))
                return false;
            var relativePath = string.Join("/", segments, 1, segments.Length - 1);
            var target = root.transform.Find(relativePath);
            if (!target)
            {
                // Try clone variant of final segment
                var cloneRelative = string.Join("/", segments, 1, segments.Length - 2);
                if (!string.IsNullOrEmpty(cloneRelative))
                {
                    var node = root.transform.Find(cloneRelative);
                    if (node)
                    {
                        var last = segments[segments.Length - 1];
                        target = node.Find($"{last}(Clone)");
                    }
                }
            }
            if (!target)
                return false;
            visual = SelectVisualFrom(target);
            return visual;
        }
        private static void LogAvailableCandidates(string source)
        {
            var log = MultiplayerPlugin.LogS;
            if (log == null || CandidatesLogged)
                return;
            CandidatesLogged = true;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[LocatorHintDump] Candidates after {source} hints failed:");
                AppendAnimatorCandidates(sb);
                AppendRendererCandidates(sb);
                log.LogWarning(sb.ToString());
            }
            catch (Exception ex)
            {
                log.LogDebug($"[LocatorHintDump] Failed to enumerate candidates: {ex}");
            }
        }
        private static void AppendAnimatorCandidates(StringBuilder sb)
        {
            try
            {
                var animators = FindSceneObjectsOfType<Animator>(includeInactive: true);
                if (animators == null || animators.Length == 0)
                {
                    sb.AppendLine("  Animators: <none>");
                    return;
                }
                sb.AppendLine("  Animators:");
                for (int i = 0; i < animators.Length; i++)
                {
                    var anim = animators[i];
                    if (!anim)
                        continue;
                    sb.Append("    - ").Append(Describe(anim.transform)).AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Animators: failed ({ex.GetType().Name}: {ex.Message})");
            }
        }
        private static void AppendRendererCandidates(StringBuilder sb)
        {
            try
            {
                var meshes = FindSceneObjectsOfType<SkinnedMeshRenderer>(includeInactive: true);
                if (meshes == null || meshes.Length == 0)
                {
                    sb.AppendLine("  SkinnedMeshRenderers: <none>");
                    return;
                }
                sb.AppendLine("  SkinnedMeshRenderers:");
                for (int i = 0; i < meshes.Length; i++)
                {
                    var smr = meshes[i];
                    if (!smr)
                        continue;
                    sb.Append("    - ").Append(Describe(smr.transform)).AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  SkinnedMeshRenderers: failed ({ex.GetType().Name}: {ex.Message})");
            }
        }
        private static void LogTransformTree(Transform root)
        {
            if (!root)
                return;
            var log = MultiplayerPlugin.LogS;
            if (log == null)
                return;
            var sb = new StringBuilder();
            sb.AppendLine($"[LocatorHierarchyDump] Tree for {Describe(root)}");
            _scratchCount = 0;
            AppendHierarchy(root, sb, depth: 0, maxDepth: 6, maxEntries: 160, ref _scratchCount);
            _scratchCount = 0;
            log.LogInfo(sb.ToString());
        }
        private static void AppendGlobalMeshDump(StringBuilder sb, Transform rig, string context)
        {
            string sceneName = SceneManager.GetActiveScene().IsValid() ? SceneManager.GetActiveScene().name : "<none>";
            string key = $"{sceneName}::{context ?? "<null>"}";
            if (!GlobalMeshDumpedContexts.Add(key))
                return;
            Vector3 reference = rig ? rig.position : (Camera.main ? Camera.main.transform.position : Vector3.zero);
            try
            {
                AppendGlobalSkinnedMeshes(sb, reference);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Global SkinnedMeshRenderers: failed ({ex.GetType().Name}: {ex.Message})");
            }
            try
            {
                AppendGlobalAnimators(sb, reference);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Global Animators: failed ({ex.GetType().Name}: {ex.Message})");
            }
        }
        private static void AppendGlobalSkinnedMeshes(StringBuilder sb, Vector3 reference)
        {
            var all = FindAllOfType<SkinnedMeshRenderer>();
            if (all == null || all.Length == 0)
            {
                sb.AppendLine("  Global SkinnedMeshRenderers: <none>");
                return;
            }
            var entries = new List<(float dist, SkinnedMeshRenderer smr)>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var smr = all[i];
                if (!smr)
                    continue;
                var go = smr.gameObject;
                if (!IsSceneObject(go))
                    continue;
                float dist = Vector3.Distance(reference, smr.transform.position);
                entries.Add((dist, smr));
            }
            if (entries.Count == 0)
            {
                sb.AppendLine("  Global SkinnedMeshRenderers: <none in scene>");
                return;
            }
            entries.Sort((a, b) => a.dist.CompareTo(b.dist));
            sb.AppendLine("  Global SkinnedMeshRenderers:");
            int limit = Math.Min(12, entries.Count);
            for (int i = 0; i < limit; i++)
            {
                var smr = entries[i].smr;
                var tf = smr ? smr.transform : null;
                sb.Append("    - ").Append(Describe(tf))
                  .Append(" scene=").Append(SceneName(tf))
                  .Append(" dist=").Append(entries[i].dist.ToString("F2"))
                  .Append(" bounds=").Append(smr.bounds.size.ToString("F2"))
                  .AppendLine();
            }
        }
        private static void AppendGlobalAnimators(StringBuilder sb, Vector3 reference)
        {
            var all = FindAllOfType<Animator>();
            if (all == null || all.Length == 0)
            {
                sb.AppendLine("  Global Animators: <none>");
                return;
            }
            var entries = new List<(float dist, Animator animator)>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var animator = all[i];
                if (!animator)
                    continue;
                var go = animator.gameObject;
                if (!IsSceneObject(go))
                    continue;
                float dist = Vector3.Distance(reference, animator.transform.position);
                entries.Add((dist, animator));
            }
            if (entries.Count == 0)
            {
                sb.AppendLine("  Global Animators: <none in scene>");
                return;
            }
            entries.Sort((a, b) => a.dist.CompareTo(b.dist));
            sb.AppendLine("  Global Animators:");
            int limit = Math.Min(12, entries.Count);
            for (int i = 0; i < limit; i++)
            {
                var animator = entries[i].animator;
                var tf = animator ? animator.transform : null;
                sb.Append("    - ").Append(Describe(tf))
                  .Append(" scene=").Append(SceneName(tf))
                  .Append(" dist=").Append(entries[i].dist.ToString("F2"))
                  .AppendLine();
            }
        }
        private static bool IsSceneObject(GameObject go)
        {
            if (!go)
                return false;
            var scene = go.scene;
            if (!scene.IsValid())
                return false;
            if (go.hideFlags != HideFlags.None)
                return false;
            return true;
        }
        private static string SceneName(Transform tf)
        {
            if (!tf || !tf.gameObject)
                return "<none>";
            var scene = tf.gameObject.scene;
            return scene.IsValid() ? scene.name : "<none>";
        }
        private static T[] FindAllOfType<T>() where T : UnityEngine.Object
        {
            var sceneObjects = FindSceneObjectsOfType<T>(includeInactive: true);
            if (sceneObjects.Length > 0)
                return sceneObjects;

            var viaResources = TryFindViaResources<T>();
            return viaResources.Length > 0 ? viaResources : Array.Empty<T>();
        }
        private static T[] TryFindViaResources<T>() where T : UnityEngine.Object
        {
            try
            {
                var raw = Resources.FindObjectsOfTypeAll(typeof(T));
                if (raw == null || raw.Length == 0)
                    return Array.Empty<T>();
                var list = new List<T>(raw.Length);
                for (int i = 0; i < raw.Length; i++)
                {
                    if (raw[i] is T typed && typed)
                        list.Add(typed);
                }
                return list.Count > 0 ? list.ToArray() : Array.Empty<T>();
            }
            catch (MissingMethodException)
            {
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.LogS?.LogDebug($"[LocatorDump] Resources.FindObjectsOfTypeAll<{typeof(T).Name}> failed: {ex}");
            }
            return Array.Empty<T>();
        }

        private static T[] FindSceneObjectsOfType<T>(bool includeInactive) where T : UnityEngine.Object
        {
            var results = new List<T>();
            int sceneCount = SceneManager.sceneCount;
            var targetType = typeof(T);

            for (int s = 0; s < sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                if (roots == null)
                    continue;

                for (int r = 0; r < roots.Length; r++)
                {
                    CollectFromHierarchy(roots[r], includeInactive, targetType, results);
                }
            }

            return results.Count > 0 ? results.ToArray() : Array.Empty<T>();
        }

        private static void CollectFromHierarchy<T>(GameObject root, bool includeInactive, Type targetType, List<T> results) where T : UnityEngine.Object
        {
            if (root == null)
                return;

            var goActive = root.activeInHierarchy;
            if (includeInactive || goActive)
            {
                if (typeof(Component).IsAssignableFrom(targetType))
                {
                    var components = new List<Component>();
                    root.GetComponents(targetType, components);
                    for (int i = 0; i < components.Count; i++)
                    {
                        var component = components[i];
                        if (component != null && component is T typedComponent)
                            results.Add(typedComponent);
                    }
                }
                else if (targetType == typeof(GameObject))
                {
                    if (root is T typedGo)
                        results.Add(typedGo);
                }
                else if (targetType == typeof(Transform))
                {
                    var transform = root.transform;
                    if (transform && transform is T typedTf)
                        results.Add(typedTf);
                }
            }

            var childCount = root.transform.childCount;
            for (int c = 0; c < childCount; c++)
            {
                var child = root.transform.GetChild(c);
                if (child != null)
                    CollectFromHierarchy(child.gameObject, includeInactive, targetType, results);
            }
        }
    }
}
