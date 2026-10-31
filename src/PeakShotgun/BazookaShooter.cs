using System.Collections.Generic;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Everything that happens after the trigger: the rocket, its explosion, the blast and every hit RPC. It sits on
/// every <see cref="Character"/> and talks through the scout's own Photon view, because PEAK destroys a held item
/// when it is put away: a rocket tied to the <see cref="Action_Gun"/> vanished without a bang when you switched
/// item right after firing.
/// </summary>
public class BazookaShooter : MonoBehaviourPun
{
    /// <summary>Standard mode: Injury on a scout in the middle of the blast; half that at its edge.</summary>
    private const float ScoutInjury = 0.4f;

    /// <summary>Speed in m/s given to loose items on the ground at the edge / in the middle of the blast.</summary>
    private const float LooseItemMinSpeed = 2f;

    private const float LooseItemMaxSpeed = 7f;

    private Character? character;

    internal Character? Character => character;

    private void Awake() => character = GetComponent<Character>();

    /// <summary>Shooter, after the host accepted the shot: every client spawns the rocket.</summary>
    internal void LaunchRocket(Vector3 origin, Vector3 forward, int shotId)
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_LaunchRocket), RpcTarget.All, origin, forward, shotId);
            return;
        }

        SpawnRocket(origin, forward, shotId, reportsImpact: true);
    }

    [PunRPC]
    private void RPC_LaunchRocket(Vector3 origin, Vector3 forward, int shotId, PhotonMessageInfo info)
    {
        if (!FromOwner(info))
        {
            return;
        }

        // Only the shooter's own copy reports the impact, so the blast is applied once.
        SpawnRocket(origin, forward, shotId, reportsImpact: photonView.IsMine);
    }

    private bool FromOwner(PhotonMessageInfo info) =>
        !PhotonNetwork.InRoom || (info.Sender != null && info.Sender == photonView.Owner);

    private void SpawnRocket(Vector3 origin, Vector3 forward, int shotId, bool reportsImpact)
    {
        GameObject rocketObject = BuildRocketVisual();
        rocketObject.transform.position = origin;
        var rocket = rocketObject.AddComponent<BazookaRocket>();
        rocket.Shooter = this;
        rocket.ShotId = shotId;
        rocket.ReportsImpact = reportsImpact;
        rocket.Launch(origin, forward, Plugin.RocketSpeed);
    }

    /// <summary>A copy of the bazooka's loaded shell, from the registered prefab (the held gun may be gone).</summary>
    private static GameObject BuildRocketVisual()
    {
        GameObject? prefab = Plugin.ShotgunPrefab;
        Transform? shell = null;
        if (prefab != null)
        {
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == "Shell")
                {
                    shell = child;
                    break;
                }
            }
        }

        GameObject visual;
        if (shell != null)
        {
            visual = Instantiate(shell.gameObject);
            visual.SetActive(true);
            foreach (Collider extra in visual.GetComponentsInChildren<Collider>(true))
            {
                Destroy(extra);
            }
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.localScale = Vector3.one * 0.22f;
            Destroy(visual.GetComponent<Collider>());
            MeshRenderer? source = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>(true) : null;
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null && source != null)
            {
                renderer.sharedMaterial = source.sharedMaterial;
            }
        }

        visual.name = "BazookaRocket";
        return visual;
    }

    /// <summary>Shooter-only: tell everyone to explode here, then apply blast hits.</summary>
    internal void DetonateRocket(int shotId, Vector3 point, Vector3 direction)
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_RocketExplode), RpcTarget.All, point, shotId);
        }
        else
        {
            PlayExplosion(point);
        }

        ApplyBlast(shotId, point, direction);
    }

    [PunRPC]
    private void RPC_RocketExplode(Vector3 point, int shotId, PhotonMessageInfo info)
    {
        if (!FromOwner(info))
        {
            return;
        }

        PlayExplosion(point);
        // Only this shot's copy: a second rocket from the same scout may still be in the air.
        foreach (BazookaRocket rocket in FindObjectsByType<BazookaRocket>(FindObjectsSortMode.None))
        {
            if (rocket != null && rocket.Shooter == this && rocket.ShotId == shotId)
            {
                Destroy(rocket.gameObject);
            }
        }
    }

    private static void PlayExplosion(Vector3 point)
    {
        if (Plugin.ExplosionPrefab != null)
        {
            // The template is inactive; set it up, then switch it on so ExplosionEffect.Start sees the new size.
            GameObject fx = Instantiate(Plugin.ExplosionPrefab, point, Quaternion.identity);
            float visual = Plugin.ExplosionRadius / Plugin.DynamiteExplosionRadius;
            fx.transform.localScale = Vector3.one * visual;

            ExplosionEffect? effect = fx.GetComponent<ExplosionEffect>() ?? fx.GetComponentInChildren<ExplosionEffect>();
            if (effect != null)
            {
                effect.baseScale *= visual;
                effect.explosionRadius = Plugin.ExplosionRadius;
                effect.explosionPointCount = 1;
                effect.subExplosionPointCount = 0;
                effect.minDelay = 0f;
                effect.maxDelay = 0f;
            }

            float volume = Plugin.ExplosionVolume;
            foreach (AudioSource audio in fx.GetComponentsInChildren<AudioSource>(true))
            {
                audio.volume *= volume;
                if (audio.isPlaying)
                {
                    audio.Stop();
                    audio.Play();
                }
            }

            fx.SetActive(true);
            Destroy(fx, Plugin.ExplosionCleanupSeconds);
        }

        GamefeelHandler.instance.AddPerlinShakeProximity(point, Plugin.ExplosionShake);
        PushLooseItems(point);
    }

    /// <summary>
    /// Every client nudges the items on the ground that it owns (only the owner can move an item), like vanilla
    /// dynamite does, but sized to this blast.
    /// </summary>
    private static void PushLooseItems(Vector3 point)
    {
        float reach = Plugin.BlastReach;
        var pushed = new HashSet<Item>();
        foreach (Collider collider in Physics.OverlapSphere(point, reach, HelperFunctions.DefaultMask, QueryTriggerInteraction.Ignore))
        {
            Item? item = collider != null ? collider.GetComponentInParent<Item>() : null;
            if (item == null || item.rig == null || item.rig.isKinematic || item.itemState != ItemState.Ground
                || !item.photonView.IsMine || !pushed.Add(item))
            {
                continue;
            }

            Vector3 away = item.Center() - point;
            float strength = Mathf.Clamp01(1f - away.magnitude / reach);
            Vector3 direction = away.sqrMagnitude < 0.0001f ? Vector3.up : (away.normalized + Vector3.up * 0.6f).normalized;
            item.rig.AddForce(direction * Mathf.Lerp(LooseItemMinSpeed, LooseItemMaxSpeed, strength), ForceMode.VelocityChange);
        }
    }

    private void ApplyBlast(int shotId, Vector3 point, Vector3 fallbackDirection)
    {
        if (character == null || !character.IsLocal)
        {
            return;
        }

        ApplySelfBlast(character, point);

        // One entry per victim (a scout has many colliders, terrain shares a scene root): its nearest collider.
        var nearest = new Dictionary<int, (Collider collider, Vector3 hitPoint, float distance)>();
        int mask = HelperFunctions.CharacterAndDefaultMask | ShotTargetRegistry.ExtraLayerMask;
        int terrain = HelperFunctions.terrainMapMask.value;
        float radius = Plugin.BlastReach;
        foreach (Collider candidate in Physics.OverlapSphere(point, radius, mask, QueryTriggerInteraction.Collide))
        {
            if (candidate == null || ((1 << candidate.gameObject.layer) & terrain) != 0)
            {
                continue;
            }

            Character? victim = candidate.GetComponentInParent<Character>();
            if (victim == character)
            {
                continue;
            }

            int id;
            if (victim != null)
            {
                id = victim.GetInstanceID();
            }
            else
            {
                PhotonView? view = candidate.GetComponentInParent<PhotonView>();
                id = view != null && view.ViewID != 0 ? view.ViewID : candidate.GetInstanceID();
            }

            Vector3 hitPoint = candidate.transform.position;
            try
            {
                hitPoint = candidate.ClosestPoint(point);
            }
            catch (System.Exception)
            {
                // Non-convex mesh colliders reject ClosestPoint; the transform is close enough.
            }

            float distance = Vector3.Distance(hitPoint, point);
            if (!nearest.TryGetValue(id, out var best) || distance < best.distance)
            {
                nearest[id] = (candidate, hitPoint, distance);
            }
        }

        ShotgunCombat.BlastPoint = point;
        foreach ((Collider collider, Vector3 hitPoint, float distance) in nearest.Values)
        {
            Vector3 direction = hitPoint - point;
            direction = direction.sqrMagnitude < 0.0001f ? fallbackDirection : direction.normalized;
            // 1 in the middle of the blast, 0 at its edge: knockback and Injury scale with it.
            float strength = Mathf.Clamp01(1f - distance / radius);
            ShotgunCombat.TryHit(collider, character, this, shotId, hitPoint, direction, strength);
        }
    }

    /// <summary>
    /// Own-rocket launch. Standard: a ragdoll fling plus <see cref="Plugin.SelfDamage"/>. TF2: a rocket jump you
    /// steer with WASD, no Injury (<see cref="RocketFlight"/>).
    /// </summary>
    private static void ApplySelfBlast(Character shooter, Vector3 point)
    {
        if (shooter.data.dead)
        {
            return;
        }

        Vector3 center = shooter.Center;
        // Chest-to-crater is ~1m on a floor shot, near the edge of a 1.5m fireball.
        // Measure the nearest body collider so a jump at your feet still launches.
        float distance = NearestBlastDistance(shooter, point, center);
        Vector3 away = center - point;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector3.up;
        }

        if (Plugin.Tf2Mode)
        {
            RocketJump(shooter, distance, away.normalized);
            return;
        }

        float radius = Plugin.BlastReach;
        if (distance > radius)
        {
            return;
        }

        float strength = 1f - distance / radius;
        float falloff = strength * strength;

        shooter.Fall(GunCharacterLaunch.BlastRagdollSeconds);
        float speed = Plugin.SelfKnockback * falloff;
        if (speed > 0.01f)
        {
            Vector3 launch = (away.normalized + Vector3.up * Plugin.SelfKnockbackUp).normalized;
            // Bodypart forces apply once per physics step, so this is exactly `speed` m/s.
            shooter.AddForce(launch * (speed / Time.fixedDeltaTime));
        }

        // Same curve as a hit on another scout (full in the middle, half at the edge). Squared falloff left a
        // typical floor shot (~1m away) at a quarter of SelfDamage, too little to see.
        float injury = Plugin.SelfDamage * Mathf.Lerp(0.5f, 1f, strength);
        if (injury > 0.0001f)
        {
            shooter.refs.afflictions.AddAffliction(
                new Affliction_AdjustStatus(CharacterAfflictions.STATUSTYPE.Injury, injury, 1f));
        }
    }

    /// <summary>
    /// TF2 soldier jump: full <see cref="Plugin.Tf2LaunchSpeed"/> at point-blank, half at the edge of the blast,
    /// straight away from it (plus <see cref="Plugin.Tf2LaunchUp"/>). Shoot the floor to go up, the floor behind
    /// you to go forward, a wall to go sideways.
    /// </summary>
    private static void RocketJump(Character shooter, float distance, Vector3 away)
    {
        float reach = Plugin.BlastReach;
        float speed = Plugin.Tf2LaunchSpeed * (1f - 0.5f * (distance / reach));
        if (distance > reach || speed < 0.01f)
        {
            return;
        }

        Vector3 launch = (away + Vector3.up * Plugin.Tf2LaunchUp).normalized;
        shooter.GetComponent<GunCharacterLaunch>()?.BeginRocketFlight(launch * speed);
    }

    private static float NearestBlastDistance(Character shooter, Vector3 point, Vector3 fallback)
    {
        float best = Vector3.Distance(fallback, point);
        CharacterRagdoll? ragdoll = shooter.refs != null ? shooter.refs.ragdoll : null;
        if (ragdoll?.partList == null)
        {
            return best;
        }

        foreach (Bodypart part in ragdoll.partList)
        {
            if (part == null)
            {
                continue;
            }

            Collider[] cols = part.GetComponents<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                Collider col = cols[i];
                if (col == null || !col.enabled)
                {
                    continue;
                }

                try
                {
                    float d = (col.ClosestPoint(point) - point).magnitude;
                    if (d < best)
                    {
                        best = d;
                    }
                }
                catch (System.Exception)
                {
                    // Non-convex mesh colliders reject ClosestPoint.
                }
            }
        }

        return best;
    }

    [PunRPC]
    public void RPC_GunImpact(Photon.Realtime.Player? hitPlayer, Vector3 direction, float strength, int shotId, PhotonMessageInfo info)
    {
        if (hitPlayer == null || !hitPlayer.IsLocal)
        {
            return;
        }

        Character local = Character.localCharacter;
        if (local == null)
        {
            return;
        }

        int targetId = local.photonView.ViewID;
        if (!ShotLedger.TryConsumeOrDeferHit(info.Sender, shotId, targetId, () => ApplyGunImpact(local, direction, strength)))
        {
            return;
        }

        ApplyGunImpact(local, direction, strength);
    }

    private static void ApplyGunImpact(Character local, Vector3 direction, float strength)
    {
        // TF2 mode never hurts scouts. The shooter already skips us; this covers a shot fired before the mode changed.
        if (!ShootableRules.Scouts.Enabled || Plugin.Tf2Mode)
        {
            return;
        }

        strength = Mathf.Clamp01(strength);
        local.GetComponent<GunCharacterLaunch>().Blast(direction, strength);
        local.refs.afflictions.AddAffliction(new Affliction_AdjustStatus(
            CharacterAfflictions.STATUSTYPE.Injury,
            ScoutInjury * Mathf.Lerp(0.5f, 1f, strength),
            1f));
    }

    /// <summary>
    /// Host-only: apply a hit on a non-character target after checking the shot was accepted and the
    /// room still allows shooting that kind of target.
    /// </summary>
    [PunRPC]
    public void RPC_HostShootTarget(int viewId, int kind, int shotId, int extra, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || !ShotLedger.TryConsumeShot(info.Sender, shotId, viewId))
        {
            return;
        }

        PhotonView? view = PhotonView.Find(viewId);
        if (view != null)
        {
            ShotgunCombat.HostApply(view, (ShotTargetKind)kind, this, extra);
        }
    }

    /// <summary>Host → everyone else: condor / antlion / jellyfish defeat. Late joiners get the same list as a room property.</summary>
    [PunRPC]
    public void RPC_ApplyWorldDefeat(int viewId, int kind, int extra, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient)
        {
            return;
        }

        ShotgunCombat.ClientApplyDefeat(PhotonView.Find(viewId), (ShotTargetKind)kind, extra);
    }

    /// <summary>Host → everyone: short-lived feedback for a hit target (a push, a voice line).</summary>
    [PunRPC]
    public void RPC_ShotEffect(int viewId, int kind, int extra, Vector3 push, int variant, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient)
        {
            return;
        }

        ShotgunCombat.ApplyEffect(PhotonView.Find(viewId), (ShotTargetKind)kind, extra, push, variant);
    }
}
