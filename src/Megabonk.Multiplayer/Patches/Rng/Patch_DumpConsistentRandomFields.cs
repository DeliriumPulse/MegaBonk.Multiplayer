using HarmonyLib;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_DumpConsistentRandomFields
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("ConsistentRandom");
            if (t == null)
            {
                MultiplayerPlugin.LogS?.LogWarning("[RNGDUMP] Could not find ConsistentRandom type!");
                yield break;
            }

            foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                yield return ctor;
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            var t = __instance.GetType();
            MultiplayerPlugin.LogS?.LogInfo($"[RNGDUMP] --- Dumping fields for {t.FullName} ---");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var val = f.GetValue(__instance);
                MultiplayerPlugin.LogS?.LogInfo($"[RNGDUMP] {f.Name} = {val}");
            }
            MultiplayerPlugin.LogS?.LogInfo("[RNGDUMP] --- End dump ---");
        }
    }
}
