using System.Collections.Generic;
using HarmonyLib;
using Peak;
using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

public class Action_Gun : ItemAction
{
    public float fireRate = 0.8f;

    public Transform? spawnTransform;

    public System.Action? OnShoot;

    // An unanswered request is re-sent with the same id after this long (message lost, host changed).
    private const float PendingShotRetry = 1.5f;

    // Requests sent fireRate apart can arrive bunched up by network jitter.
    private const float HostFireRateTolerance = 0.9f;

    private static int nextShotId;

    private float lastShootTime = float.NegativeInfinity;

    private int pendingShotId;

    private float pendingShotSentTime;

    private Vector3 pendingForward;

    // Host-side: newest shot id per sender (actor number) and the answer given, so a retry gets the same answer.
    private readonly Dictionary<int, (int shotId, bool accepted)> lastShotBySender = new();

    private double lastAcceptedShotTime = double.NegativeInfinity;

    /// <summary>
    /// Asks the host to authorize one shot. Nothing happens locally until the host accepts it, and only one
    /// request is outstanding at a time. An unanswered request is retried with the same id; the host answers
    /// a retry with its original decision.
    /// </summary>
    public override void RunAction()
    {
        if (pendingShotId != 0)
        {
            if (Time.time >= pendingShotSentTime + PendingShotRetry)
            {
                pendingForward = MainCamera.instance.transform.forward;
                SendShotRequest();
            }

            return;
        }

        if (Time.time <= lastShootTime + fireRate || spawnTransform == null)
        {
            return;
        }

        Action_Ammo? ammo = GetComponent<Action_Ammo>();
        if (ammo != null && !ammo.HasAmmo())
        {
            return;
        }

        lastShootTime = Time.time;
        pendingShotId = ++nextShotId;
        pendingForward = MainCamera.instance.transform.forward;
        SendShotRequest();
    }

    private void SendShotRequest()
    {
        pendingShotSentTime = Time.time;
        if (!PhotonNetwork.InRoom)
        {
            HandleShotRequest(pendingShotId, null, Time.timeAsDouble);
            return;
        }

        photonView.RPC(nameof(RPC_RequestShot), RpcTarget.MasterClient, pendingShotId);
    }

    [PunRPC]
    public void RPC_RequestShot(int shotId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        HandleShotRequest(shotId, info.Sender, info.SentServerTime);
    }

    /// <summary>Host (or offline): validate holder, cooldown and ammo, then announce the result.</summary>
    private void HandleShotRequest(int shotId, Photon.Realtime.Player? sender, double sentTime)
    {
        int senderId = sender?.ActorNumber ?? 0;
        if (lastShotBySender.TryGetValue(senderId, out (int shotId, bool accepted) last))
        {
            if (shotId < last.shotId)
            {
                return;
            }

            if (shotId == last.shotId)
            {
                SendShotResult(senderId, shotId, last.accepted);
                return;
            }
        }

        double sinceLastShot = sentTime - lastAcceptedShotTime;
        // A large negative gap means the server clock wrapped; do not lock the gun forever.
        bool cooledDown = sinceLastShot >= fireRate * HostFireRateTolerance || sinceLastShot < -60.0;
        Action_Ammo? ammo = GetComponent<Action_Ammo>();
        bool accepted = IsHeldBy(sender) && cooledDown && (ammo == null || ammo.TrySpendOne());
        if (accepted)
        {
            lastAcceptedShotTime = sentTime;
        }

        lastShotBySender[senderId] = (shotId, accepted);
        SendShotResult(senderId, shotId, accepted);
    }

    private bool IsHeldBy(Photon.Realtime.Player? sender)
    {
        Character? holder = item.holderCharacter;
        if (item.itemState != ItemState.Held || holder == null)
        {
            return false;
        }

        return sender == null || (holder.photonView != null && holder.photonView.Owner == sender);
    }

    /// <summary>
    /// Accepted shots go to everyone, so each client can check that a hit RPC belongs to a shot the host
    /// authorized. Rejections only matter to the shooter.
    /// </summary>
    private void SendShotResult(int shooterActor, int shotId, bool accepted)
    {
        if (!PhotonNetwork.InRoom)
        {
            OnShotResult(shotId, accepted);
            return;
        }

        if (accepted)
        {
            photonView.RPC(nameof(RPC_ShotResult), RpcTarget.All, shooterActor, shotId, true);
            return;
        }

        Photon.Realtime.Player? shooter = PhotonNetwork.CurrentRoom?.GetPlayer(shooterActor);
        if (shooter != null)
        {
            photonView.RPC(nameof(RPC_ShotResult), shooter, shooterActor, shotId, false);
        }
    }

    [PunRPC]
    public void RPC_ShotResult(int shooterActor, int shotId, bool accepted, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient)
        {
            return;
        }

        if (accepted)
        {
            ShotLedger.RecordAuthorizedShot(shooterActor, shotId);
        }

        if (PhotonNetwork.LocalPlayer != null && shooterActor == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            OnShotResult(shotId, accepted);
        }
    }

    private void OnShotResult(int shotId, bool accepted)
    {
        if (shotId != pendingShotId)
        {
            return;
        }

        pendingShotId = 0;
        if (accepted)
        {
            Fire(shotId, pendingForward);
        }
    }

    /// <summary>Runs on the shooter once the host has accepted the shot.</summary>
    private void Fire(int shotId, Vector3 forward)
    {
        // A gun put away before the answer arrived just loses the shot.
        BazookaShooter? shooter = character != null ? character.GetComponent<BazookaShooter>() : null;
        if (spawnTransform == null || character == null || !character.IsLocal || shooter == null)
        {
            return;
        }

        OnShoot?.Invoke();

        Vector3 origin = spawnTransform.position;
        // Recoil is a ragdoll shove; skip it in TF2 mode so the shot doesn't spoil a rocket jump.
        if (Plugin.Recoil > 0f && !Plugin.Tf2Mode)
        {
            character.AddForce(-forward * (Plugin.Recoil / Time.fixedDeltaTime));
        }

        // From here on the rocket belongs to the scout, not the item: putting the bazooka away must not delete it.
        shooter.LaunchRocket(origin, forward, shotId);
        PlayVanillaShootJuice();
    }

    private void PlayVanillaShootJuice()
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC("RPCA_Shoot", RpcTarget.All);
            return;
        }

        Action_Antizooka? juice = GetComponent<Action_Antizooka>();
        if (juice != null)
        {
            AccessTools.Method(typeof(Action_Antizooka), "RPCA_Shoot")?.Invoke(juice, null);
        }
    }
}
