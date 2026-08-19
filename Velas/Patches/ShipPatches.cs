using HarmonyLib;

namespace Velas.Patches
{
    /// <summary>Makes every Ship carry a ShipSailComponent, generically -- this is what lets
    /// the mod support "at least the standard boats" (spec section 10) without an allowlist
    /// of prefab names: any GameObject with a Ship component gets sail support the moment it
    /// exists, including future boats added by this or other mods.</summary>
    [HarmonyPatch(typeof(Ship), nameof(Ship.Awake))]
    internal static class Ship_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Ship __instance)
        {
            if (!SailConfig.Enabled.Value || __instance == null) return;
            if (__instance.GetComponent<Ships.ShipSailComponent>() == null)
                __instance.gameObject.AddComponent<Ships.ShipSailComponent>();
        }
    }
}
