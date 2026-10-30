using System.Collections.Generic;
using HarmonyLib;
using Peak;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

public class Action_Gun : ItemAction
{
    public float maxDistance = 40f;

    public float pelletRadius = 0.2f;

    public float fireRate = 0.8f;

    public float spread = 0.07f;

    public int pelletCount = 8;

    [SerializeReference]
    public Affliction[] afflictionsOnHit = [];

    public Transform? spawnTransform;

    public SFX_Instance? shotSFX;

    public System.Action? OnShoot;

    // An unanswered request is re-sent with the same id after this long (message lost, host changed).
    private const float PendingShotRetry = 1.5f;

    // Requests sent fireRate apart can arrive bunched up by network jitter.
    private const float HostFireRateTolerance = 0.9f;

    // How long an accepted shot id stays valid for hit RPCs.
    private const float AuthorizedShotLifetime = 10f;

    private static int nextShotId;

    private float lastShootTime = float.NegativeInfinity;

    private int pendingShotId;

    private float pendingShotSentTime;

    private Vector3 pendingForward;

    // Host-side: newest shot id per sender (actor number) and the answer given, so a retry gets the same answer.
    private readonly Dictionary<int, (int shotId, bool accepted)> lastShotBySender = new();

    private double lastAcceptedShotTime = double.NegativeInfinity;

    // Every client: shots the host accepted (shooter actor + shot id) and which targets each one already hit.
    private readonly Dictionary<long, float> authorizedShots = new();

    private readonly Dictionary<long, HashSet<int>> consumedTargets = new();

    // Hit RPCs from the shooter can arrive before the host's ShotResult on other clients (different senders,
    // no cross-sender order). Keep those hits until the shot is authorized, then apply once.
    private readonly Dictionary<long, List<PendingHit>> pendingHits = new();

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
            RecordAuthorizedShot(shooterActor, shotId);
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

    private static long ShotKey(int shooterActor, int shotId) => ((long)shooterActor << 32) | (uint)shotId;

