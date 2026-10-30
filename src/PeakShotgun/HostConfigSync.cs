using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Room rules authored by the host only: magazine size (<c>Shots</c>, 0 = infinite),
/// <c>ExplosionOneShot</c>, and defeated condors / antlions. Blasts always hit every target type.
/// Published through Photon room custom properties so every client applies the same rules,
/// whoever fires and whoever is hit.
/// </summary>
internal sealed class HostConfigSync : MonoBehaviour, IInRoomCallbacks, IMatchmakingCallbacks
{
    internal const string RoomShotsKey = "PeakBazooka_Shots";

    internal const string RoomShootablesKey = "PeakBazooka_Shootables";

    internal const string RoomWorldDownKey = "PeakBazooka_WorldDown";

    internal const string RoomOneShotKey = "PeakBazooka_OneShot";

    private static int? hostShots;

    private static ShootTargets? hostShootables;

    private static bool? hostExplosionOneShot;

    /// <summary>Host-synced magazine size when in a room; otherwise the local config. 0 = infinite.</summary>
    internal static int ShotCount
    {
        get
        {
            if (hostShots.HasValue)
            {
                return Mathf.Max(0, hostShots.Value);
            }

            return Plugin.LocalConfiguredShots;
        }
    }

    /// <summary>Every target. No cfg — blasts always hit everyone.</summary>
    internal static ShootTargets Shootables => ShootableRules.LocalShootables;

    /// <summary>Hardcoded hit counts (no cfg). Used only when <see cref="ExplosionOneShot"/> is off.</summary>
    internal static HitsToDefeatSettings HitsToDefeat => HitsToDefeatSettings.Defaults;

    /// <summary>Host-synced: one rocket defeats a shootable. Otherwise local config.</summary>
    internal static bool ExplosionOneShot => hostExplosionOneShot ?? Plugin.LocalExplosionOneShot;

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);

    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    /// <summary>Host publishes its config; clients pull the room values. Call after cfg reload / map load.</summary>
    internal static void SyncFromLocalRole(string reason)
    {
        if (!PhotonNetwork.InRoom)
        {
            hostShots = Plugin.LocalConfiguredShots;
            hostShootables = null;
            hostExplosionOneShot = Plugin.LocalExplosionOneShot;
            ShotgunCombat.ClearWorldHitCounts();
            Plugin.ApplyShotCountConfig();
            Plugin.Log.LogInfo($"Shots (offline/local) = {(Plugin.InfiniteShots ? "infinite" : ShotCount.ToString())} ({reason}).");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            PublishHostSettings(reason);
            return;
        }

        if (!TryApplyRoomSettings())
        {
            Plugin.Log.LogInfo($"Waiting for host bazooka room settings ({reason}).");
        }
    }

    private static void PublishHostSettings(string reason)
    {
        int shots = Plugin.LocalConfiguredShots;
        ShootTargets shootables = ShootableRules.LocalShootables;
        bool oneShot = Plugin.LocalExplosionOneShot;
        hostShots = shots;
        hostShootables = shootables;
        hostExplosionOneShot = oneShot;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { RoomShotsKey, shots },
            { RoomShootablesKey, (int)shootables },
            { RoomWorldDownKey, ShotgunCombat.PackWorldDefeats() },
            { RoomOneShotKey, oneShot ? 1 : 0 },
        });
        Plugin.ApplyShotCountConfig();
        Plugin.Log.LogInfo(
            $"Host published Shots = {(shots <= 0 ? "infinite" : shots.ToString())}, ExplosionOneShot = {oneShot}, Shootables = {shootables} ({reason}).");
    }

    /// <summary>Applies whichever room settings the host has published. False until shootables arrive.</summary>
    private static bool TryApplyRoomSettings()
    {
        Room? room = PhotonNetwork.CurrentRoom;
        if (room?.CustomProperties == null)
        {
            return false;
        }

        Hashtable props = room.CustomProperties;
        if (props.TryGetValue(RoomShootablesKey, out object? rawShootables)
            && rawShootables is int shootablesValue
            && hostShootables != (ShootTargets)shootablesValue)
        {
            hostShootables = (ShootTargets)shootablesValue;
            Plugin.Log.LogInfo($"Client applied host Shootables = {hostShootables}.");
        }

        if (props.TryGetValue(RoomWorldDownKey, out object? rawDowns) && rawDowns is int[] worldDowns)
        {
            ShotgunCombat.ApplyPackedWorldDefeats(worldDowns);
        }

        if (props.TryGetValue(RoomOneShotKey, out object? rawOneShot) && rawOneShot is int oneShotValue)
        {
            bool oneShot = oneShotValue != 0;
            if (hostExplosionOneShot != oneShot)
            {
                hostExplosionOneShot = oneShot;
                Plugin.Log.LogInfo($"Client applied host ExplosionOneShot = {oneShot}.");
            }
        }

        if (!props.TryGetValue(RoomShotsKey, out object? rawShots) || rawShots is not int shots)
        {
            return props.ContainsKey(RoomShootablesKey);
        }

        shots = Mathf.Max(0, shots);
        if (hostShots != shots)
        {
            hostShots = shots;
            Plugin.ApplyShotCountConfig();
            Plugin.Log.LogInfo($"Client applied host Shots = {(shots <= 0 ? "infinite" : shots.ToString())}.");
        }

        return true;
    }

    /// <summary>Host publishes defeated condors / antlions so late joiners apply the same outcome.</summary>
    internal static void PublishWorldDefeats()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { RoomWorldDownKey, ShotgunCombat.PackWorldDefeats() },
        });
    }

    private static void ClearHostSettings()
    {
        hostShots = null;
        hostShootables = null;
        hostExplosionOneShot = null;
        ShotgunCombat.ClearWorldHitCounts();
    }

    public void OnJoinedRoom() => SyncFromLocalRole("joined room");

    public void OnLeftRoom() => ClearHostSettings();

    public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PublishHostSettings("became host");
            ShotgunLootLedger.OnBecameHost();
        }
    }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null)
        {
            return;
        }

        ShotgunLootLedger.OnRoomPropertiesUpdate(propertiesThatChanged);
        if (propertiesThatChanged.ContainsKey(RoomShotsKey)
            || propertiesThatChanged.ContainsKey(RoomShootablesKey)
            || propertiesThatChanged.ContainsKey(RoomWorldDownKey)
            || propertiesThatChanged.ContainsKey(RoomOneShotKey))
        {
            TryApplyRoomSettings();
        }
    }

    public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) { }

    public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) { }

    public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps) { }

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    public void OnCreatedRoom() { }

    public void OnCreateRoomFailed(short returnCode, string message) { }

    public void OnJoinRoomFailed(short returnCode, string message) { }

    public void OnJoinRandomFailed(short returnCode, string message) { }
}
