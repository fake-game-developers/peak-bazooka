using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// Room rules authored by the host only: <c>Mode</c> (Standard / RocketJump), magazine size (<c>Shots</c>, 0 = infinite),
/// the <c>[Targets]</c> switches, and defeated condors / antlions / spiders.
/// Published through Photon room custom properties so every client applies the same rules,
/// whoever fires and whoever is hit.
/// </summary>
internal sealed class HostConfigSync : MonoBehaviour, IInRoomCallbacks, IMatchmakingCallbacks
{
    internal const string RoomShotsKey = "PeakBazooka_Shots";

    internal const string RoomShootablesKey = "PeakBazooka_Shootables";

    internal const string RoomWorldDownKey = "PeakBazooka_WorldDown";

    internal const string RoomModeKey = "PeakBazooka_Mode";

    private static int? hostShots;

    private static ShootTargets? hostShootables;

    private static BazookaMode? hostMode;

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

    /// <summary>Host's <c>[Targets]</c> switches in a room; this player's own offline.</summary>
    internal static ShootTargets Shootables => hostShootables ?? ShootableRules.LocalShootables;

    /// <summary>Host-synced Standard / RocketJump. Both sides of a hit must agree on whether scouts take damage.</summary>
    internal static BazookaMode Mode => hostMode ?? Plugin.LocalMode;

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);

    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    /// <summary>Host publishes its config; clients pull the room values. Call after cfg reload / map load.</summary>
    internal static void SyncFromLocalRole(string reason)
    {
        if (!PhotonNetwork.InRoom)
        {
            hostShots = Plugin.LocalConfiguredShots;
            hostShootables = null;
            hostMode = Plugin.LocalMode;
            BazookaCombat.ClearWorldHitCounts();
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
        BazookaMode mode = Plugin.LocalMode;
        hostShots = shots;
        hostShootables = shootables;
        hostMode = mode;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { RoomShotsKey, shots },
            { RoomShootablesKey, (int)shootables },
            { RoomWorldDownKey, BazookaCombat.PackWorldDefeats() },
            { RoomModeKey, (int)mode },
        });
        Plugin.ApplyShotCountConfig();
        Plugin.Log.LogInfo(
            $"Host published Mode = {mode}, Shots = {(shots <= 0 ? "infinite" : shots.ToString())}, Targets = {shootables} ({reason}).");
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
            Plugin.Log.LogInfo($"Client applied host Targets = {hostShootables}.");
        }

        if (props.TryGetValue(RoomWorldDownKey, out object? rawDowns) && rawDowns is int[] worldDowns)
        {
            BazookaCombat.ApplyPackedWorldDefeats(worldDowns);
        }

        if (props.TryGetValue(RoomModeKey, out object? rawMode) && rawMode is int modeValue
            && System.Enum.IsDefined(typeof(BazookaMode), modeValue))
        {
            var mode = (BazookaMode)modeValue;
            if (hostMode != mode)
            {
                hostMode = mode;
                Plugin.Log.LogInfo($"Client applied host Mode = {mode}.");
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
            { RoomWorldDownKey, BazookaCombat.PackWorldDefeats() },
        });
    }

    private static void ClearHostSettings()
    {
        hostShots = null;
        hostShootables = null;
        hostMode = null;
        BazookaCombat.ClearWorldHitCounts();
        ShotLedger.Clear();
    }

    public void OnJoinedRoom() => SyncFromLocalRole("joined room");

    public void OnLeftRoom() => ClearHostSettings();

    public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PublishHostSettings("became host");
        }
    }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null)
        {
            return;
        }

        if (propertiesThatChanged.ContainsKey(RoomShotsKey)
            || propertiesThatChanged.ContainsKey(RoomShootablesKey)
            || propertiesThatChanged.ContainsKey(RoomWorldDownKey)
            || propertiesThatChanged.ContainsKey(RoomModeKey))
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
