using System.Reflection;
using HarmonyLib;

namespace Peak.Bazooka;

[HarmonyPatch]
internal static class ZombieSilencePatch
{
    private static bool Prepare() => TargetMethod() != null;

    private static MethodBase? TargetMethod() =>
        GameCompat.FindType(GameCompat.ZombieTypeName)?.GetMethod(
            "RPC_PlaySFX",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static bool Prefix(UnityEngine.Component __instance)
    {
        GunCharacterLaunch? launch = __instance.GetComponent<GunCharacterLaunch>();
        return launch == null || !launch.Silenced;
    }
}
