using HarmonyLib;
using UnityEngine;

namespace Peak.Bazooka;

[HarmonyPatch(typeof(Character), "Awake")]
internal static class GunPatch
{
    [HarmonyPostfix]
    private static void AddLaunch(Character __instance)
    {
        __instance.gameObject.AddComponent<GunCharacterLaunch>();
        __instance.gameObject.AddComponent<BazookaShooter>();
    }
}
