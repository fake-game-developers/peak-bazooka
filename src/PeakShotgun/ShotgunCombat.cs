using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Hit resolution for what the shotgun can affect (creatures, hazards, other scouts). The rules live in
/// <see cref="ShootableRules"/> and each world target in its own <see cref="ShotTarget"/>; this class owns the
/// shared flow. In multiplayer the rules come from the host (<see cref="HostConfigSync"/>), not from each
/// client's config, and the host applies every non-character hit after re-checking it.
/// </summary>
internal static class ShotgunCombat
{
    /// <summary>Host-side (or offline) hit counts toward defeat for world targets, keyed by Photon view id or local instance id.</summary>
    private static readonly Dictionary<int, int> worldHitCounts = new();

    /// <summary>Defeats that every client applies itself (<see cref="ShotTarget.BroadcastsDefeat"/>), replayed to late joiners via a room property.</summary>
    private static readonly Dictionary<long, ShotTargetKind> worldDefeats = new();

    /// <summary>Centre of the blast being resolved, for targets whose collider is much bigger than they are.</summary>
    internal static Vector3 BlastPoint { get; set; }

    /// <summary>Clears host/offline world-target hit progress (map change / leave room).</summary>
    internal static void ClearWorldHitCounts()
    {
        worldHitCounts.Clear();
        worldDefeats.Clear();
    }

    internal static int[] PackWorldDefeats()
    {
        int[] packed = new int[worldDefeats.Count * 3];
        int i = 0;
        foreach (KeyValuePair<long, ShotTargetKind> entry in worldDefeats)
        {
            UnpackDefeatKey(entry.Key, out int viewId, out int extra);
            packed[i++] = viewId;
            packed[i++] = (int)entry.Value;
            packed[i++] = extra;
        }

        return packed;
    }

    internal static void ApplyPackedWorldDefeats(int[]? packed)
    {
        if (packed == null)
        {
            return;
        }

        for (int i = 0; i + 2 < packed.Length; i += 3)
        {
            int viewId = packed[i];
            var kind = (ShotTargetKind)packed[i + 1];
            int extra = packed[i + 2];
            worldDefeats[DefeatKey(viewId, extra)] = kind;
            PhotonView? view = PhotonView.Find(viewId);
            ClientApplyDefeat(view, kind, extra);
        }
    }

    private static long DefeatKey(int viewId, int extra) => ((long)viewId << 32) | (uint)extra;

    private static void UnpackDefeatKey(long key, out int viewId, out int extra)
    {
        viewId = (int)(key >> 32);
        extra = (int)key;
    }

    /// <summary>
    /// Applies one rocket hit to whatever is under the collider, if that target is enabled.
    /// <paramref name="strength"/> (0–1, how close to the middle of the blast) scales knockback and Injury on characters.
    /// Returns true when a target was handled.
    /// </summary>
    internal static bool TryHit(
        Collider collider,
        Character? shooter,
        BazookaShooter source,
        int shotId,
        Vector3 hitPoint,
        Vector3 direction,
        float strength)
    {
        if (collider == null)
        {
            return false;
        }

        // A layer outside Character and Default is only in the pellet mask because some target asked for it
        // (ShotTarget.ExtraLayerMask): offer it to those targets only, never as a scout.
        int layerBit = 1 << collider.gameObject.layer;
        if ((HelperFunctions.CharacterAndDefaultMask.value & layerBit) == 0)
        {
            return TryHitStage(ShotStage.World, collider, source, shotId, layerBit);
        }

        if (TryHitStage(ShotStage.BeforeCharacters, collider, source, shotId))
        {
            return true;
        }

        Character? character = collider.GetComponentInParent<Character>();
        if (character != null && character != shooter)
        {
            return TryHitCharacter(character, collider, source, shotId, hitPoint, direction, strength);
        }

        if (character != null)
        {
            return false;
        }

        return TryHitStage(ShotStage.World, collider, source, shotId);
    }