    private void RecordAuthorizedShot(int shooterActor, int shotId)
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
    /// <see cref="TryConsumeOrDeferHit"/> for hits that may race ahead of <c>RPC_ShotResult</c>.
    /// </summary>
    internal bool TryConsumeShot(Photon.Realtime.Player? sender, int shotId, int targetId)
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
            return false;
        }

        return ConsumeTarget(key, targetId);
    }

    /// <summary>
    /// Like <see cref="TryConsumeShot"/>, but if the host's acceptance has not arrived yet, queues
    /// <paramref name="apply"/> and runs it when the shot becomes authorized (still once per target).
    /// Returns true when the caller should run <paramref name="apply"/> immediately.
    /// </summary>
    internal bool TryConsumeOrDeferHit(Photon.Realtime.Player? sender, int shotId, int targetId, System.Action apply)
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

    private bool ConsumeTarget(long key, int targetId)
    {
        if (!consumedTargets.TryGetValue(key, out HashSet<int> targets))
        {
            targets = new HashSet<int>();
            consumedTargets[key] = targets;
        }

        return targets.Add(targetId);
    }

    private void FlushPendingHits(long key)
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

    /// <summary>Runs on the shooter once the host has accepted the shot.</summary>
    private void Fire(int shotId, Vector3 forward)
    {
        // A gun put away before the answer arrived just loses the shot.
        if (spawnTransform == null || character == null || !character.IsLocal)
        {
            return;
        }

        OnShoot?.Invoke();

        Vector3 origin = spawnTransform.position;
        // Recoil is a ragdoll shove; skip it in jetpack/air-control mode so the shot doesn't spin you.
        if (Plugin.Recoil > 0f && Plugin.SelfRagdoll)
        {
            character.AddForce(-forward * (Plugin.Recoil / Time.fixedDeltaTime));
        }

        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_LaunchRocket), RpcTarget.All, origin, forward, shotId);
        }
        else
        {
            SpawnRocket(origin, forward, shotId, reportsImpact: true);
        }

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

    [PunRPC]
    private void RPC_LaunchRocket(Vector3 origin, Vector3 forward, int shotId, PhotonMessageInfo info)
    {
        bool reportsImpact = character != null && character.IsLocal
            && (info.Sender == null || info.Sender == PhotonNetwork.LocalPlayer);
        SpawnRocket(origin, forward, shotId, reportsImpact);
    }

    private void SpawnRocket(Vector3 origin, Vector3 forward, int shotId, bool reportsImpact)
    {
        GameObject rocketObject = BuildRocketVisual();
        rocketObject.transform.position = origin;
        var rocket = rocketObject.AddComponent<BazookaRocket>();
        rocket.Gun = this;
        rocket.Shooter = character;
        rocket.ShotId = shotId;
        rocket.ReportsImpact = reportsImpact;
        rocket.Launch(origin, forward, Plugin.RocketSpeed);
    }

    private GameObject BuildRocketVisual()
    {
        Transform? shell = FindShellVisual();
        GameObject visual;
        if (shell != null)
        {
            visual = Instantiate(shell.gameObject);
            visual.name = "BazookaRocket";
            foreach (Collider extra in visual.GetComponentsInChildren<Collider>(true))
            {
                Destroy(extra);
            }
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "BazookaRocket";
            visual.transform.localScale = Vector3.one * 0.22f;
            Destroy(visual.GetComponent<Collider>());
            MeshRenderer? source = GetComponentInChildren<MeshRenderer>();
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null && source != null)
            {
                renderer.sharedMaterial = source.sharedMaterial;
            }
        }

        return visual;
    }

    private Transform? FindShellVisual()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == "Shell")
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>Shooter-only: tell everyone to explode here, then apply blast hits.</summary>
    internal void DetonateRocket(int shotId, Vector3 point, Vector3 direction)
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_RocketExplode), RpcTarget.All, point);
        }
        else
        {
            PlayExplosion(point);
        }

        ApplyBlast(shotId, point, direction);
    }

    [PunRPC]
    private void RPC_RocketExplode(Vector3 point)
    {
        PlayExplosion(point);
        foreach (BazookaRocket rocket in Object.FindObjectsByType<BazookaRocket>(FindObjectsSortMode.None))
        {
            if (rocket != null && rocket.Gun == this)
            {
                Destroy(rocket.gameObject);
            }
        }
    }

    private static void PlayExplosion(Vector3 point)
    {
        if (Plugin.ExplosionPrefab != null)
        {
            GameObject fx = Object.Instantiate(Plugin.ExplosionPrefab, point, Quaternion.identity);
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
        }

        GamefeelHandler.instance.AddPerlinShakeProximity(point, Plugin.ExplosionShake);
    }

    private void ApplyBlast(int shotId, Vector3 point, Vector3 fallbackDirection)
    {
        if (character == null || !character.IsLocal)
        {
            return;
        }

        ApplySelfBlast(point);

        int blastHits = Plugin.ExplosionHits;
        var struck = new HashSet<int>();
        int mask = HelperFunctions.CharacterAndDefaultMask | ShotTargetRegistry.ExtraLayerMask;
        int terrain = HelperFunctions.terrainMapMask.value;
        Collider[] hits = Physics.OverlapSphere(
            point,
            Plugin.ExplosionRadius,
            mask,
            QueryTriggerInteraction.Collide);
        foreach (Collider candidate in hits)
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

            if (!struck.Add(id))
            {
                continue;
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

            Vector3 direction = hitPoint - point;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = fallbackDirection;
            }
            else
            {
                direction.Normalize();
            }

            ShotgunCombat.TryHit(candidate, character, this, shotId, hitPoint, direction, blastHits);
        }
    }

    /// <summary>
    /// Own-rocket launch. Knockback falls off with distance.
    /// <see cref="Plugin.TF2RocketJump"/>: WASD, no ragdoll, no self-Injury.
    /// Otherwise <see cref="Plugin.SelfRagdoll"/> flops you and may apply <see cref="Plugin.SelfDamage"/>.
    /// </summary>
    private void ApplySelfBlast(Vector3 point)
    {
        Character shooter = character!;
        if (shooter.data.dead)
        {
            return;
        }

        float radius = Plugin.ExplosionRadius;
        Vector3 center = shooter.Center;
        // Chest-to-crater is ~1m on a floor shot, which used to sit near the edge of a 1.5m fireball.
        // Measure the nearest body collider so a jump at your feet still launches.
        float distance = NearestBlastDistance(shooter, point, center);
        if (distance > radius)
        {
            return;
        }

        float falloff = 1f - distance / radius;
        falloff *= falloff;

        float speed = Plugin.SelfKnockback * falloff;
        if (Plugin.SelfRagdoll)
        {
            shooter.Fall(GunCharacterLaunch.BlastRagdollSeconds);
        }

        if (speed > 0.01f)
        {
            Vector3 away = center - point;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector3.up;
            }

            Vector3 launch = (away.normalized + Vector3.up * Plugin.SelfKnockbackUp).normalized;
            // Bodypart forces apply once per physics step, so this is exactly `speed` m/s.
            shooter.AddForce(launch * (speed / Time.fixedDeltaTime));
        }

        if (Plugin.SelfRagdoll)
        {
            float injury = Plugin.SelfDamage * falloff;
            if (injury > 0.0001f)
            {
                shooter.refs.afflictions.AddAffliction(
                    new Affliction_AdjustStatus(CharacterAfflictions.STATUSTYPE.Injury, injury, 1f));
            }

            return;
        }

        shooter.GetComponent<GunCharacterLaunch>()?.BeginAirSteer();
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
    public void RPC_GunImpact(Photon.Realtime.Player? hitPlayer, Vector3 endpoint, Vector3 direction, int shotId, PhotonMessageInfo info)
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
        if (!TryConsumeOrDeferHit(info.Sender, shotId, targetId, () => ApplyGunImpact(local, direction, endpoint)))
        {
            return;
        }

        ApplyGunImpact(local, direction, endpoint);
    }

    private void ApplyGunImpact(Character local, Vector3 direction, Vector3 endpoint)
    {
        if (!ShootableRules.Scouts.Enabled)
        {
            return;
        }

        local.GetComponent<GunCharacterLaunch>().Blast(direction, endpoint);
        foreach (Affliction affliction in afflictionsOnHit)
        {
            local.refs.afflictions.AddAffliction(affliction);
        }
    }

    /// <summary>
    /// Host-only: apply a hit on a non-character target after checking the shot was accepted and the
    /// room still allows shooting that kind of target.
    /// </summary>
    [PunRPC]
    public void RPC_HostShootTarget(int viewId, int kind, int shotId, int extra, int hits, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || !TryConsumeShot(info.Sender, shotId, viewId))
        {
            return;
        }

        PhotonView? view = PhotonView.Find(viewId);
        if (view != null)
        {
            ShotgunCombat.HostApply(view, (ShotTargetKind)kind, this, extra, hits);
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
