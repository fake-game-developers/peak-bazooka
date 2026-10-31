using HarmonyLib;
using Peak;

namespace Peak.Bazooka;

/// <summary>Skip AntiZooka charge shake / charge SFX so the bazooka fires on click.</summary>
[HarmonyPatch(typeof(Action_Antizooka), "PlayChargeFX")]
internal static class AntizookaChargeStartPatch
{
    private static bool Prepare() =>
        AccessTools.Method(typeof(Action_Antizooka), "PlayChargeFX") != null;

    private static bool Prefix() => false;
}

[HarmonyPatch(typeof(Action_Antizooka), "StopChargeFX")]
internal static class AntizookaChargeStopPatch
{
    private static bool Prepare() =>
        AccessTools.Method(typeof(Action_Antizooka), "StopChargeFX") != null;

    private static bool Prefix() => false;
}

/// <summary>
/// The AntiZooka's shot juice kicks the held item back to <c>recoilPos</c> and then always settles it at
/// <c>defaultPos = (0, 0, 0)</c>, not where it was held, so the bazooka sat higher after its first shot. For the
/// bazooka, play the same shake and sound (not the muzzle flash, a burst of antigravity bubbles) and let
/// <see cref="Action_Gun"/> recoil around its own hold position. The vanilla AntiZooka keeps its juice.
/// </summary>
[HarmonyPatch(typeof(Action_Antizooka), "RPCA_Shoot")]
internal static class AntizookaShotJuicePatch
{
    private static bool Prepare() =>
        AccessTools.Method(typeof(Action_Antizooka), "RPCA_Shoot") != null;

    private static bool Prefix(Action_Antizooka __instance)
    {
        Action_Gun? gun = __instance.GetComponent<Action_Gun>();
        if (gun == null)
        {
            return true;
        }

        gun.PlayShotJuice(__instance);
        return false;
    }
}
