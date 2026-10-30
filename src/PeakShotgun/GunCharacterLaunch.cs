using System;
using System.Collections;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

public class GunCharacterLaunch : MonoBehaviourPunCallbacks
{
    private static readonly Type? ZombieType = GameCompat.FindType(GameCompat.ZombieTypeName);

    // A coconut-style impulse is applied to every bone, which launches the body.
    // This is a short acceleration instead, so the ragdoll tips over in place.
    internal const float BlastRagdollSeconds = 3f;

    private const float ShoveAcceleration = 36f;

    private const float ShoveSeconds = 0.3f;

    private const float ZombieDrowsyDamage = 0.5f;

    // Zombie shot state is owned by the zombie's owner and mirrored to everyone (RPC_BazookaZombieShotState), so a new
    // owner after host migration continues from it and late joiners see a knocked-down zombie as silenced.
    private int shotgunHits;

    private bool heldDown;

    internal bool Silenced { get; private set; }

    private float shoveTime;

    private Vector3 shoveDirection;

    private Coroutine? airSteerRoutine;

    public static bool IsZombie(Character character) =>
        ZombieType != null && character.GetComponent(ZombieType) != null;

    /// <summary>
    /// Zombie hit sent by a remote shooter. The zombie's owner applies it only for a shot the host accepted
    /// (<paramref name="gunViewId"/> + <paramref name="shotId"/>) and only while the room allows shooting zombies.
    /// </summary>
    [PunRPC]
    public void RPC_BazookaBlast(Vector3 direction, Vector3 point, int gunViewId, int shotId, int hits, PhotonMessageInfo info)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine || !IsZombie(character) || !ShootableRules.Zombies.Enabled)
        {
            return;
        }

        PhotonView? gunView = PhotonView.Find(gunViewId);
        Action_Gun? gun = gunView != null ? gunView.GetComponent<Action_Gun>() : null;
        if (gun == null)
        {
            return;
        }

        int targetId = character.photonView.ViewID;
        if (!gun.TryConsumeOrDeferHit(info.Sender, shotId, targetId, () => Blast(direction, point, hits)))
        {
            return;
        }

        Blast(direction, point, hits);
    }

    /// <summary>
    /// Rocket jump with WASD: stay out of <see cref="Character.Fall"/>, kill spin, keep ragdoll control.
    /// Does not pin <c>sinceGrounded</c> or <c>isJumping</c> — those are PEAK's jetpack hover
    /// (+10 gravity while jumping at t=0) and would pull you into the ceiling.
    /// </summary>
    internal void BeginAirSteer()
    {
        if (airSteerRoutine != null)
        {
            StopCoroutine(airSteerRoutine);
        }

        Character? character = GetComponent<Character>();
        character?.refs.movement?.CapFallDamage(0f, 15f);
        airSteerRoutine = StartCoroutine(AirSteerRoutine());
    }

    private IEnumerator AirSteerRoutine()
    {
        Character? character = GetComponent<Character>();
        float timeout = Time.unscaledTime + 12f;
        while (character != null && !character.data.dead && Time.unscaledTime < timeout)
        {
            HoldAirSteer(character);
            yield return new WaitForFixedUpdate();
            if (character.data.isGrounded && character.data.groundedFor > 0.08f)
            {
                break;
            }
        }

        airSteerRoutine = null;
    }

    private static void HoldAirSteer(Character character)
    {
        if (character.data.fallSeconds > 0f)
        {
            character.RPCA_UnFall();
        }

        // FallTime is min(sinceJump, sinceGrounded). A long sinceJump from walking plus a 2s
        // hang is ~30% Injury on land. Keep the jump timer at 0 and cap fall damage at 0.
        character.data.sinceJump = 0f;
        character.refs.movement?.CapFallDamage(0f, 2f);

        if (!character.data.isGrounded)
        {
            // Gravity is Lerp(0, maxGravity=-20, curve(sinceGrounded)). 1+ is a full downward pull.
            // isJumping at sinceGrounded 0 is Lerp(+10, -20, 0) = upward — never set that here.
            if (character.data.sinceGrounded < 1f)
            {
                character.data.sinceGrounded = 1f;
            }

            character.data.isJumping = false;
        }

        character.ClampRagdollControl(1f);
        character.data.currentRagdollControll = 1f;
        ZeroSpin(character);
    }

    private static void ZeroSpin(Character character)
    {
        CharacterRagdoll? ragdoll = character.refs.ragdoll;
        if (ragdoll?.partList == null)
        {
            return;
        }

        foreach (Bodypart part in ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig != null && !rig.isKinematic)
            {
                rig.angularVelocity = Vector3.zero;
            }
        }
    }

    public void Blast(Vector3 direction, Vector3 point, int hits = 1)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine || character.data.dead)
        {
            return;
        }

        Vector3 force = (direction.normalized + Vector3.up * 0.45f).normalized;
        character.Fall(BlastRagdollSeconds);
        shoveDirection = force;
        shoveTime = ShoveSeconds;
        if (!IsZombie(character))
        {
            return;
        }

        shotgunHits += Mathf.Max(1, hits);
        if (shotgunHits >= ShootableRules.Zombies.HitsToDefeat)
        {
            KnockDown(character);
            return;
        }

        BroadcastShotState();

        // Injury does not affect zombies. The first shot only adds drowsy, so they can get back up.
        character.refs.afflictions.AddAffliction(
            new Affliction_AdjustStatus(CharacterAfflictions.STATUSTYPE.Drowsy, ZombieDrowsyDamage, 1f));
    }

    // Bodypart forces accumulate until the next physics step, so the shove has to be added once per step,
    // not once per rendered frame, or its strength scales with frame rate.
    private void FixedUpdate()
    {
        if (shoveTime <= 0f)
        {
            return;
        }

        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine)
        {
            shoveTime = 0f;
            return;
        }

        float step = Time.fixedDeltaTime;
        float fraction = Mathf.Min(shoveTime, step) / step;
        shoveTime -= step;
        character.AddForce(shoveDirection * (ShoveAcceleration * fraction), 1f, 1f);
    }

    private void LateUpdate()
    {
        if (!heldDown)
        {
            return;
        }

        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine)
        {
            return;
        }

        if (character.data.dead)
        {
            heldDown = false;
            BroadcastShotState();
            return;
        }

        // A finished lunge recovery sends the zombie back to chasing. Hold the knockout.
        HoldDown(character);
    }

    private void KnockDown(Character character)
    {
        heldDown = true;
        character.PassOutInstantly();
        HoldDown(character);
        Silence();
        BroadcastShotState();
    }

    private void BroadcastShotState()
    {
        if (PhotonNetwork.InRoom && photonView != null)
        {
            photonView.RPC(nameof(RPC_BazookaZombieShotState), RpcTarget.Others, shotgunHits, heldDown);
        }
    }

    [PunRPC]
    public void RPC_BazookaZombieShotState(int hits, bool down, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender != photonView.Owner)
        {
            return;
        }

        shotgunHits = hits;
        heldDown = down;
        if (down)
        {
            Silence();
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        if (photonView != null && photonView.IsMine && (shotgunHits > 0 || heldDown))
        {
            photonView.RPC(nameof(RPC_BazookaZombieShotState), newPlayer, shotgunHits, heldDown);
        }
    }

    private void Silence()
    {
        if (Silenced)
        {
            return;
        }

        Silenced = true;
        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            source.mute = true;
        }
    }

    private static void HoldDown(Character character)
    {
        character.refs.afflictions.SetStatus(CharacterAfflictions.STATUSTYPE.Drowsy, 1f, false);
        character.data.passedOut = true;
        character.data.fullyPassedOut = true;
        if (character.data.fallSeconds < 1f)
        {
            character.data.fallSeconds = 5f;
        }
    }
}
