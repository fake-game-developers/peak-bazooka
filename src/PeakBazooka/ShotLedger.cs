using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// Every client's record of the shots the host accepted (shooter actor + shot id) and which targets each one
/// already hit. One per client, not per gun: PEAK destroys a held item when it is put away, and a rocket still in
/// the air must keep its hits valid.
/// </summary>
internal static class ShotLedger
{
    // How long an accepted shot id stays valid for hit RPCs.
    private const float AuthorizedShotLifetime = 10f;

    private static readonly Dictionary<long, float> authorizedShots = new();

    private static readonly Dictionary<long, HashSet<int>> consumedTargets = new();

    // Hit RPCs from the shooter can arrive before the host's ShotResult on other clients (different senders,
    // no cross-sender order). Keep those hits until the shot is authorized, then apply once.
    private static readonly Dictionary<long, List<PendingHit>> pendingHits = new();

    private readonly struct PendingHit
    {
        public readonly int TargetId;
        public readonly System.Action Apply;

        public PendingHit(int targetId, System.Action apply)
        {
            TargetId = targetId;
            Apply = apply;
        }
    }

    private static long ShotKey(int shooterActor, int shotId) => ((long)shooterActor << 32) | (uint)shotId;

    internal static void Clear()
    {
        authorizedShots.Clear();
        consumedTargets.Clear();
        pendingHits.Clear();
    }

    internal static void RecordAuthorizedShot(int shooterActor, int shotId)
    {
        float now = Time.time;
        var expired = new List<long>();
        foreach (KeyValuePair<long, float> shot in authorizedShots)
        {
            if (now - shot.Value > AuthorizedShotLifetime)
            {
                expired.Add(shot.Key);
            }
        }

        foreach (long key in expired)
        {
            authorizedShots.Remove(key);
            consumedTargets.Remove(key);
            pendingHits.Remove(key);
        }

        long authorizedKey = ShotKey(shooterActor, shotId);
        authorizedShots[authorizedKey] = now;
        FlushPendingHits(authorizedKey);
    }

    /// <summary>
    /// Receiving end of a hit: true once per target for each shot the host accepted from that sender.
    /// Offline there is nothing to check. When the shot is not authorized yet, returns false — use
    /// <see cref="TryConsumeOrDeferHit"/> for hits that may race ahead of the host's shot result.
    /// </summary>
    internal static bool TryConsumeShot(Photon.Realtime.Player? sender, int shotId, int targetId)
    {
        if (!PhotonNetwork.InRoom)
        {
            return true;
        }

        if (sender == null)
        {
            return false;
        }

        long key = ShotKey(sender.ActorNumber, shotId);
        return authorizedShots.ContainsKey(key) && ConsumeTarget(key, targetId);
    }

    /// <summary>
    /// Like <see cref="TryConsumeShot"/>, but if the host's acceptance has not arrived yet, queues
    /// <paramref name="apply"/> and runs it when the shot becomes authorized (still once per target).
    /// Returns true when the caller should run <paramref name="apply"/> immediately.
    /// </summary>
    internal static bool TryConsumeOrDeferHit(Photon.Realtime.Player? sender, int shotId, int targetId, System.Action apply)
    {
        if (!PhotonNetwork.InRoom)
        {
            return true;
        }

        if (sender == null)
        {
            return false;
        }

        long key = ShotKey(sender.ActorNumber, shotId);
        if (!authorizedShots.ContainsKey(key))
        {
            if (!pendingHits.TryGetValue(key, out List<PendingHit> list))
            {
                list = new List<PendingHit>();
                pendingHits[key] = list;
            }

            list.Add(new PendingHit(targetId, apply));
            return false;
        }

        return ConsumeTarget(key, targetId);
    }

    private static bool ConsumeTarget(long key, int targetId)
    {
        if (!consumedTargets.TryGetValue(key, out HashSet<int> targets))
        {
            targets = new HashSet<int>();
            consumedTargets[key] = targets;
        }

        return targets.Add(targetId);
    }

    private static void FlushPendingHits(long key)
    {
        if (!pendingHits.TryGetValue(key, out List<PendingHit> list))
        {
            return;
        }

        pendingHits.Remove(key);
        foreach (PendingHit hit in list)
        {
            if (ConsumeTarget(key, hit.TargetId))
            {
                hit.Apply();
            }
        }
    }
}