    /// <summary>First enabled target of that stage under the collider, in <see cref="ShotTargetRegistry"/> order.</summary>
    private static bool TryHitStage(ShotStage stage, Collider collider, BazookaShooter source, int shotId, int extraLayerBit = 0)
    {
        foreach (ShotTarget target in ShotTargetRegistry.All)
        {
            if (target.Stage != stage || target.Broken || !target.Rule.Enabled)
            {
                continue;
            }

            if (extraLayerBit != 0 && (target.ExtraLayerMask & extraLayerBit) == 0)
            {
                continue;
            }

            Component? found = ShotTargetRegistry.Classify(target, collider);
            if (found != null)
            {
                HitWorldTarget(target, found, source, shotId);
                return true;
            }
        }

        return false;
    }

    private static void HitWorldTarget(ShotTarget target, Component component, BazookaShooter source, int shotId)
    {
        PhotonView? view = ShotTargetRegistry.ViewOf(target, component);
        int extra = ShotTargetRegistry.ExtraOf(target, component);
        if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient || view == null)
        {
            ApplyRegisteredHit(target, component, view, source, extra);
            return;
        }

        source.photonView.RPC(
            nameof(BazookaShooter.RPC_HostShootTarget),
            RpcTarget.MasterClient,
            view.ViewID,
            (int)target.Kind,
            shotId,
            extra);
    }

    /// <summary>
    /// Host side of <see cref="BazookaShooter.RPC_HostShootTarget"/>: find the claimed target under the view and apply
    /// it only if it really is that kind of target and the room allows shooting it.
    /// </summary>
    internal static void HostApply(PhotonView view, ShotTargetKind kind, BazookaShooter? source = null, int extra = 0)
    {
        ShotTarget? target = ShotTargetRegistry.For(kind);
        if (target == null || !target.Rule.Enabled)
        {
            return;
        }

        Component? component = ShotTargetRegistry.FindFromView(target, view, extra);

        // The view must be the target's own, so a forged id cannot destroy some larger object that merely
        // contains a mandrake or spore emitter.
        if (component != null && ShotTargetRegistry.ViewOf(target, component) == view)
        {
            ApplyRegisteredHit(target, component, view, source, extra);
        }
    }

    /// <summary>Runs on the host (or offline, or for a target with no network view).</summary>
    private static void ApplyRegisteredHit(ShotTarget target, Component component, PhotonView? view, BazookaShooter? source, int extra)
    {
        if (!target.Rule.Enabled)
        {
            return;
        }

        if (!RegisterWorldHitTowardDefeat(target, component, view, extra))
        {
            ShotTargetRegistry.PartialHit(target, component, new ShotHit(view, source, extra));
            return;
        }

        if (ShotTargetRegistry.Defeat(target, component, new ShotHit(view, source, extra)))
        {
            ReplicateWorldDefeat(target, source, view, extra);
        }
    }

    private static void ReplicateWorldDefeat(ShotTarget target, BazookaShooter? source, PhotonView? view, int extra)
    {
        if (!target.BroadcastsDefeat)
        {
            return;
        }

        if (view != null && view.ViewID != 0)
        {
            worldDefeats[DefeatKey(view.ViewID, extra)] = target.Kind;
        }

        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        HostConfigSync.PublishWorldDefeats();
        if (source != null && view != null)
        {
            source.photonView.RPC(nameof(BazookaShooter.RPC_ApplyWorldDefeat), RpcTarget.Others, view.ViewID, (int)target.Kind, extra);
        }
    }

    /// <summary>
    /// Host or offline, from <see cref="ShotTarget.Defeat"/>: runs <see cref="ShotTarget.Effect"/> on every client
    /// (<see cref="BazookaShooter.RPC_ShotEffect"/>). Not replayed to late joiners.
    /// </summary>
    internal static void BroadcastEffect(ShotTarget target, Component component, ShotHit hit, Vector3 push, int variant = 0)
    {
        if (PhotonNetwork.InRoom && hit.Shooter != null && hit.View != null && hit.View.ViewID != 0)
        {
            hit.Shooter.photonView.RPC(
                nameof(BazookaShooter.RPC_ShotEffect),
                RpcTarget.All,
                hit.View.ViewID,
                (int)target.Kind,
                hit.Extra,
                push,
                variant);
            return;
        }

        ShotTargetRegistry.Effect(target, component, push, variant);
    }

    /// <summary>Every client: the host's <see cref="BroadcastEffect"/> arrived.</summary>
    internal static void ApplyEffect(PhotonView? view, ShotTargetKind kind, int extra, Vector3 push, int variant)
    {
        ShotTarget? target = ShotTargetRegistry.For(kind);
        if (target == null || view == null)
        {
            return;
        }

        Component? component = ShotTargetRegistry.FindFromView(target, view, extra);
        if (component != null)
        {
            ShotTargetRegistry.Effect(target, component, push, variant);
        }
    }

    /// <summary>Other clients / late joiners: apply a host-authoritative defeat that has no vanilla RPC.</summary>
    internal static void ClientApplyDefeat(PhotonView? view, ShotTargetKind kind, int extra = 0)
    {
        ShotTarget? target = ShotTargetRegistry.For(kind);
        if (target != null && target.BroadcastsDefeat)
        {
            ShotTargetRegistry.ClientDefeat(target, view, extra);
        }
    }

    /// <summary>
    /// Counts this rocket toward defeating a world target. True when the count has reached
    /// <see cref="ShootableRule.HitsToDefeat"/> (then the counter is cleared).
    /// </summary>
    private static bool RegisterWorldHitTowardDefeat(ShotTarget target, Component component, PhotonView? view, int extra)
    {
        int need = target.Rule.HitsToDefeat;
        if (need <= 1)
        {
            return true;
        }

        int key = view != null && view.ViewID != 0
            ? view.ViewID ^ (extra * unchecked((int)0x9E3779B9))
            : unchecked((int)0x80000000) ^ component.GetInstanceID();
        worldHitCounts.TryGetValue(key, out int hits);
        hits++;
        if (hits >= need)
        {
            worldHitCounts.Remove(key);
            return true;
        }

        worldHitCounts[key] = hits;
        return false;
    }

    private static bool TryHitCharacter(
        Character character,
        Collider collider,
        BazookaShooter source,
        int shotId,
        Vector3 hitPoint,
        Vector3 direction,
        float strength)
    {
        // Never fall through to scout damage: the Scoutmaster's owner is the host, who would take the Injury.
        if (character.isScoutmaster)
        {
            return TryHitStage(ShotStage.Scoutmaster, collider, source, shotId);
        }

        if (GunCharacterLaunch.IsZombie(character))
        {
            if (!ShootableRules.Zombies.Enabled)
            {
                return false;
            }

            GunCharacterLaunch? launch = character.GetComponent<GunCharacterLaunch>();
            if (character.photonView.IsMine)
            {
                launch?.Blast(direction, strength, Plugin.Knockback);
            }
            else
            {
                character.photonView.RPC(
                    nameof(GunCharacterLaunch.RPC_BazookaBlast),
                    character.photonView.Owner,
                    direction,
                    strength,
                    Plugin.Knockback,
                    shotId);
            }

            return true;
        }

        if (!ShootableRules.Scouts.Enabled)
        {
            return false;
        }

        // TF2 mode: rockets are for moving around, not for hurting other scouts.
        if (Plugin.Tf2Mode)
        {
            return true;
        }

        source.photonView.RPC(nameof(BazookaShooter.RPC_GunImpact), RpcTarget.All, character.photonView.Owner, direction, strength, Plugin.Knockback, shotId);
        return true;
    }
}
