using HarmonyLib;

namespace Velas.UI
{
    /// <summary>Same trick as NpcValheim/UI/UiInputBlocker.cs: while the selector is open,
    /// force Menu.IsVisible so the cursor is freed, and swallow player/camera input so
    /// clicking a sail in the grid doesn't also swing a weapon or spin the camera.</summary>
    internal static class SailUiInputBlocker
    {
        public static bool IsOpen;
    }

    [HarmonyPatch(typeof(Menu), nameof(Menu.IsVisible))]
    internal static class Menu_IsVisible_SailPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (SailUiInputBlocker.IsOpen) __result = true;
        }
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
    internal static class PlayerController_TakeInput_SailPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (SailUiInputBlocker.IsOpen) __result = false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
    internal static class Player_TakeInput_SailPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (SailUiInputBlocker.IsOpen) __result = false;
        }
    }
}
