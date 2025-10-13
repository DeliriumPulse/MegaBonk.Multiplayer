using HarmonyLib;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(MyButtonCharacter))]
    internal static class Patch_MyButtonCharacter
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void PostAwake(MyButtonCharacter __instance)
        {
            SkinPrefabRegistry.RegisterMenuButton(__instance);
        }

        [HarmonyPatch("OnDestroy")]
        [HarmonyPostfix]
        private static void PostDestroy(MyButtonCharacter __instance)
        {
            SkinPrefabRegistry.UnregisterMenuButton(__instance);
        }
    }
}
