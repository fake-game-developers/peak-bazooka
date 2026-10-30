using HarmonyLib;
using Peak;

namespace Peak.Shotgun;

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
