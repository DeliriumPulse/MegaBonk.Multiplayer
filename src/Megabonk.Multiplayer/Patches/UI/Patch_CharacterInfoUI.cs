using HarmonyLib;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch(typeof(CharacterInfoUI))]
    internal static class Patch_CharacterInfoUI
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void PostAwake(CharacterInfoUI __instance)
        {
            RegisterButtons(__instance);
        }

        [HarmonyPatch("OnEnable")]
        [HarmonyPostfix]
        private static void PostEnable(CharacterInfoUI __instance)
        {
            RegisterButtons(__instance);
            SkinPrefabRegistry.PrimeMenuRoster();
        }

        [HarmonyPatch("OnDisable")]
        [HarmonyPrefix]
        private static void PreDisable(CharacterInfoUI __instance)
        {
            UnregisterButtons(__instance);
        }

        private static void RegisterButtons(CharacterInfoUI ui)
        {
            var buttons = Il2CppComponentUtil.GetComponentsInChildrenCompat<MyButtonCharacter>(ui ? ui.gameObject : null, true);
            if (buttons == null || buttons.Length == 0)
                return;

            for (int i = 0; i < buttons.Length; i++)
                SkinPrefabRegistry.RegisterMenuButton(buttons[i]);
        }

        private static void UnregisterButtons(CharacterInfoUI ui)
        {
            var buttons = Il2CppComponentUtil.GetComponentsInChildrenCompat<MyButtonCharacter>(ui ? ui.gameObject : null, true);
            if (buttons == null || buttons.Length == 0)
                return;

            for (int i = 0; i < buttons.Length; i++)
                SkinPrefabRegistry.UnregisterMenuButton(buttons[i]);
        }
    }
}
