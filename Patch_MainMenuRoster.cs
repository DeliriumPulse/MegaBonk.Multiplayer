using System;
using Assets.Scripts._Data;
using HarmonyLib;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    internal static class Patch_MainMenuRoster
    {
        [HarmonyPatch(typeof(MainMenu), "Start")]
        [HarmonyPostfix]
        private static void MainMenuStart()
        {
            DumpButtonMetadata();
            SkinPrefabRegistry.PrimeMenuRoster();
        }

        [HarmonyPatch(typeof(CharacterInfoUI), "OnCharacterSelected", new Type[] { typeof(MyButtonCharacter) })]
        [HarmonyPostfix]
        private static void CharacterInfoUIOnCharacterSelected(CharacterInfoUI __instance, MyButtonCharacter __0)
        {
            if (__0 == null)
                return;

            BroadcastSelection(__0);
        }

        private static void BroadcastSelection(MyButtonCharacter button)
        {
            if (button == null)
                return;

            SkinPrefabRegistry.RegisterMenuButton(button);
            SkinPrefabRegistry.PrimeMenuRoster();

            CharacterData characterData = null;
            try { characterData = button.characterData; }
            catch { }

            var skinData = SkinPrefabRegistry.TryResolveSkin(button);

            SkinPrefabRegistry.RegisterMenuSelection(characterData, skinData);

            var driver = MultiplayerPlugin.Driver;
            driver?.ReportMenuSelection(characterData, skinData);
        }

        private static void DumpButtonMetadata()
        {
            if (SkinPrefabRegistry.MenuMetadataLogged)
                return;

            try
            {
                var type = typeof(MyButtonCharacter);
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
    }
}
