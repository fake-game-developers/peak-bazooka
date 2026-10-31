using System;
using System.Collections;
using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

namespace Peak.Shotgun;

public class GunCharacterLaunch : MonoBehaviourPunCallbacks
{
    private static readonly Type? ZombieType = GameCompat.FindType(GameCompat.ZombieTypeName);

    /// <summary>Standard mode: how long your own blast ragdolls you.</summary>
    internal const float BlastRagdollSeconds = 2f;

    // A clear fling: several metres through the air at point-blank, a stumble at the edge. Not a dynamite launch.
    private const float MinShoveSpeed = 6f;

    private const float MaxShoveSpeed = 15f;

    private const float ShoveUp = 0.8f;

    // A knocked-out zombie is owned by the zombie's owner and mirrored to everyone (RPC_BazookaZombieShotState), so a
    // new owner after host migration keeps it down and late joiners see it silenced.
    private bool heldDown;

    internal bool Silenced { get; private set; }

    private Coroutine? flightRoutine;

    private float flightStarted;

    // Just past PEAK's 0.2s "still allowed to jump" window, so a flight cannot be extended with a mid-air jump.
    private const float FlightSinceGrounded = 0.21f;

    public static bool IsZombie(Character character) =>
        ZombieType != null && character.GetComponent(ZombieType) != null;

    /// <summary>
    /// Zombie hit sent by a remote shooter. The zombie's owner applies it only for a shot the host accepted
    /// (sender + <paramref name="shotId"/>) and only while the room allows shooting zombies.
    /// </summary>
    [PunRPC]
    public void RPC_BazookaBlast(Vector3 direction, float strength, int shotId, PhotonMessageInfo info)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine || !IsZombie(character) || !ShootableRules.Zombies.Enabled)
        {
            return;
        }

        int targetId = character.photonView.ViewID;
        if (!ShotLedger.TryConsumeOrDeferHit(info.Sender, shotId, targetId, () => Blast(direction, strength)))
        {
            return;
        }

        Blast(direction, strength);
    }

    /// <summary>
    /// TF2-mode rocket jump: add <paramref name="velocityChange"/> to the whole body (and the held item) and fly
    /// under <see cref="RocketFlight"/> physics until you land, grab something, or get ragdolled. A rocket fired
    /// mid-flight adds to the current velocity, so jumps chain.
    /// </summary>
    internal void BeginRocketFlight(Vector3 velocityChange)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.IsLocal || character.data.dead || character.data.fallSeconds > 0f)
        {
            return;
        }

        RocketFlight.Begin(character);
        foreach (Bodypart part in character.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig != null && !rig.isKinematic)
            {
                rig.linearVelocity += velocityChange;
            }
        }

        Rigidbody? itemRig = character.data.currentItem != null ? character.data.currentItem.rig : null;
        if (itemRig != null && !itemRig.isKinematic)
        {
            itemRig.linearVelocity += velocityChange;
        }

        // Like a vanilla jump: CharacterMovement.CanStand needs sinceJump > 0.3s, so the floor you blasted off
        // does not count as a landing. Only reset here, never every step, or you could never land again.
        character.data.sinceJump = 0f;
        character.refs.movement.CapFallDamage(0f, 2f);
        flightStarted = Time.time;
        flightRoutine ??= StartCoroutine(RocketFlightRoutine(character));
    }

    private IEnumerator RocketFlightRoutine(Character character)
    {
        var step = new WaitForFixedUpdate();
        while (true)
        {
            yield return step;
            if (!KeepFlying(character, Time.time - flightStarted))
            {
                break;
            }

            // FallTime is min(sinceJump, sinceGrounded), and PEAK only scores a landing after 0.5s off the ground.
            // Holding sinceGrounded short means landing from a flight is never a fall (no Injury, no ragdoll),
            // while landing itself still works.
            if (character.data.sinceGrounded > FlightSinceGrounded)
            {
                character.data.sinceGrounded = FlightSinceGrounded;
            }

            character.refs.movement.CapFallDamage(0f, 2f);
        }

        flightRoutine = null;
        RocketFlight.End();
    }

    private static bool KeepFlying(Character character, float seconds)
    {
        if (character == null)
        {
            return false;
        }

        CharacterData data = character.data;
        if (data.dead || data.passedOut || data.fullyPassedOut || data.fallSeconds > 0f
            || data.carrier != null || data.isClimbing || data.isRopeClimbing || data.isVineClimbing
            || seconds > 15f)
        {
            return false;
        }

        // Touching down ends it at once: walking, ground friction and the ground animation come straight back.
        // (The floor you launched from cannot count for 0.3s; see BeginRocketFlight.)
        return !data.isGrounded;
    }

    private void OnDestroy()
    {
        if (RocketFlight.IsFlying(GetComponent<Character>()))
        {
            RocketFlight.End();
        }
    }

    /// <summary>
    /// A rocket hit on a scout or zombie this client owns: ragdoll and a push away from the blast, both scaled by
    /// <paramref name="strength"/> (1 in the middle of the blast, 0 at its edge). Zombies are knocked out.
    /// </summary>
    public void Blast(Vector3 direction, float strength)
    {
        Character? character = GetComponent<Character>();
        if (character == null || !character.photonView.IsMine || character.data.dead)
        {
            return;
        }

        strength = Mathf.Clamp01(strength);
        character.Fall(Mathf.Lerp(1.5f, 3f, strength));

        // One velocity change on every bone, like the rocket jump. Pushing over several steps dragged the body along
        // the ground, where friction ate most of it. Fall() already zeroes ragdoll control on the next step; do it
        // now so PEAK's drag (scaled by control) cannot take a bite first.
        character.data.currentRagdollControll = 0f;
        float speed = Mathf.Lerp(MinShoveSpeed, MaxShoveSpeed, strength);
        Vector3 launch = (direction.normalized + Vector3.up * ShoveUp).normalized * speed;
        foreach (Bodypart part in character.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig != null && !rig.isKinematic)
            {
                rig.linearVelocity += launch;
            }
        }
        if (IsZombie(character))
        {
            KnockDown(character);
        }
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
            photonView.RPC(nameof(RPC_BazookaZombieShotState), RpcTarget.Others, heldDown);
        }
    }

    [PunRPC]
    public void RPC_BazookaZombieShotState(bool down, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender != photonView.Owner)
        {
            return;
        }

        heldDown = down;
        if (down)
        {
            Silence();
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        if (photonView != null && photonView.IsMine && heldDown)
        {
            photonView.RPC(nameof(RPC_BazookaZombieShotState), newPlayer, heldDown);
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
