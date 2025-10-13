using HarmonyLib;
using System.Reflection;
using System.Linq;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_TraceCR_Constructors
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("ConsistentRandom");
            if (t == null)
            {
                MultiplayerPlugin.LogS?.LogWarning("[RNGTRACE] Could not find ConsistentRandom type for constructor tracing!");
                yield break;
            }

            foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                MultiplayerPlugin.LogS?.LogInfo($"[RNGTRACE] Hooking ConsistentRandom ctor: ({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name))})");
                yield return ctor;
            }
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance, params object[] __args)
        {
            string args = (__args == null || __args.Length == 0)
                ? "(no args)"
                : string.Join(", ", __args.Select(a => a?.ToString() ?? "null"));

            MultiplayerPlugin.LogS?.LogInfo($"[RNGTRACE] New ConsistentRandom() created with args={args}, InstanceID={__instance?.GetHashCode()}");

            // Try to log any obvious internal seed-like fields
            var t = __instance.GetType();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (f.Name.ToLower().Contains("seed") || f.Name.ToLower().Contains("rand"))
                {
                    var val = f.GetValue(__instance);
                    MultiplayerPlugin.LogS?.LogInfo($"[RNGTRACE] Field {f.Name} = {val}");
                }
            }
        }
    }
}
