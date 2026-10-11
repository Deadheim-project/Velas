using HarmonyLib;
using UnityEngine;
using Velas.Ships;
using Velas.UI;

namespace Velas.Patches
{
    /// <summary>Shows "[G] Trocar vela" on the crosshair while the player aims at a mast,
    /// like the prompt vanilla shows for any other interactable. Runs after vanilla filled
    /// the hover text and only writes when vanilla left it empty, so a real Hoverable (the
    /// rudder, a chest on deck) always wins.</summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
    internal static class Hud_UpdateCrosshair_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Hud __instance, Player player)
        {
            if (!SailConfig.Enabled.Value || __instance == null || player == null) return;
            if (player != Player.m_localPlayer || SailUiInputBlocker.IsOpen) return;
            if (__instance.m_hoverName == null || !string.IsNullOrEmpty(__instance.m_hoverName.text)) return;
            if (MastTargeting.GetAimedMastShip(player) == null) return;

            __instance.m_hoverName.text =
                $"[<color=yellow><b>{SailConfig.OpenSailSelectorKey.Value}</b></color>] Trocar vela";
            __instance.m_crosshair.color = Color.yellow;
        }
    }
}
